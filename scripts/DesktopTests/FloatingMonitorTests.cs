using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Headless;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class FloatingMonitorTests {
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
    public static void Run(string? output=null) {
        var owner=new MonitorWindow(new MonitorSource(true),start:false);
        owner.Show();owner.Present(new("21.0%","4.0 / 16.0 GiB · 25.0%","1.0 KiB/s","2.0 KiB/s",true,true));
        using var tray=new DesktopTray(owner);
        var open=(NativeMenuItem)tray.Menu.Items[1];
        open.Command!.Execute(null);var floating=owner.FloatingMonitor!;
        try {
            owner.OpenFloatingMonitor();Check(ReferenceEquals(floating,owner.FloatingMonitor),"Repeated open created another floating window");
            floating.Hide();open.Command.Execute(null);Check(ReferenceEquals(floating,owner.FloatingMonitor)&&floating.IsVisible,"Tray did not restore same floating window");
            Check(floating.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="21.0%"),"Floating window lost existing snapshot");
            if(output!=null){floating.Width=360;Dispatcher.UIThread.RunJobs();using var frame=floating.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"floating-360.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
            var top=floating.GetVisualDescendants().OfType<CheckBox>().Single();
            top.IsChecked=true;Check(floating.Topmost,"Topmost not applied");top.IsChecked=false;Check(!floating.Topmost,"Topmost not reversible");
            owner.Present(new("—","—","—","—",false,false));
            Check(!floating.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="21.0%"),"Floating window retained unavailable live data");
            owner.Language.Select("zh-CN");Check(floating.Title=="浮动监控窗口"&&open.Header=="打开浮动监控窗口","Floating/tray entry not localized");
            floating.Width=360;Dispatcher.UIThread.RunJobs();
            Check(floating.GetVisualDescendants().OfType<TextBlock>().All(x=>x.Bounds.Width<=360),"Floating text overflow");
            floating.Close();Check(owner.FloatingMonitor==null&&owner.IsVisible,"Floating close ended Monitor");
            owner.OpenFloatingMonitor();floating=owner.FloatingMonitor!;
            owner.Close();Check(!floating.IsVisible&&owner.FloatingMonitor==null,"Owner close left floating window alive");
            Check(!open.Command.CanExecute(null),"Disposed tray still offers floating action");open.Command.Execute(null);
            owner.OpenFloatingMonitor();Check(owner.FloatingMonitor==null,"Closed owner reopened floating window");
        } finally {floating.Close();owner.Close();}
        Console.WriteLine("PASS floating monitor: shared snapshots, singleton, topmost, localization and lifetime");
    }
}
