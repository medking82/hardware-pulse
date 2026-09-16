using Avalonia.Controls;
using Avalonia.Threading;
using HardwarePulse.Desktop;

static class TrayTests {
    sealed class CloseFixture : Window {
        public bool RequestClose(WindowCloseReason reason,bool programmatic=false) {
            var args=(WindowClosingEventArgs)Activator.CreateInstance(typeof(WindowClosingEventArgs),
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new object[]{reason,programmatic},null)!;
            OnClosing(args);
            if(!args.Cancel)Close();
            return args.Cancel;
        }
    }
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
        var background=new CloseFixture();background.Show();
        using(var hiddenTray=new DesktopTray(background,closeToTray:true,trayAvailable:()=>true)) {
            bool ended=false;background.Closed+=(_,_)=>ended=true;
            if(!background.RequestClose(WindowCloseReason.WindowClosing)||background.IsVisible||ended)throw new Exception("User close must hide without ending lifetime");
            ((NativeMenuItem)hiddenTray.Menu.Items[0]).Command!.Execute(null);
            if(!background.IsVisible||ended)throw new Exception("Hidden window must reopen");
            background.RequestClose(WindowCloseReason.WindowClosing);
            ((NativeMenuItem)hiddenTray.Menu.Items[1]).Command!.Execute(null);
            if(!ended)throw new Exception("Quit must close a hidden window");
        }
        foreach(var reason in new[]{WindowCloseReason.ApplicationShutdown,WindowCloseReason.OSShutdown,WindowCloseReason.OwnerWindowClosing}) {
            var shutdown=new CloseFixture();shutdown.Show();
            using var shutdownTray=new DesktopTray(shutdown,closeToTray:true,trayAvailable:()=>true);
            if(shutdown.RequestClose(reason))throw new Exception("Shutdown must not be cancelled");
        }
        var unavailable=new CloseFixture();unavailable.Show();
        using(var noTray=new DesktopTray(unavailable,closeToTray:true,trayAvailable:()=>false))if(unavailable.RequestClose(WindowCloseReason.WindowClosing))throw new Exception("Unsupported tray must retain close behavior");
        Console.WriteLine("PASS close-to-tray: hide/reopen, hidden quit, shutdown and unavailable fallback");
        Console.WriteLine("PASS tray commands: restore existing window, preserve maximized state, quit and stale callback rejection");
    }
}
