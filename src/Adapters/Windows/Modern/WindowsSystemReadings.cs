using System.Globalization;
using System.Runtime.InteropServices;

namespace HardwarePulse;

public readonly record struct WindowsSystemSample(bool CpuValid,ulong Idle,ulong Kernel,ulong User,
    bool MemoryValid,ulong TotalBytes,ulong AvailableBytes);

// Serial polling, no elevation, driver, WMI, process launch or owned timer.
public sealed class WindowsSystemReadings {
    readonly Func<WindowsSystemSample> read;
    readonly string source=Guid.NewGuid().ToString("N");
    WindowsSystemSample? previous;
    long sequence;
    public WindowsSystemReadings() {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Windows system counters required");
        read=ReadNative;
    }
    public WindowsSystemReadings(Func<WindowsSystemSample> read)=>this.read=read??throw new ArgumentNullException(nameof(read));
    public Reading Read(DateTimeOffset now) {
        var result=new Reading {time=now,identity=source+":"+(++sequence).ToString(CultureInfo.InvariantCulture),
            available=new Dictionary<string,bool>{{"cpuLoad",false}}};
        try {
            var current=read();
            if(current.MemoryValid&&current.TotalBytes>0&&current.AvailableBytes<=current.TotalBytes) {
                double used=current.TotalBytes-current.AvailableBytes;
                result.usage["ram"]=new Usage {used=used/1073741824.0,total=current.TotalBytes/1073741824.0,
                    percent=100*used/current.TotalBytes,label="Physical memory in use"};
            }
            if(current.CpuValid&&previous is { } before&&current.Idle>=before.Idle&&current.Kernel>=before.Kernel&&current.User>=before.User) {
                double idle=current.Idle-before.Idle;
                double total=(double)(current.Kernel-before.Kernel)+(current.User-before.User);
                // Kernel includes idle. Invalid/reset/zero intervals are unavailable, never synthetic zero.
                if(total>0&&idle<=total) {
                    result.values["cpuLoad"]=100*(total-idle)/total;
                    result.available["cpuLoad"]=true;
                }
            }
            previous=current.CpuValid?current:null;
            if(result.usage.Count>0||result.values.Count>0)result.state="LIVE";
        }catch(Exception e) when(e is IOException or DllNotFoundException or EntryPointNotFoundException) {
            previous=null;result.error="Windows system counters unavailable";
        }
        return result;
    }
    [StructLayout(LayoutKind.Sequential)] struct FileTime {public uint Low,High;public readonly ulong Ticks=>((ulong)High<<32)|Low;}
    [StructLayout(LayoutKind.Sequential)] struct MemoryStatus {
        public uint Length,Load;
        public ulong Total,Available,PageTotal,PageAvailable,VirtualTotal,VirtualAvailable,ExtendedAvailable;
    }
    [DllImport("kernel32.dll")][return:MarshalAs(UnmanagedType.Bool)]
    static extern bool GetSystemTimes(out FileTime idle,out FileTime kernel,out FileTime user);
    [DllImport("kernel32.dll")][return:MarshalAs(UnmanagedType.Bool)]
    static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    [DllImport("kernel32.dll")] static extern ushort GetActiveProcessorGroupCount();
    static WindowsSystemSample ReadNative() {
        var memory=new MemoryStatus {Length=(uint)Marshal.SizeOf<MemoryStatus>()};
        bool ram=GlobalMemoryStatusEx(ref memory);
        FileTime idle=default,kernel=default,user=default;
        // GetSystemTimes is only whole-machine for a single processor group.
        bool cpu=GetActiveProcessorGroupCount()==1&&GetSystemTimes(out idle,out kernel,out user);
        return new(cpu,idle.Ticks,kernel.Ticks,user.Ticks,ram,memory.Total,memory.Available);
    }
}
