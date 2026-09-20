using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Threading;
using HardwarePulse;
using HardwarePulse.Desktop;

static class DesktopLayerTests {
    [DllImport("user32.dll")] static extern nint GetTopWindow(nint parent);
    [DllImport("user32.dll")] static extern nint GetWindow(nint window,uint command);
    [DllImport("user32.dll")] static extern nint GetDesktopWindow();
    [DllImport("user32.dll")] static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool ShowWindow(nint window,int command);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern nint GetStyle(nint window,int index);
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static bool Above(nint first,nint second){for(nint h=GetTopWindow(0);h!=0;h=GetWindow(h,2)){if(h==first)return true;if(h==second)return false;}throw new Exception("Fixture absent from z-order");}
    static void Pump(){using var slice=new CancellationTokenSource(250);Dispatcher.UIThread.MainLoop(slice.Token);}
    public static void Native() {
        if(!OperatingSystem.IsWindows())return;
        bool rejected=false;
        try{using var invalid=new WindowsDesktopLayer(GetDesktopWindow(),a=>a(),()=>{});}catch(InvalidOperationException){rejected=true;}
        Check(rejected,"Foreign window must be rejected before placement");
        var desktop=new FloatingMonitorWindow(new UiLanguage("en"));
        var cover=new Window{Width=400,Height=240,ShowInTaskbar=false};
        try {
            desktop.Show();desktop.Present(new("20%","4 GiB","—","—",true,true));Pump();
            Check(desktop.SetLocked(true),"Native Desktop could not lock");
            cover.Show();cover.Activate();Pump();
            nint hwnd=desktop.TryGetPlatformHandle()!.Handle,other=cover.TryGetPlatformHandle()!.Handle;
            var position=desktop.Position;var size=desktop.ClientSize;
            Check(Above(other,hwnd)&&(GetStyle(hwnd,-20).ToInt64()&8)==0,"Locked Desktop must stay below an ordinary app");
            Check(GetForegroundWindow()==other,"Desktop placement stole activation");
            ShowWindow(hwnd,0);Pump();
            desktop.Present(new("21%","4 GiB","—","—",true,true));Pump();
            Check(IsWindowVisible(hwnd)&&Above(other,hwnd),"Snapshot refresh did not recover native-hidden Desktop below apps");
            desktop.ApplyPreferences(new PreviewSettings{DesktopTopmost=true});Pump();
            Check(Above(hwnd,other)&&(GetStyle(hwnd,-20).ToInt64()&8)!=0,"Always on Top did not enter topmost band");
            desktop.ApplyPreferences(new PreviewSettings{DesktopTopmost=false});Pump();
            Check(Above(other,hwnd)&&(GetStyle(hwnd,-20).ToInt64()&8)==0,"Turning Always on Top off did not restore Desktop layer");
            Check(desktop.Position==position&&desktop.ClientSize==size,"Layer changes moved or resized Desktop");
            Check(desktop.SetLocked(false),"Desktop editor could not unlock");desktop.Activate();Pump();
            Check(GetForegroundWindow()==hwnd&&Above(hwnd,other),"Unlocked editor must remain interactive above ordinary windows");
            desktop.Close();cover.Activate();Pump(); // queued foreground callbacks after disposal are harmless
        }finally{desktop.Close();cover.Close();}
        Console.WriteLine("PASS Windows Desktop layer: owned-window boundary, below apps, no activation, topmost restore, stable geometry, editor and disposal");
    }
}
