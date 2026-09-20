using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Avalonia.Media;
using HardwarePulse.Desktop;

static class FloatingMonitorTests {
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern nint GetWindowLongPtrW(nint window,int index);
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
    public static void Run(string? output=null) {
        var owner=new MonitorWindow(new MonitorSource(true),start:false);
        owner.Show();owner.Present(new("21.0%","4.0 / 16.0 GiB · 25.0%","1.0 KiB/s","2.0 KiB/s",true,true));
        using var tray=new DesktopTray(owner);
        var open=(NativeMenuItem)tray.Menu.Items[1];
        open.Command!.Execute(null);var floating=owner.FloatingMonitor!;
        try {
            owner.OpenFloatingMonitor();Check(ReferenceEquals(floating,owner.FloatingMonitor),"Repeated open created another floating window");
            var materialWindow=new FloatingMonitorWindow(owner.Language);
            materialWindow.Present(new("21.0%","4.0 / 16.0 GiB","1 KiB/s","2 KiB/s",true,true));materialWindow.Show();
            try {
            var material=materialWindow.GetVisualDescendants().OfType<Border>().Single(x=>x.Name=="DesktopSurface");
            var preferences=new PreviewSettings{DesktopBackgroundOpacity=30,DesktopOverlayOpacity=70,DesktopTextOpacity=40};
            materialWindow.ApplyPreferences(preferences);Dispatcher.UIThread.RunJobs();
            byte Expected(double percent)=>materialWindow.ActualTransparencyLevel==WindowTransparencyLevel.None?(byte)255:(byte)Math.Round(percent*255/100);
            Check(((ISolidColorBrush)material.Background!).Color.A==Expected(30),"Desktop background alpha or opaque fallback");
            var metrics=(Grid)materialWindow.GetVisualDescendants().OfType<Grid>().Single(x=>x.Name=="DesktopMetricCPU").Parent!;
            Check(metrics.Opacity==.4&&materialWindow.Opacity==1,"Text opacity affects metrics only, not editor/window");
            Check(((ISolidColorBrush)materialWindow.Foreground!).Color.ToString()=="#fff5f7fa","Original light Desktop foreground remains readable on dark backing");
            preferences.DesktopTopmost=true;materialWindow.ApplyPreferences(preferences);
            Check(((ISolidColorBrush)material.Background!).Color.A==Expected(70),"Topmost uses independent overlay opacity");
            foreach(double alpha in new[]{0d,100d}) {
                preferences.DesktopOverlayOpacity=alpha;materialWindow.ApplyPreferences(preferences);
                Check(((ISolidColorBrush)material.Background!).Color.A==Expected(alpha)&&metrics.Opacity==.4,"Background endpoints preserve metric opacity");
            }
            materialWindow.ApplyPreferences(new PreviewSettings());
            } finally {materialWindow.Close();}
            floating.Hide();open.Command.Execute(null);Check(ReferenceEquals(floating,owner.FloatingMonitor)&&floating.IsVisible,"Tray did not restore same floating window");
            if(floating.CanLock) {
                Check(floating.SetLocked(true)&&floating.IsLocked,"Native floating lock failed");
                if(OperatingSystem.IsWindows()&&floating.TryGetPlatformHandle()?.HandleDescriptor=="HWND")
                    Check((GetWindowLongPtrW(floating.TryGetPlatformHandle()!.Handle,-20).ToInt64()&0x08080020)==0x08080020,"Chrome changes preserve native pass-through and no-activate bits");
                Check(!floating.CanResize&&!floating.ShowInTaskbar&&floating.WindowDecorations==WindowDecorations.None,"Locked Desktop removes window chrome and resize");
                Check(!floating.GetVisualDescendants().OfType<StackPanel>().Single(x=>x.Name=="DesktopEditor").IsVisible,"Locked Desktop hides editor");
                var lockedPin=floating.GetVisualDescendants().OfType<CheckBox>().Single(x=>x.Name=="FloatingTopmost");
                lockedPin.IsChecked=true;Dispatcher.UIThread.RunJobs();
                if(OperatingSystem.IsWindows()&&floating.TryGetPlatformHandle()?.HandleDescriptor=="HWND")
                    Check((GetWindowLongPtrW(floating.TryGetPlatformHandle()!.Handle,-20).ToInt64()&0x08080020)==0x08080020,"Changing topmost while locked preserves native pass-through");
                Check(floating.IsLocked&&!floating.CanResize,"Topmost change does not unlock Desktop");
                lockedPin.IsChecked=false;
                floating.Hide();
                open.Command.Execute(null);Check(!floating.IsLocked&&ReferenceEquals(floating,owner.FloatingMonitor),"Tray did not unlock existing window");
                Check(floating.CanResize&&floating.ShowInTaskbar&&floating.GetVisualDescendants().OfType<StackPanel>().Single(x=>x.Name=="DesktopEditor").IsVisible,"Reopen restores editor and resize");
            }
            Check(floating.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="21.0%"),"Floating window lost existing snapshot");
            var hardware=new HardwarePulse.Reading{state="LIVE",gpuFanCount=2,values={{"cpu",55},{"cpuLoad",12},{"gpu",40},{"gpuLoad",20},{"gpuFan",600},{"gpuFan2",700},{"diskC",43},{"lanLink",1000000000},{"netDown",999999}}};
            hardware.available=hardware.values.Keys.ToDictionary(x=>x,_=>true);
            var sample=new MonitorSnapshot("21.0%","4.0 / 16.0 GiB · 25.0%","1.0 KiB/s","2.0 KiB/s",true,true){Hardware=hardware,HardwarePeaks=new Dictionary<string,double>{{"cpu",65},{"cpuLoad",22},{"gpu",50},{"gpuLoad",30}}};
            owner.Present(sample);Dispatcher.UIThread.RunJobs();
            bool Text(string text)=>floating.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.IsEffectivelyVisible&&x.Text==text);
            Check(Text("55.0 °C   12.0%")&&Text("40.0 °C   20.0%")&&Text("600 RPM")&&Text("700 RPM")&&Text("43.0 °C"),"Desktop receives grouped CPU/GPU, separate fans and drive readings from shared hardware snapshot");
            Check(Text("1.0 KiB/s"),"Desktop traffic uses selected-interface snapshot, not collector default");
            var gpu=floating.GetVisualDescendants().OfType<Grid>().Single(x=>x.Name=="DesktopMetricGPU");
            floating.Present(sample,true);Check(Text("65.0 °C   22.0%")&&Text(sample.Memory),"Desktop peak mode retains current memory");
            owner.Present(sample with {Hardware=new HardwarePulse.Reading{state="STALE",available=hardware.available}});
            Check(!Text("40.0 °C   20.0%")&&ReferenceEquals(gpu,floating.GetVisualDescendants().OfType<Grid>().Single(x=>x.Name=="DesktopMetricGPU")),"Stale hardware clears values while reusing capability rows");
            owner.Present(sample);Dispatcher.UIThread.RunJobs();
            if(output!=null){floating.Width=360;Dispatcher.UIThread.RunJobs();using var frame=floating.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"floating-360.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
            floating.ApplyPreferences(new PreviewSettings{DesktopFontSize=10,DesktopSpacing=24,DesktopColumns=3});
            floating.Width=1000;
            var layoutDeadline=DateTime.UtcNow.AddSeconds(3);
            while(floating.ClientSize.Width<990&&DateTime.UtcNow<layoutDeadline){using var slice=new CancellationTokenSource(TimeSpan.FromMilliseconds(20));Dispatcher.UIThread.MainLoop(slice.Token);}
            Dispatcher.UIThread.RunJobs();
            var cpu=floating.GetVisualDescendants().OfType<Grid>().Single(x=>x.Name=="DesktopMetricCPU");
            var metricGrid=(Grid)cpu.Parent!;
            Console.WriteLine($"DESKTOP_LAYOUT client={floating.ClientSize.Width} bounds={floating.Bounds.Width} grid={metricGrid.Bounds.Width} columns={metricGrid.ColumnDefinitions.Count} spacing={metricGrid.RowSpacing}");
            Check(metricGrid.ColumnDefinitions.Count==3&&metricGrid.RowSpacing==24,"Desktop uses requested columns and row spacing when space permits");
            Check(Grid.GetColumn(gpu)==1&&Grid.GetRow(gpu)==0,"Desktop metric order flows across columns");
            if(output!=null){using var frame=floating.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"desktop-three-columns.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
            floating.ApplyPreferences(new PreviewSettings());
            var top=floating.GetVisualDescendants().OfType<CheckBox>().Single();
            top.IsChecked=true;Check(floating.Topmost,"Topmost not applied");top.IsChecked=false;Check(!floating.Topmost,"Topmost not reversible");
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
            owner.Hide();
            floating.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="ReturnToApp").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Check(owner.FloatingMonitor==null&&owner.IsVisible&&!floating.IsVisible,"Return restores App and closes only Desktop");
            owner.OpenFloatingMonitor();floating=owner.FloatingMonitor!;
            owner.Close();Check(!floating.IsVisible&&owner.FloatingMonitor==null,"Owner close left floating window alive");
            Check(!open.Command.CanExecute(null),"Disposed tray still offers floating action");open.Command.Execute(null);
            owner.OpenFloatingMonitor();Check(owner.FloatingMonitor==null,"Closed owner reopened floating window");
        } finally {floating.Close();owner.Close();}
        Console.WriteLine("PASS floating monitor: shared snapshots, singleton, topmost, localization and lifetime");
    }
}
