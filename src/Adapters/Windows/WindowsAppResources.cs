using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;

namespace HardwarePulse {
    public sealed class AppResourceProcess {
        public int Pid;public long StartedUtcTicks,RamBytes;public long? GpuBytes;
        public string Name;public bool CanClose,IsForeground;
    }
    public sealed class AppResourceSnapshot {
        public AppResourceProcess[] Processes;public RamUsage Ram;public bool GpuAvailable;
    }
    public enum AppCloseResult {Requested,Unavailable,IdentityChanged,NotAllowed}

    // On-demand current-user observations. Never runs in the elevated Collector.
    public static class WindowsAppResources {
        static readonly Regex gpuInstance=new Regex(@"\Apid_([0-9]{1,10})_luid_0x[0-9a-fA-F]{1,8}_0x[0-9a-fA-F]{1,8}_phys_[0-9]{1,5}\z",RegexOptions.CultureInvariant);
        static readonly HashSet<string> windowsComponents=new HashSet<string>(new[]{
            "explorer","dwm","csrss","winlogon","wininit","smss","lsass","services","svchost","sihost",
            "ShellExperienceHost","StartMenuExperienceHost","SearchHost","SearchApp","SearchUI",
            "TextInputHost","ctfmon","InputApp","ApplicationFrameHost","RuntimeBroker","LockApp","UserOOBEBroker","conhost"
        },StringComparer.OrdinalIgnoreCase);
        [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
        [DllImport("advapi32.dll",SetLastError=true)]static extern bool OpenProcessToken(IntPtr process,uint access,out IntPtr token);
        [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
        [DllImport("user32.dll")]static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
        [DllImport("user32.dll")]static extern bool IsWindowEnabled(IntPtr window);
        [DllImport("pdh.dll",CharSet=CharSet.Unicode)]static extern uint PdhOpenQuery(string source,IntPtr user,out IntPtr query);
        [DllImport("pdh.dll",CharSet=CharSet.Unicode)]static extern uint PdhAddEnglishCounter(IntPtr query,string path,IntPtr user,out IntPtr counter);
        [DllImport("pdh.dll")]static extern uint PdhCollectQueryData(IntPtr query);
        [DllImport("pdh.dll",CharSet=CharSet.Unicode)]static extern uint PdhGetFormattedCounterArray(IntPtr counter,uint format,ref uint bytes,out uint count,IntPtr buffer);
        [DllImport("pdh.dll")]static extern uint PdhCloseQuery(IntPtr query);
        [StructLayout(LayoutKind.Explicit,Size=16)]struct CounterValue {
            [FieldOffset(0)]public uint Status;[FieldOffset(8)]public long Value;
        }
        [StructLayout(LayoutKind.Sequential)]struct CounterItem {public IntPtr Name;public CounterValue Counter;}

        static bool SameUser(Process process,string sid) {
            // Token inspection needs query access, never terminate/write access.
            IntPtr handle=OpenProcess(0x1000,false,process.Id),token=IntPtr.Zero;
            if(handle==IntPtr.Zero)return false;
            try{
                if(!OpenProcessToken(handle,8,out token))return false;
                using(var identity=new WindowsIdentity(token))return identity.User!=null&&identity.User.Value==sid;
            }finally{if(token!=IntPtr.Zero)CloseHandle(token);CloseHandle(handle);}
        }
        static int ShellPid(){uint pid;GetWindowThreadProcessId(GetShellWindow(),out pid);return (int)pid;}
        static bool Allowed(Process process,int own,int session,int shell,string sid) {
            return process.Id!=own&&process.Id!=shell&&process.SessionId==session&&session!=0&&SameUser(process,sid)
                &&!string.Equals(process.ProcessName,"HardwarePulse",StringComparison.OrdinalIgnoreCase);
        }
        public static bool TryGpuInstance(string instance,long value,out int pid) {
            pid=0;
            if(value<0||value>1099511627776L||string.IsNullOrEmpty(instance)||!instance.StartsWith("pid_",StringComparison.Ordinal))return false;
            var match=gpuInstance.Match(instance);
            return match.Success&&int.TryParse(match.Groups[1].Value,NumberStyles.None,CultureInfo.InvariantCulture,out pid)&&pid>0;
        }
        // Input is a current-user Read().Processes snapshot. High usage warrants review, not an unused/safe-to-close claim.
        public static AppResourceProcess[] ReviewCandidates(AppResourceProcess[] processes,bool gpu) {
            if(processes==null)return new AppResourceProcess[0];
            return processes.Where(p=>p!=null&&p.Pid>0&&p.StartedUtcTicks>0&&p.CanClose&&!p.IsForeground
                    &&!string.IsNullOrEmpty(p.Name)&&!windowsComponents.Contains(p.Name)
                    &&(gpu?p.GpuBytes.HasValue&&p.GpuBytes.Value>=134217728L:p.RamBytes>=536870912L))
                .OrderByDescending(p=>gpu?p.GpuBytes.Value:p.RamBytes)
                .ThenBy(p=>p.Name,StringComparer.OrdinalIgnoreCase).ThenBy(p=>p.Pid).Take(3).ToArray();
        }
        static Dictionary<int,long> ReadGpu(out bool available) {
            available=false;var result=new Dictionary<int,long>();IntPtr query=IntPtr.Zero,buffer=IntPtr.Zero;
            try{
                if(PdhOpenQuery(null,IntPtr.Zero,out query)!=0)return result;
                IntPtr counter;if(PdhAddEnglishCounter(query,@"\GPU Process Memory(*)\Dedicated Usage",IntPtr.Zero,out counter)!=0||PdhCollectQueryData(query)!=0)return result;
                uint bytes=0,count;
                const uint moreData=0x800007d2;
                if(PdhGetFormattedCounterArray(counter,0x400,ref bytes,out count,IntPtr.Zero)!=moreData||bytes==0||bytes>4194304)return result;
                buffer=Marshal.AllocHGlobal((int)bytes);uint allocated=bytes;
                if(PdhGetFormattedCounterArray(counter,0x400,ref bytes,out count,buffer)!=0||bytes>allocated)return result;
                int size=Marshal.SizeOf(typeof(CounterItem));if(count>allocated/(uint)size)return result;
                var seen=new HashSet<string>(StringComparer.Ordinal);
                for(uint i=0;i<count;i++){
                    var item=(CounterItem)Marshal.PtrToStructure(IntPtr.Add(buffer,checked((int)i*size)),typeof(CounterItem));
                    if(item.Counter.Status>1)continue;
                    string name=Marshal.PtrToStringUni(item.Name);int pid;
                    if(!TryGpuInstance(name,item.Counter.Value,out pid)||!seen.Add(name))continue;
                    long sum;result.TryGetValue(pid,out sum);
                    if(sum<=1099511627776L-item.Counter.Value)result[pid]=sum+item.Counter.Value;
                }
                available=result.Count>0;
            }catch{/* Optional counters are unavailable on legacy Windows / unsupported drivers. */}
            finally{if(buffer!=IntPtr.Zero)Marshal.FreeHGlobal(buffer);if(query!=IntPtr.Zero)PdhCloseQuery(query);}
            return result;
        }
        public static AppResourceSnapshot Read() {
            bool gpuAvailable;var gpu=ReadGpu(out gpuAvailable);var rows=new List<AppResourceProcess>();
            using(var own=Process.GetCurrentProcess())using(var identity=WindowsIdentity.GetCurrent()){
                int session=own.SessionId,shell=ShellPid();uint foregroundPid;GetWindowThreadProcessId(GetForegroundWindow(),out foregroundPid);string sid=identity.User.Value;
                foreach(var process in Process.GetProcesses())using(process)try{
                    if(!Allowed(process,own.Id,session,shell,sid)||process.HasExited)continue;
                    long memory=process.WorkingSet64;if(memory<0)continue;
                    IntPtr window=process.MainWindowHandle;uint windowPid;
                    long bytes;long? dedicated=gpu.TryGetValue(process.Id,out bytes)?(long?)bytes:null;
                    rows.Add(new AppResourceProcess {Pid=process.Id,StartedUtcTicks=process.StartTime.ToUniversalTime().Ticks,Name=process.ProcessName,
                        RamBytes=memory,GpuBytes=dedicated,IsForeground=(uint)process.Id==foregroundPid,
                        CanClose=window!=IntPtr.Zero&&GetWindowThreadProcessId(window,out windowPid)!=0&&windowPid==(uint)process.Id&&IsWindowEnabled(window)});
                }catch{/* Processes can disappear or deny access between observations. */}
            }
            return new AppResourceSnapshot {Processes=rows.OrderByDescending(p=>p.RamBytes).ToArray(),Ram=WindowsHardware.ReadMemory(),GpuAvailable=gpuAvailable};
        }
        public static AppCloseResult RequestClose(AppResourceProcess selected) {
            if(selected==null||selected.Pid<=0||selected.StartedUtcTicks<=0||!selected.CanClose)return AppCloseResult.NotAllowed;
            try{
                using(var process=Process.GetProcessById(selected.Pid))using(var own=Process.GetCurrentProcess())using(var identity=WindowsIdentity.GetCurrent()){
                    // Revalidate the process creation time; never trust a PID from an old snapshot.
                    if(process.HasExited)return AppCloseResult.Unavailable;
                    if(process.StartTime.ToUniversalTime().Ticks!=selected.StartedUtcTicks)return AppCloseResult.IdentityChanged;
                    if(!Allowed(process,own.Id,own.SessionId,ShellPid(),identity.User.Value))return AppCloseResult.NotAllowed;
                    IntPtr window=process.MainWindowHandle;uint pid;
                    if(window==IntPtr.Zero||GetWindowThreadProcessId(window,out pid)==0||pid!=(uint)selected.Pid||!IsWindowEnabled(window))return AppCloseResult.Unavailable;
                    return process.CloseMainWindow()?AppCloseResult.Requested:AppCloseResult.Unavailable;
                }
            }catch{return AppCloseResult.Unavailable;}
        }
    }
}
