using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

static class CiForegroundPreparation {
    internal const string AppId="Microsoft.Windows.CloudExperienceHost_cw5n1h2txyewy!App";
    internal sealed record Target(nint Window,uint Pid,long Created,string Process,string Class,string Package,string Application,bool Foreground,bool FullDesktop,bool Visible,bool Foreign);
    internal readonly record struct Delivery(bool Sent,int Error);
    internal static bool Authorized(Func<string,string?> env,bool windows,bool arm64)=>windows&&arm64
        &&env("GITHUB_ACTIONS")=="true"&&env("PULSE_CI_HOSTED_RUNNER")=="github-hosted"
        &&env("RUNNER_OS")=="Windows"&&env("RUNNER_ARCH")=="ARM64"
        &&env("GITHUB_REPOSITORY")=="medking82/hardware-pulse"&&env("GITHUB_EVENT_NAME")=="pull_request"
        &&env("GITHUB_HEAD_REF")=="codex/arm-native-session-20261022"&&env("PULSE_CI_CXH_CLOSE_APPROVED")=="1";
    static bool Matches(Target target)=>target.Window>0&&target.Window!=(nint)0xffff&&target.Pid>0&&target.Created>0
        &&target.Process.Equals("WWAHost",StringComparison.OrdinalIgnoreCase)&&target.Class=="Windows.UI.Core.CoreWindow"
        &&target.Package.StartsWith("Microsoft.Windows.CloudExperienceHost_",StringComparison.Ordinal)
        &&target.Package.EndsWith("_cw5n1h2txyewy",StringComparison.Ordinal)&&target.Application==AppId
        &&target.Foreground&&target.FullDesktop&&target.Visible&&target.Foreign;
    internal static int Run(bool authorized,Func<Target?> inspect,Func<nint,uint,uint,Delivery> send,Func<Target,int,bool> waitGone) {
        if(!authorized){Console.WriteLine("CI_CXH_CLOSE_REFUSED scope=not-approved-hosted-arm-candidate");return 2;}
        var target=inspect();
        if(target==null||!Matches(target)){Console.WriteLine("CI_CXH_CLOSE_SKIPPED target=not-exact-fullscreen-cloudexperiencehost");return 0;}
        // Re-read the live HWND owner, package, creation time and geometry immediately before sending.
        if(inspect()!=target){Console.WriteLine("CI_CXH_CLOSE_REFUSED target=changed-before-send");return 1;}
        Console.WriteLine($"CI_CXH_CLOSE_REQUEST hwnd={target.Window} pid={target.Pid} appId={target.Application} message=WM_CLOSE timeoutMs=2000 attempt=1");Console.Out.Flush();
        var delivery=send(target.Window,0x0010,2000); // Exactly one normal close; no kill, input or fallback.
        if(!delivery.Sent){Console.WriteLine($"CI_CXH_CLOSE_FAILED error={delivery.Error} attempts=1");return 1;}
        if(!waitGone(target,3000)){Console.WriteLine("CI_CXH_CLOSE_FAILED window-still-present=True attempts=1");return 1;}
        Console.WriteLine("CI_CXH_CLOSE_PASS original-window-gone=True attempts=1");return 0;
    }
    public static int Native()=>Run(Authorized(Environment.GetEnvironmentVariable,OperatingSystem.IsWindows(),RuntimeInformation.ProcessArchitecture==Architecture.Arm64),Inspect,Send,WaitGone);
    static Target? Inspect() {
        nint window=GetForegroundWindow();GetWindowThreadProcessId(window,out uint pid);
        if(pid==0)return null;
        try {
            using var process=Process.GetProcessById((int)pid);
            if(!process.ProcessName.Equals("WWAHost",StringComparison.OrdinalIgnoreCase))return null;
            using var handle=WindowsForegroundDiagnostics.OpenProcess(0x1000,false,pid);
            if(handle.IsInvalid||!WindowsForegroundDiagnostics.GetProcessTimes(handle,out long created,out _,out _,out _))return null;
            var package=WindowsForegroundDiagnostics.ReadId(handle,false);var app=WindowsForegroundDiagnostics.ReadId(handle,true);
            var name=new StringBuilder(256);GetClassNameW(window,name,name.Capacity);
            int left=GetSystemMetrics(76),top=GetSystemMetrics(77),width=GetSystemMetrics(78),height=GetSystemMetrics(79);
            bool full=width>0&&height>0&&GetWindowRect(window,out var rect)&&rect.Left==left&&rect.Top==top&&rect.Right==left+width&&rect.Bottom==top+height;
            return new(window,pid,created,process.ProcessName,name.ToString(),package.Code==0?package.Text:"",app.Code==0?app.Text:"",GetForegroundWindow()==window,full,IsWindowVisible(window)&&!IsIconic(window),pid!=(uint)Environment.ProcessId);
        } catch(Exception e) when(e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) {
            Console.WriteLine($"CI_CXH_CLOSE_QUERY_UNAVAILABLE reason={e.GetType().Name}");return null;
        }
    }
    static Delivery Send(nint window,uint message,uint milliseconds) {
        Marshal.SetLastPInvokeError(0);
        bool sent=SendMessageTimeoutW(window,message,0,0,3,milliseconds,out _)!=0; // BLOCK | ABORTIFHUNG; enforce timeout.
        return new(sent,Marshal.GetLastWin32Error());
    }
    static bool WaitGone(Target target,int milliseconds) {
        var clock=Stopwatch.StartNew();
        while(clock.ElapsedMilliseconds<milliseconds) {
            if(!IsWindow(target.Window)||GetWindowThreadProcessId(target.Window,out uint pid)==0||pid!=target.Pid)return true;
            Thread.Sleep(25);
        }
        return false;
    }
    [StructLayout(LayoutKind.Sequential)] struct Rect {public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")] static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint window,out uint process);
    [DllImport("user32.dll")] static extern bool GetWindowRect(nint window,out Rect rect);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,ExactSpelling=true)] static extern int GetClassNameW(nint window,StringBuilder value,int capacity);
    [DllImport("user32.dll",SetLastError=true,ExactSpelling=true)] static extern nint SendMessageTimeoutW(nint window,uint message,nuint wParam,nint lParam,uint flags,uint timeout,out nuint result);
}
