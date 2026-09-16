using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Threading;
using HardwarePulse.Desktop;

static class WindowsLayerTests {
    [DllImport("user32.dll")] static extern nint GetWindow(nint window,uint command);
    [DllImport("user32.dll")] static extern nint GetWindowLongPtrW(nint window,int index);
    [DllImport("user32.dll")] static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] static extern nint GetParent(nint window);
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
    static void Pump(){using var stop=new CancellationTokenSource(150);Dispatcher.UIThread.MainLoop(stop.Token);}
    public static void Native() {
        if(!OperatingSystem.IsWindows())return;
        nint host=WindowsDesktopLayer.FindDesktopHost();Check(host!=0,"Desktop layer acceptance requires a running Explorer icon host");
        var ordinary=new Window{Width=360,Height=240};
        var floating=new FloatingMonitorWindow(new UiLanguage("en"));
        ordinary.Show();floating.Show();Pump();
        var hwnd=floating.TryGetPlatformHandle()!.Handle;
        var sample=new MonitorSnapshot("20%","4 GiB","—","—",true,true);
        try {
            nint parent=GetParent(hwnd);var position=floating.Position;
            Check(floating.SetLocked(true),"Could not lock Desktop");
            ordinary.Activate();Pump();floating.Present(sample);
            Check(floating.DesktopLayerAvailable,"Locked layer did not attach");
            Check(GetWindow(host,3)==hwnd,"Locked readout is not immediately above desktop icons");
            Check(GetForegroundWindow()!=hwnd,"Desktop placement stole activation");
            Check(GetParent(hwnd)==parent&&floating.Position==position,"Layer changed parent or position");
            Check((GetWindowLongPtrW(hwnd,-20).ToInt64()&0x08000020)==0x08000020,"Layer lost locked input styles");
            floating.Topmost=true;Pump();floating.Present(sample);
            Check((GetWindowLongPtrW(hwnd,-20).ToInt64()&8)!=0,"Always on top lost native topmost state");
            floating.Topmost=false;Pump();floating.Present(sample);
            Check((GetWindowLongPtrW(hwnd,-20).ToInt64()&8)==0&&GetWindow(host,3)==hwnd,"Turning off topmost did not restore desktop placement");
            floating.Hide();ordinary.Activate();Pump();floating.Present(sample);
            Check(!floating.IsVisible&&!floating.DesktopLayerAvailable,"Event/poll resurrected hidden Desktop");
            floating.Show();Pump();floating.Present(sample);
            Check(GetWindow(host,3)==hwnd,"Show did not restore locked desktop placement");
            Check(floating.SetLocked(false),"Could not unlock Desktop");floating.Activate();Pump();floating.Present(sample);
            Check(!floating.DesktopLayerAvailable&&(GetWindowLongPtrW(hwnd,-20).ToInt64()&0x08000020)==0,"Unlock did not restore editable input");
            Check(GetWindow(host,3)!=hwnd,"Unlocked editor was forced behind ordinary windows");
            floating.Close();ordinary.Activate();Pump();
            Check(!floating.DesktopLayerAvailable,"Closed layer retained attachment");
        }finally{floating.Close();ordinary.Close();}
        Console.WriteLine("PASS Windows Desktop layer: native Z-order, no reparent/activation, topmost, input, hidden restore and disposal");
    }
}
