using Avalonia.Controls;
using HardwarePulse.Desktop;

static class TrayTests {
    public static void Run() {
        var window=new Window();window.Show();
        using var tray=new DesktopTray(window);
        var open=(NativeMenuItem)tray.Menu.Items[0];
        var quit=(NativeMenuItem)tray.Menu.Items[1];
        window.WindowState=WindowState.Minimized;
        open.Command!.Execute(null);
        if(window.WindowState!=WindowState.Normal||!window.IsVisible)throw new Exception("Tray restore failed");
        window.WindowState=WindowState.Maximized;
        open.Command.Execute(null);
        if(window.WindowState!=WindowState.Maximized)throw new Exception("Tray restore lost maximized state");
        bool closed=false;window.Closed+=(_,_)=>closed=true;
        quit.Command!.Execute(null);
        if(!closed||open.Command.CanExecute(null)||quit.Command.CanExecute(null))throw new Exception("Tray quit/lifetime failed");
        open.Command.Execute(null); // A stale menu callback cannot reopen a closed window.
        tray.Dispose();
        Console.WriteLine("PASS tray commands: restore existing window, preserve maximized state, quit and stale callback rejection");
    }
}
