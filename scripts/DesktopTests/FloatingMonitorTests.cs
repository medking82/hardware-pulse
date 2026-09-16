using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Headless;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;
using HardwarePulse;

static class FloatingMonitorTests {
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
    public static void Run(string? output=null) {
        Check(FloatingMonitorWindow.ConstrainPosition(new(-4000,5000),new(-1920,40,1920,1040),440,420)==new Avalonia.PixelPoint(-1920,660),"Removed monitor/negative desktop recovery");
        Check(FloatingMonitorWindow.ConstrainPosition(new(100,100),new(0,0,300,200),440,420)==new Avalonia.PixelPoint(0,0),"Small work area recovery");
        var owner=new MonitorWindow(new MonitorSource(true),start:false);
        owner.Show();owner.Present(new("21.0%","4.0 / 16.0 GiB · 25.0%","1.0 KiB/s","2.0 KiB/s",true,true));
        using var tray=new DesktopTray(owner);
        var open=(NativeMenuItem)tray.Menu.Items[1];
        open.Command!.Execute(null);var floating=owner.FloatingMonitor!;
        try {
            owner.OpenFloatingMonitor();Check(ReferenceEquals(floating,owner.FloatingMonitor),"Repeated open created another floating window");
            floating.Hide();open.Command.Execute(null);Check(ReferenceEquals(floating,owner.FloatingMonitor)&&floating.IsVisible,"Tray did not restore same floating window");
            if(floating.CanLock) {
                Check(floating.SetLocked(true)&&floating.IsLocked,"Native floating lock failed");
                floating.Hide();
                open.Command.Execute(null);Check(!floating.IsLocked&&ReferenceEquals(floating,owner.FloatingMonitor),"Tray did not unlock existing window");
            }
            Check(floating.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="21.0%"),"Floating window lost existing snapshot");
            floating.SetBackgroundOpacity(0);Dispatcher.UIThread.RunJobs();
            var background=(Avalonia.Media.SolidColorBrush)floating.Background!;
            Check(background.Color.A==(floating.ActualTransparencyLevel==WindowTransparencyLevel.None?255:0)&&floating.Opacity==1,"Transparent background or solid fallback altered text opacity");
            if(floating.CanLock){Check(floating.SetLocked(true)&&floating.SetLocked(false),"Zero background opacity broke lock/unlock");}
            floating.SetBackgroundOpacity(100);Check(((Avalonia.Media.SolidColorBrush)floating.Background!).Color.A==255,"Solid background alpha");
            if(output!=null){floating.Width=360;Dispatcher.UIThread.RunJobs();using var frame=floating.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"floating-360.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
            var top=floating.GetVisualDescendants().OfType<CheckBox>().Single();
            top.IsChecked=true;Check(floating.Topmost,"Topmost not applied");top.IsChecked=false;Check(!floating.Topmost,"Topmost not reversible");
            var allQuota=new List<QuotaWindow>{new(){Label="5-hour",Remaining=72.5},new(){Label="Weekly",Remaining=54},new(){Label="Extra",Remaining=null}};
            floating.PresentQuota(new QuotaReading{Provider="Codex",Status="Live",Windows=[allQuota[0]],AllWindows=allQuota});
            Check(floating.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="54.0% left")&&floating.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="Extra"),"Floating quota must include all windows, not only selected ones");
            owner.Language.Select("zh-CN");
            Check(!floating.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="54.0% left"),"Floating quota did not follow language change");
            owner.Language.Select("en");
            var quota=owner.GetVisualDescendants().OfType<CodexQuotaPanel>().Single();quota.QuotaEnabled=true;
            var quotaDeadline=DateTime.UtcNow.AddSeconds(5);
            while(quota.CurrentReading?.Status!="Live"&&DateTime.UtcNow<quotaDeadline){using var slice=new CancellationTokenSource(TimeSpan.FromMilliseconds(20));Dispatcher.UIThread.MainLoop(slice.Token);}
            Check(quota.CurrentReading?.Status=="Live"&&floating.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="72.5% left"),"Existing quota session not projected into floating window");
            quota.QuotaEnabled=false;
            Check(!floating.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="72.5% left"),"Disabled quota remains visible in floating window");
            owner.Present(new("—","—","—","—",false,false));
            Check(!floating.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="21.0%"),"Floating window retained unavailable live data");
            owner.Language.Select("zh-CN");Check(floating.Title=="浮动监控窗口"&&open.Header=="打开浮动监控窗口","Floating/tray entry not localized");
            floating.Width=360;Dispatcher.UIThread.RunJobs();
            Console.WriteLine($"FLOATING_RESIZE requested=360 client={floating.ClientSize.Width} widest={floating.GetVisualDescendants().OfType<TextBlock>().Max(x=>x.Bounds.Width)}");
            // A real window manager acknowledges resize asynchronously, especially
            // after removing/restoring decorations. RunJobs alone is not an acknowledgement.
            var resized=DateTime.UtcNow.AddSeconds(3);
            while(Math.Abs(floating.ClientSize.Width-360)>1&&DateTime.UtcNow<resized){using var slice=new CancellationTokenSource(TimeSpan.FromMilliseconds(20));Dispatcher.UIThread.MainLoop(slice.Token);}
            Dispatcher.UIThread.RunJobs();
            Console.WriteLine($"FLOATING_RESIZE settled client={floating.ClientSize.Width} widest={floating.GetVisualDescendants().OfType<TextBlock>().Max(x=>x.Bounds.Width)}");
            Check(Math.Abs(floating.ClientSize.Width-360)<=1,"Floating native resize was not acknowledged");
            Check(floating.GetVisualDescendants().OfType<TextBlock>().All(x=>x.Bounds.Width<=360),"Floating text overflow");
            top.IsChecked=true;double savedWidth=floating.Width,savedHeight=floating.Height;
            floating.Close();Check(owner.FloatingMonitor==null&&owner.IsVisible,"Floating close ended Monitor");
            owner.OpenFloatingMonitor();floating=owner.FloatingMonitor!;
            Check(floating.Topmost&&floating.Width==savedWidth&&floating.Height==savedHeight,"Floating geometry/topmost lost on reopen");
            Check(floating.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked==true,"Restored topmost checkbox disagrees with window");
            owner.Close();Check(!floating.IsVisible&&owner.FloatingMonitor==null,"Owner close left floating window alive");
            Check(!open.Command.CanExecute(null),"Disposed tray still offers floating action");open.Command.Execute(null);
            owner.OpenFloatingMonitor();Check(owner.FloatingMonitor==null,"Closed owner reopened floating window");
        } finally {floating.Close();owner.Close();}
        Console.WriteLine("PASS floating monitor: shared snapshots, singleton, topmost, localization and lifetime");
    }
}
