using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HardwarePulse {
    // Recovery through the installed official CLI. No login, turns or credential arguments.
    internal static class CodexAppServerQuota {
        internal static string InstalledExecutable(){
            string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","OpenAI","Codex","bin","codex.exe");
            return File.Exists(path)?path:null;
        }
        internal static QuotaReading Read(string executable,CancellationToken cancel){
            return QuotaDecoder.Decode("Codex",QuotaChildProcess.Read(executable,"-s read-only -a untrusted app-server --stdio",ReadProtocol,cancel),DateTimeOffset.UtcNow);
        }
        internal static object ReadProtocol(TextReader input,TextWriter output,CancellationToken cancel){
            int total=0;
            output.WriteLine("{\"id\":1,\"method\":\"initialize\",\"params\":{\"clientInfo\":{\"name\":\"hardware_pulse\",\"title\":\"Hardware Pulse\",\"version\":\"0.6.35\"}}}");output.Flush();
            Response(input,1,ref total,cancel);
            output.WriteLine("{\"method\":\"initialized\",\"params\":{}}");
            output.WriteLine("{\"id\":2,\"method\":\"account/rateLimits/read\",\"params\":{}}");output.Flush();
            return Response(input,2,ref total,cancel);
        }
        static object Response(TextReader input,int id,ref int total,CancellationToken cancel){
            for(int messages=0;messages<64;messages++){
                var line=new StringBuilder();
                while(true){cancel.ThrowIfCancellationRequested();int value=input.Read();if(value<0)throw new QuotaFailure("Quota unavailable");if(++total>1048576)throw new QuotaFailure("Quota unavailable");if(value=='\n')break;line.Append((char)value);}
                object message=QuotaData.Parse(line.ToString());
                if(QuotaDecoder.Get(message,"method")!=null)continue;
                object responseId=QuotaDecoder.Get(message,"id");
                if(responseId==null||Convert.ToString(responseId,System.Globalization.CultureInfo.InvariantCulture)!=id.ToString())continue;
                if(QuotaDecoder.Get(message,"error")!=null)throw new QuotaFailure("Quota unavailable");
                object result=QuotaDecoder.Get(message,"result");if(result==null)throw new QuotaFailure("Quota unavailable");return result;
            }
            throw new QuotaFailure("Quota unavailable");
        }
    }
    // Both installed quota clients share the same deadline and owned-child cleanup.
    internal static class QuotaChildProcess {
        internal static object Read(string executable,string arguments,Func<TextReader,TextWriter,CancellationToken,object> read,CancellationToken cancel,bool requireSuccessfulExit=false){
            using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancel))
            using(var process=new Process()){
                deadline.CancelAfter(TimeSpan.FromSeconds(15));
                process.StartInfo=new ProcessStartInfo(executable,arguments){
                    UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,
                    WorkingDirectory=Path.GetDirectoryName(executable),RedirectStandardInput=true,
                    RedirectStandardOutput=true,RedirectStandardError=true,
                    StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
                cancel.ThrowIfCancellationRequested();
                if(!process.Start())throw new QuotaFailure("Quota unavailable");
                Action stop=delegate{try{if(!process.HasExited)process.Kill();}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}};
                using(deadline.Token.Register(()=>stop()))
                using(var reading=CancellationTokenSource.CreateLinkedTokenSource(deadline.Token))
                using(var stdout=new StreamReader(new QuotaPipeStream(process.StandardOutput.BaseStream,reading.Token),Encoding.UTF8))
                using(var stderr=new StreamReader(new QuotaPipeStream(process.StandardError.BaseStream,reading.Token),Encoding.UTF8)){
                    int diagnosticOverflow=0;
                    var errors=Task.Run(()=>{try{var buffer=new char[2048];int count,total=0;while((count=stderr.Read(buffer,0,buffer.Length))>0){total+=count;if(total>65536){Interlocked.Exchange(ref diagnosticOverflow,1);reading.Cancel();stop();break;}}}catch(OperationCanceledException){}catch(IOException){}catch(ObjectDisposedException){}});
                    object body;
                    try{
                        body=read(stdout,process.StandardInput,reading.Token);
                    }catch(OperationCanceledException){
                        cancel.ThrowIfCancellationRequested();throw new QuotaFailure("Quota unavailable");
                    }finally{
                        // A child can close its pipe before the writer flushes. Even if
                        // closing stdin fails, always finish owned-process cleanup.
                        try{process.StandardInput.Close();}
                        finally{
                            try{if(!process.WaitForExit(500)){stop();if(!process.WaitForExit(2000))throw new QuotaFailure("Quota unavailable");}}
                            finally{
                                // A descendant may retain stderr after the owned child exits.
                                // Cancel the pipe reader before joining; never abandon a worker.
                                reading.Cancel();
                                if(!errors.Wait(2000))throw new QuotaFailure("Quota unavailable");
                            }
                        }
                    }
                    cancel.ThrowIfCancellationRequested();
                    if(deadline.IsCancellationRequested||diagnosticOverflow!=0)throw new QuotaFailure("Quota unavailable");
                    if(requireSuccessfulExit&&process.ExitCode!=0)throw new QuotaFailure("Quota unavailable");
                    return body;
                }
            }
        }
    }
    // Sole reader of a redirected anonymous pipe. Probe bytes before ReadFile so
    // an idle writer cannot trap a synchronous StreamReader beyond cancellation.
    // The Process/PipeStream owns the handle; this wrapper never closes it.
    internal sealed class QuotaPipeStream:Stream {
        readonly SafeHandle handle;
        readonly CancellationToken cancel;
        readonly byte[] bytes=new byte[4096];
        [DllImport("kernel32.dll",SetLastError=true)]static extern bool PeekNamedPipe(SafeHandle pipe,IntPtr buffer,uint size,IntPtr read,out uint available,IntPtr remaining);
        [DllImport("kernel32.dll",SetLastError=true)]static extern bool ReadFile(SafeHandle file,byte[] buffer,uint count,out uint read,IntPtr overlapped);
        internal QuotaPipeStream(Stream source,CancellationToken cancel){
            var file=source as FileStream;var pipe=source as PipeStream;
            if(file!=null)handle=file.SafeFileHandle;
            else if(pipe!=null)handle=pipe.SafePipeHandle;
            else throw new ArgumentException("Expected an owned pipe stream","source");
            this.cancel=cancel;
        }
        public override int Read(byte[] buffer,int offset,int count){
            if(buffer==null)throw new ArgumentNullException("buffer");
            if(offset<0||count<0||offset>buffer.Length-count)throw new ArgumentOutOfRangeException();
            if(count==0)return 0;
            while(true){
                cancel.ThrowIfCancellationRequested();uint available;
                if(!PeekNamedPipe(handle,IntPtr.Zero,0,IntPtr.Zero,out available,IntPtr.Zero))return EndOrThrow();
                if(available>0){
                    uint received;uint wanted=(uint)Math.Min(Math.Min(count,bytes.Length),(long)available);
                    if(!ReadFile(handle,bytes,wanted,out received,IntPtr.Zero))return EndOrThrow();
                    Buffer.BlockCopy(bytes,0,buffer,offset,(int)received);return (int)received;
                }
                cancel.WaitHandle.WaitOne(25);
            }
        }
        int EndOrThrow(){int error=Marshal.GetLastWin32Error();cancel.ThrowIfCancellationRequested();if(error==109)return 0;throw new IOException("Quota pipe read failed",new System.ComponentModel.Win32Exception(error));}
        public override bool CanRead{get{return true;}}public override bool CanSeek{get{return false;}}public override bool CanWrite{get{return false;}}
        public override long Length{get{throw new NotSupportedException();}}public override long Position{get{throw new NotSupportedException();}set{throw new NotSupportedException();}}
        public override void Flush(){}public override long Seek(long offset,SeekOrigin origin){throw new NotSupportedException();}
        public override void SetLength(long value){throw new NotSupportedException();}public override void Write(byte[] buffer,int offset,int count){throw new NotSupportedException();}
    }
}
