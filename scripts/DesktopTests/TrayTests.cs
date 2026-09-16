using Avalonia.Controls;
using Avalonia.Threading;
using HardwarePulse.Desktop;

static class TrayTests {
    static void Until(Func<bool> done,string failure) {
        var end=DateTime.UtcNow.AddSeconds(5);
        while(!done()&&DateTime.UtcNow<end){using var slice=new CancellationTokenSource(TimeSpan.FromMilliseconds(20));Dispatcher.UIThread.MainLoop(slice.Token);}
        if(!done())throw new Exception(failure);
    }
    public static void Run(bool native=false) {
        var window=new Window();window.Show();
        if(native){window.Activate();Until(()=>window.IsActive,"Native Tray fixture did not activate");}
        using var tray=new DesktopTray(window);
        var open=(NativeMenuItem)tray.Menu.Items[0];
        var quit=(NativeMenuItem)tray.Menu.Items[1];
        window.WindowState=WindowState.Minimized;
        Until(()=>window.WindowState==WindowState.Minimized,"Native minimize request was not acknowledged");
        open.Command!.Execute(null);
        Until(()=>window.WindowState==WindowState.Normal&&window.IsVisible,"Tray restore failed");
        window.WindowState=WindowState.Maximized;
        Until(()=>window.WindowState==WindowState.Maximized,"Native maximize request was not acknowledged");
        Console.WriteLine("TRAY_STATE before Open: "+window.WindowState);
        open.Command.Execute(null);
        Console.WriteLine("TRAY_STATE after Open: "+window.WindowState);
        if(window.WindowState!=WindowState.Maximized)throw new Exception("Tray restore lost maximized state");
        bool closed=false;window.Closed+=(_,_)=>closed=true;
        quit.Command!.Execute(null);
        if(!closed||open.Command.CanExecute(null)||quit.Command.CanExecute(null))throw new Exception("Tray quit/lifetime failed");
        open.Command.Execute(null); // A stale menu callback cannot reopen a closed window.
        tray.Dispose();
        Console.WriteLine("PASS tray commands: restore existing window, preserve maximized state, quit and stale callback rejection");
    }
}
