using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

static class WindowsForegroundDiagnostics {
    const uint QueryLimitedInformation=0x1000;
    public static void SelfTest() {
        if(!OperatingSystem.IsWindows())return;
        using var self=OpenProcess(QueryLimitedInformation,false,(uint)Environment.ProcessId);
        if(self.IsInvalid||!GetProcessTimes(self,out _,out _,out _,out _))throw new Exception("Own-process limited identity query failed");
        foreach(var identity in new[]{ReadId(self,false),ReadId(self,true)})
            if(identity.Code is not (0 or 15700 or 15703))throw new Exception($"Own-process identity API failed: {identity.Code}");
        Console.WriteLine("PASS limited process identity query: creation time and package/AUMID APIs; target=self");
    }
    public static void Report(string stage,nint window=0) {
        if(!OperatingSystem.IsWindows())return;
        if(window==0)window=GetForegroundWindow();
        GetWindowThreadProcessId(window,out uint pid);
        try {
            if(pid==0){Console.WriteLine($"WINDOWS_TARGET_IDENTITY stage={stage} noTarget=True");return;}
            using var target=Process.GetProcessById((int)pid);
            if(!target.ProcessName.Equals("WWAHost",StringComparison.OrdinalIgnoreCase)) {
                Console.WriteLine($"WINDOWS_TARGET_IDENTITY stage={stage} wwaHost=False");return;
            }
            using var handle=OpenProcess(QueryLimitedInformation,false,pid);
            if(handle.IsInvalid){Console.WriteLine($"WINDOWS_TARGET_IDENTITY stage={stage} wwaHost=True queryError={Marshal.GetLastWin32Error()}");return;}
            using var self=OpenProcess(QueryLimitedInformation,false,(uint)Environment.ProcessId);
            var package=ReadId(handle,false);var application=ReadId(handle,true);
            Console.WriteLine($"WINDOWS_TARGET_IDENTITY stage={stage} hwnd={window} pid={pid} process=WWAHost targetStartedUtc={Started(handle)} probeStartedUtc={Started(self)} package={package.Text} appId={application.Text}");
        } catch(Exception e) when(e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) {
            Console.WriteLine($"WINDOWS_TARGET_IDENTITY stage={stage} unavailable={e.GetType().Name}");
        } finally {Console.Out.Flush();}
    }
    static (int Code,string Text) ReadId(SafeProcessHandle process,bool application) {
        var value=new StringBuilder(1024);uint length=(uint)value.Capacity;
        int code=application?GetApplicationUserModelId(process,ref length,value):GetPackageFullName(process,ref length,value);
        return (code,code==0?value.ToString():$"unavailable({code})");
    }
    static string Started(SafeProcessHandle process)=>!process.IsInvalid&&GetProcessTimes(process,out long created,out _,out _,out _)?DateTime.FromFileTimeUtc(created).ToString("O"):"unavailable";
    [DllImport("user32.dll")] static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint window,out uint process);
    [DllImport("kernel32.dll",SetLastError=true)] static extern SafeProcessHandle OpenProcess(uint access,bool inherit,uint process);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetProcessTimes(SafeProcessHandle process,out long creation,out long exit,out long kernel,out long user);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,ExactSpelling=true)] static extern int GetPackageFullName(SafeProcessHandle process,ref uint length,StringBuilder value);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,ExactSpelling=true)] static extern int GetApplicationUserModelId(SafeProcessHandle process,ref uint length,StringBuilder value);
}
