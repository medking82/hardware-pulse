using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class LockedLayoutTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Pump(){var end=DateTime.UtcNow.AddMilliseconds(500);while(DateTime.UtcNow<end){using var slice=new CancellationTokenSource(TimeSpan.FromMilliseconds(20));Dispatcher.UIThread.MainLoop(slice.Token);}}
    public static void Fps(string? output=null) {
        var saved=new PreviewSettings{FloatingWidth=360};var language=new UiLanguage("en");
        var window=new FloatingMonitorWindow(language,saved);window.Show();
        try {
            window.PresentFps(new(true,"Live","144","128","60","—"));
            var row=window.GetVisualDescendants().OfType<DesktopFpsRow>().Single();
            foreach(double size in new[]{10d,24,32}) {
                window.FontSize=size;window.ApplyTextAppearance();Dispatcher.UIThread.RunJobs();
                double height=row.Bounds.Height;
                window.PresentFps(new(true,"Live","99999","99999","99999","99999"));Dispatcher.UIThread.RunJobs();
                Check(row.Bounds.Height==height,"FPS digit count changes row height");
                var boxes=row.GetVisualDescendants().OfType<Viewbox>().ToArray();
                Check(boxes.Length==4&&boxes.All(x=>x.Bounds.Right<=row.Bounds.Width+1),"Compact FPS values overflow");
                Check(boxes.All(x=>Math.Abs(x.Bounds.Y-boxes[0].Bounds.Y)<1),"FPS numeric badges wrap to another line");
                window.PresentFps(new(true,"Live","144","128","60","—"));Dispatcher.UIThread.RunJobs();
                if(output!=null){using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(output,$"desktop-fps-{size}.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
            }
            Check(row.GetVisualDescendants().OfType<TextBlock>().Single(x=>x.Name=="FpsMetric3").Text=="—","Unknown low became zero");
            window.PresentFps(new(false,"FPS capture stopped"));Check(!window.GetVisualDescendants().OfType<DesktopFpsRow>().Any(),"Disabled FPS row remains");
        }finally{window.Close();}
        Console.WriteLine("PASS compact FPS: four numeric cells, narrow bounds, stable height, large fonts and unknown low");
    }
    public static void Native() {
        if(!OperatingSystem.IsWindows())return;
        var settings=new PreviewSettings{FloatingHeight=240,FloatingWidth=440,DesktopColumns=1};
        var window=new FloatingMonitorWindow(new UiLanguage("en"),settings);window.Show();
        try {
            var snapshot=new MonitorSnapshot("20%","4 GiB","1 KiB/s","2 KiB/s",true,true){WindowsHardware=Enumerable.Range(0,10).Select(i=>new HardwareSensorSnapshot("fan"+i,"Fixture fan "+i,"1200 RPM")).ToArray(),WindowsHardwareSupported=true};
            window.Present(snapshot);Pump();Check(window.SetLocked(true),"Native lock unavailable");Pump();
            var scroll=window.GetVisualDescendants().OfType<ScrollViewer>().Single();
            Check(scroll.Extent.Height<=scroll.Viewport.Height+1,"Locked Desktop leaves avoidable overflow at short saved height");
            double height=window.Height;window.Present(snapshot with {WindowsHardware=snapshot.WindowsHardware.Select(x=>x with {Value="0 RPM"}).ToArray()});Pump();
            Check(window.Height==height,"Live values cause locked height jitter");
            var screen=window.Screens.ScreenFromWindow(window)!;double total=(window.FrameSize?.Height??window.Height)*screen.Scaling;
            Check(window.Position.Y+total<=screen.WorkingArea.Bottom+2,"Expanded Desktop exceeds work area");
            Check(window.SetLocked(false),"Expanded Desktop cannot unlock");
            settings.DesktopColumns=0;window.ApplyTextAppearance();window.Width=440;window.Height=240;
            var panel=window.GetVisualDescendants().OfType<AdaptiveReadingsPanel>().Single();
            double rowStride=panel.Children.Single(x=>Equals(x.Tag,"hardware/fan0")).DesiredSize.Height+panel.Spacing;
            // One screen of measured rows plus the four basic metrics requires
            // widening, but can fit in two columns even on the 1024x728 CI desktop.
            int count=(int)Math.Ceiling(screen.WorkingArea.Height/screen.Scaling/rowStride);
            MonitorSnapshot WithRows(int number)=>snapshot with {WindowsHardware=Enumerable.Range(0,number).Select(i=>new HardwareSensorSnapshot("fan"+i,"Fixture fan "+i,"1200 RPM")).ToArray()};
            window.Present(WithRows(count));Pump();
            Check(scroll.Extent.Height>screen.WorkingArea.Height/screen.Scaling,"Auto-column fixture must exceed a full-height single column");
            window.SetLocked(true);Pump();
            Console.WriteLine($"LOCKED_AUTO screen={screen.WorkingArea} scale={screen.Scaling} font={window.FontSize} width={window.Width} height={window.Height} client={window.ClientSize} frame={window.FrameSize} columns={panel.Columns} minColumn={panel.MinimumColumnWidth} rows={panel.Children.Count} panel={panel.Bounds} extent={scroll.Extent} viewport={scroll.Viewport}");
            if(screen.WorkingArea.Width/screen.Scaling>=820) {
                Check(window.Width>440,"Auto columns did not widen to recover overflow");
                Check(scroll.Extent.Height<=scroll.Viewport.Height+1,"Auto columns leave avoidable locked overflow");
            }
            Check((window.FrameSize?.Width??window.Width)*screen.Scaling<=screen.WorkingArea.Width+2,"Auto fit exceeds work area width");
            Check(window.SetLocked(false),"Auto-fit window cannot unlock");
            window.Present(WithRows(count*4));Pump();window.SetLocked(true);Pump();
            Check(scroll.Extent.Height>scroll.Viewport.Height+1,"Over-capacity fixture unexpectedly fits");
            Check((window.FrameSize?.Width??window.Width)*screen.Scaling<=screen.WorkingArea.Width+2&&(window.FrameSize?.Height??window.Height)*screen.Scaling<=screen.WorkingArea.Height+2,"Over-capacity layout exceeds work area");
            Check(panel.Children.Count==count*4+4&&window.FontSize==15,"Over-capacity layout discarded readings or reduced font size");
            Check(window.SetLocked(false),"Over-capacity window cannot unlock for scrolling");
            scroll.Offset=new Avalonia.Vector(0,scroll.Extent.Height);Pump();
            Check(scroll.Offset.Y>0,"Unlocked over-capacity readings cannot be scrolled");
        }finally{window.SetLocked(false);window.Close();}
        Console.WriteLine("PASS locked Desktop: short-height recovery, work-area bounds, no sample shrink and unlock");
    }
}
