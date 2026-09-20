using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class TitlebarTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Until(Func<bool> done) {
        var end=DateTime.UtcNow.AddSeconds(5);
        while(!done()&&DateTime.UtcNow<end){using var slice=new CancellationTokenSource(TimeSpan.FromMilliseconds(20));Dispatcher.UIThread.MainLoop(slice.Token);}
        Check(done(),"Titlebar native state transition timed out");
    }
    public static void Run() {
        var window=new MonitorWindow(new MonitorSource(true),start:false);window.Show();Dispatcher.UIThread.RunJobs();
        using var tray=new DesktopTray(window);
        try {
            Button Button(string name)=>window.GetVisualDescendants().OfType<Button>().Single(x=>x.Name==name);
            var grips=window.GetVisualDescendants().OfType<Border>().Where(x=>x.Name?.StartsWith("Resize")==true).ToArray();
            Check(window.WindowDecorations==WindowDecorations.None&&grips.Length==8&&grips.All(x=>x.IsVisible),"Shared titlebar has eight resize regions without duplicate OS chrome");
            window.CanResize=false;Check(grips.All(x=>!x.IsVisible),"Disabled resizing removes resize hit targets");window.CanResize=true;
            window.Language.Select("zh-CN");Check(Avalonia.Automation.AutomationProperties.GetName(Button("Minimize"))=="最小化","Titlebar accessible labels localize");
            Button("Minimize").RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));Until(()=>window.WindowState==WindowState.Minimized);
            Check(grips.All(x=>!x.IsVisible),"Minimized window disables resize regions");
            ((NativeMenuItem)tray.Menu.Items[0]).Command!.Execute(null);Until(()=>window.WindowState==WindowState.Normal&&window.IsVisible);
            Check(grips.All(x=>x.IsVisible),"Tray restore re-enables resize regions");
            window.OpenFloatingMonitor();bool closed=false;window.Closed+=(_,_)=>closed=true;
            Button("Close").RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Check(closed&&window.FloatingMonitor==null,"Close titlebar action closes owner and floating view");
            Check(!((NativeMenuItem)tray.Menu.Items[0]).Command!.CanExecute(null),"Close invalidates tray restore");
        } finally {window.Close();}
        Console.WriteLine("PASS shared titlebar: resize availability, localized labels, minimize/restore and close lifetime");
    }
}
