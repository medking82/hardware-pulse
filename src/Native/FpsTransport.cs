using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace HardwarePulse {
    // Fixed-size, local-only protocol. The UI supplies process identity, never a
    // path or arguments for an elevated executable. Disconnect expires capture.
    public static class FpsProtocol {
        public const int RequestSize=20,ResponseSize=44;
        static SecurityIdentifier CurrentUser(){using(var identity=WindowsIdentity.GetCurrent())return identity.User;}
        public static readonly SecurityIdentifier User=CurrentUser();
        static readonly int Session=Process.GetCurrentProcess().SessionId;
        public static string PipeName {get{return "HardwarePulse-Fps-v1-"+User.Value+"-"+Session;}}
        public static byte[] Request(int pid,long started,long generation){using(var memory=new MemoryStream()){using(var writer=new BinaryWriter(memory)){writer.Write(pid);writer.Write(started);writer.Write(generation);return memory.ToArray();}}}
        public static byte[] Response(FrameMetrics value){using(var memory=new MemoryStream()){using(var writer=new BinaryWriter(memory)){writer.Write(value.Current);writer.Write(value.Average);writer.Write(value.Minimum);writer.Write(value.Low);writer.Write(value.Count);writer.Write(value.Ready?1:0);writer.Write(value.Status=="Live"?1:value.Status=="FPS capture needs administrator"?2:value.Status=="FPS capture failed"?3:0);return memory.ToArray();}}}
        public static FrameMetrics Metrics(byte[] bytes){using(var reader=new BinaryReader(new MemoryStream(bytes))){var value=new FrameMetrics{Current=reader.ReadDouble(),Average=reader.ReadDouble(),Minimum=reader.ReadDouble(),Low=reader.ReadDouble(),Count=reader.ReadInt32(),Ready=reader.ReadInt32()==1};int status=reader.ReadInt32();value.Status=status==1?"Live":status==2?"FPS capture needs administrator":status==3?"FPS capture failed":"Waiting for frames";return value;}}
        public static byte[] Read(PipeStream pipe,int count,int timeout){var data=new byte[count];int offset=0;var clock=Stopwatch.StartNew();while(offset<count){var pending=pipe.BeginRead(data,offset,count-offset,null,null);using(var wait=pending.AsyncWaitHandle){int remaining=timeout-(int)clock.ElapsedMilliseconds;if(remaining<=0||!wait.WaitOne(remaining)){pipe.Dispose();throw new IOException("FPS connection timed out");}int read=pipe.EndRead(pending);if(read==0)throw new EndOfStreamException();offset+=read;}}return data;}
        public static void Write(PipeStream pipe,byte[] data){var pending=pipe.BeginWrite(data,0,data.Length,null,null);using(var wait=pending.AsyncWaitHandle){if(!wait.WaitOne(3000)){pipe.Dispose();throw new IOException("FPS connection timed out");}pipe.EndWrite(pending);}}
        [DllImport("advapi32.dll",SetLastError=true)] static extern bool OpenProcessToken(IntPtr process,uint access,out IntPtr token);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool QueryFullProcessImageName(IntPtr process,int flags,System.Text.StringBuilder name,ref int size);
        public static bool OwnProcess(Process process){IntPtr token=IntPtr.Zero,handle=IntPtr.Zero;try{handle=OpenProcess(0x1000,false,process.Id);if(process.SessionId!=Session||handle==IntPtr.Zero||!OpenProcessToken(handle,8,out token))return false;using(var identity=new WindowsIdentity(token))return identity.User==User;}catch{return false;}finally{if(token!=IntPtr.Zero)CloseHandle(token);if(handle!=IntPtr.Zero)CloseHandle(handle);}}
        public sealed class TargetLease : IDisposable {
            IntPtr handle;
            public TargetLease(int pid,long started){handle=OpenProcess(0x1000,false,pid);if(handle==IntPtr.Zero||!Target(pid,started)){Dispose();throw new IOException("FPS target no longer available");}}
            // Keep the process object alive so Windows cannot recycle its PID
            // between validation and PresentMon attach, or during trace cleanup.
            public void Dispose(){if(handle!=IntPtr.Zero){CloseHandle(handle);handle=IntPtr.Zero;}}
        }
        public static bool Target(int pid,long started){if(pid<=0||started<=0)return false;try{using(var process=Process.GetProcessById(pid))return !process.HasExited&&process.StartTime.ToUniversalTime().Ticks==started&&OwnProcess(process);}catch{return false;}}
        public static bool Peer(uint pid,string exe){IntPtr handle=IntPtr.Zero;try{using(var process=Process.GetProcessById(checked((int)pid))){handle=OpenProcess(0x1000,false,process.Id);var name=new System.Text.StringBuilder(32768);int size=name.Capacity;return OwnProcess(process)&&handle!=IntPtr.Zero&&QueryFullProcessImageName(handle,0,name,ref size)&&string.Equals(name.ToString(),exe,StringComparison.OrdinalIgnoreCase);}}catch{return false;}finally{if(handle!=IntPtr.Zero)CloseHandle(handle);}}
        public static bool ProtectedTool(string path){
            string root=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).TrimEnd('\\')+"\\";
            path=Path.GetFullPath(path);if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!File.Exists(path))return false;
            for(string entry=path;entry!=null;entry=Path.GetDirectoryName(entry))if((File.GetAttributes(entry)&FileAttributes.ReparsePoint)!=0)return false;
            return true;
        }
    }
    public sealed class FpsServer : IDisposable {
        readonly PulsePaths paths;readonly Thread worker;readonly object gate=new object();NamedPipeServerStream pipe;volatile bool stopped;
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetNamedPipeClientProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe,out uint pid);
        public FpsServer(PulsePaths paths){this.paths=paths;worker=new Thread(Run){IsBackground=true,Name="Pulse FPS collector"};worker.Start();}
        void Run(){
            string tool=Path.Combine(paths.Root,"tools","PresentMon.exe");
            try{if(!FpsProtocol.ProtectedTool(tool))return;}catch{return;}
            while(!stopped){
                try{
                    var acl=new PipeSecurity();acl.SetAccessRuleProtection(true,false);acl.AddAccessRule(new PipeAccessRule(FpsProtocol.User,PipeAccessRights.ReadWrite,AccessControlType.Allow));
                    using(var connection=new NamedPipeServerStream(FpsProtocol.PipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,256,256,acl))
                    using(var capture=new FrameCapture()){
                        lock(gate){if(stopped)return;pipe=connection;}
                        connection.WaitForConnection();uint client;
                        if(!GetNamedPipeClientProcessId(connection.SafePipeHandle,out client)||!FpsProtocol.Peer(client,paths.Exe))continue;
                        int active=0;long birth=0,generation=0;DateTime retryAfter=DateTime.MinValue;FpsProtocol.TargetLease lease=null;
                        try{while(!stopped){
                            byte[] request=FpsProtocol.Read(connection,FpsProtocol.RequestSize,3000);int pid=BitConverter.ToInt32(request,0);long started=BitConverter.ToInt64(request,4),reset=BitConverter.ToInt64(request,12);
                            if(!FpsProtocol.Target(pid,started)){if(active!=0)capture.Dispose();if(lease!=null)lease.Dispose();lease=null;active=0;FpsProtocol.Write(connection,FpsProtocol.Response(new FrameMetrics{Status="Waiting for frames"}));continue;}
                            if(pid!=active||started!=birth||reset!=generation||(!capture.IsRunning&&DateTime.UtcNow>=retryAfter)){capture.Dispose();if(lease!=null)lease.Dispose();lease=new FpsProtocol.TargetLease(pid,started);capture.Start(tool,pid);active=pid;birth=started;generation=reset;retryAfter=DateTime.UtcNow.AddSeconds(10);}
                            FpsProtocol.Write(connection,FpsProtocol.Response(capture.Read()));
                        }}finally{capture.Dispose();if(lease!=null)lease.Dispose();}
                    }
                }catch(IOException){}catch(UnauthorizedAccessException){}catch(ObjectDisposedException){}catch(Exception e){try{File.WriteAllText(Path.Combine(paths.Runtime,"fps-warning.txt"),e.Message);}catch{}}
                finally{lock(gate){pipe=null;}}
                if(!stopped)Thread.Sleep(500);
            }
        }
        public void Dispose(){stopped=true;lock(gate){if(pipe!=null)pipe.Dispose();}if(Thread.CurrentThread!=worker)worker.Join(7000);}
    }
    public sealed class FpsClient : IDisposable {
        readonly string exe;readonly object gate=new object();Thread worker;NamedPipeClientStream pipe;bool stopped;int target;long birth,generation;FrameMetrics latest=new FrameMetrics{Status="Waiting for FPS collector"};
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetNamedPipeServerProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe,out uint pid);
        public FpsClient(string exe){this.exe=exe;}
        public void Select(int pid,long started){lock(gate){if(pid!=target||started!=birth){target=pid;birth=started;latest=new FrameMetrics{Status="Waiting for frames"};}if(worker==null){worker=new Thread(Run){IsBackground=true,Name="Pulse FPS reader"};worker.Start();}}}
        public void Reset(){lock(gate){generation++;latest=new FrameMetrics{Status="Waiting for frames"};}}
        public FrameMetrics Read(){lock(gate)return latest;}
        void Run(){while(true){lock(gate){if(stopped)return;}try{using(var connection=new NamedPipeClientStream(".",FpsProtocol.PipeName,PipeDirection.InOut,PipeOptions.Asynchronous,TokenImpersonationLevel.Identification)){
                    lock(gate){if(stopped)return;pipe=connection;}connection.Connect(1000);uint server;if(!GetNamedPipeServerProcessId(connection.SafePipeHandle,out server)||!FpsProtocol.Peer(server,exe))throw new IOException("Unexpected FPS collector");
                    while(true){int pid;long started,reset;lock(gate){if(stopped)return;pid=target;started=birth;reset=generation;}FpsProtocol.Write(connection,FpsProtocol.Request(pid,started,reset));var metrics=FpsProtocol.Metrics(FpsProtocol.Read(connection,FpsProtocol.ResponseSize,10000));lock(gate){if(pid==target&&started==birth&&reset==generation)latest=metrics;}Thread.Sleep(500);}
                }}catch(Exception){lock(gate){latest=new FrameMetrics{Status="Waiting for FPS collector"};}}finally{lock(gate){pipe=null;}}Thread.Sleep(1000);}}
        public void Dispose(){lock(gate){stopped=true;if(pipe!=null)pipe.Dispose();latest=new FrameMetrics{Status="FPS capture stopped"};}}
    }
}
