using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class AdaptiveReadingsTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run(string? output=null) {
        var panel=new AdaptiveReadingsPanel();
        for(int i=0;i<7;i++)panel.Children.Add(new TextBlock{Text="Long sensor name · 系统风扇 27.6 / 61.4 GiB",TextWrapping=Avalonia.Media.TextWrapping.Wrap});
        void Layout(double width){panel.Measure(new Size(width,double.PositiveInfinity));panel.Arrange(new Rect(0,0,width,panel.DesiredSize.Height));}
        Layout(560);Check(panel.Columns==1,"Columns do not fit");
        Layout(575);Check(panel.Columns==1,"Breakpoint hysteresis missing");
        Layout(586);Check(panel.Columns==2,"Expansion threshold incorrect");
        Layout(575);Check(panel.Columns==2,"Width jitter collapsed columns");
        Layout(569);Check(panel.Columns==1,"Narrow columns overflow");
        panel.RequestedColumns=3;Layout(1000);Check(panel.Columns==3,"Explicit three columns failed");
        panel.Children[0].IsVisible=false;Layout(1000);Check(panel.Children[1].Bounds.X==0&&panel.Children[1].Bounds.Y==0,"Hidden item leaves a hole");
        foreach(var child in panel.Children.Where(x=>x.IsVisible))Check(child.Bounds.Right<=1000&&child.Bounds.Width>0,"Row exceeds bounds");
        panel.RequestedColumns=1;Layout(1000);Check(panel.Columns==1,"Explicit single column ignored");
        panel.RequestedColumns=3;Layout(280);Check(panel.Columns==1,"Explicit columns override safe minimum width");
        var settings=new PreviewSettings{DesktopColumns=3};
        var floating=new FloatingMonitorWindow(new UiLanguage("en"),settings);floating.Show();
        var readings=Enumerable.Range(0,9).Select(i=>new HardwareSensorSnapshot("fan"+i,"Long fan label · 系统进风风扇",i+" RPM")).ToArray();
        var snapshot=new MonitorSnapshot("24.0%","27.6 / 61.4 GiB · 45%","1.0 KiB/s","2.0 KiB/s",true,true){WindowsHardwareSupported=true,WindowsHardware=readings};
        floating.Present(snapshot);
        var hardware=floating.GetVisualDescendants().OfType<AdaptiveReadingsPanel>().Single(x=>x.Name=="DesktopReadings");
        var retained=hardware.Children.ToArray();
        foreach(int width in new[]{360,960,1400}) {
            floating.Width=width;Dispatcher.UIThread.RunJobs();
            Check(hardware.Columns==(width==360?1:width==960?2:3),"Desktop column selection failed");
            floating.Present(snapshot with {WindowsHardware=readings.Select(x=>x with {Value="9999 RPM"}).ToArray()});
            Dispatcher.UIThread.RunJobs();Check(hardware.Children.SequenceEqual(retained),"Live readings rebuild layout controls");
            foreach(var child in hardware.Children)Check(child.Bounds.Right<=hardware.Bounds.Width+1,"Desktop row overflow");
            foreach(var row in hardware.Children.Cast<Grid>()) {
                var label=(TextBlock)row.Children[0];var value=(TextBlock)row.Children[1];
                Check(label.Bounds.Width>0&&label.Bounds.Right<=value.Bounds.Left,"Label overlaps reading");
                if(width==1400){var natural=new TextBlock{Text=label.Text,FontSize=label.FontSize,FontFamily=label.FontFamily};natural.Measure(Size.Infinity);Check(label.Bounds.Width>=natural.DesiredSize.Width-1,"Wide row wastes available label width");}
            }
            if(output!=null){using var frame=floating.CaptureRenderedFrame();frame!.Save(Path.Combine(output,$"desktop-columns-{width}.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
        }
        settings.DesktopColumns=1;floating.ApplyTextAppearance();Dispatcher.UIThread.RunJobs();Check(hardware.Columns==1,"Desktop preference does not apply live");floating.Close();
        string directory=Directory.CreateTempSubdirectory("pulse-columns-").FullName;
        try{var store=new PreviewSettingsStore(Path.Combine(directory,"settings.json"));settings.CardColumns=2;Check(store.Save(settings),"Layout preference save failed");var loaded=store.Load();Check(loaded.CardColumns==2&&loaded.DesktopColumns==1,"Layout preference roundtrip failed");}finally{Directory.Delete(directory,true);}
        Console.WriteLine("PASS adaptive columns: Core hysteresis, requested columns, safe narrowing, hidden cells, retained live rows and persistence");
    }
}
