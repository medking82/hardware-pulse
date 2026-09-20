using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse;
using HardwarePulse.Desktop;

static class DeviceCardsTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run(string? output) {
        var reading=new Reading{state="LIVE",identity="fixture",values={{"cpu",59},{"cpuLoad",17},{"gpu",49},{"gpuLoad",20},{"gpuVolt",.85},{"ramA",41},{"ramB",42},{"diskC",43},{"system",38},{"bottom",900},{"top",1000}},
            names={{"CPU","Test CPU"},{"GPU","Test GPU"}},usage={{"vram",new Usage{used=2,total=8,percent=25}},{"ram",new Usage{used=8,total=16,percent=50}}}};
        reading.available=reading.values.Keys.ToDictionary(x=>x,_=>true);
        var snapshot=new MonitorSnapshot("24.0%","8.0 / 16.0 GiB · 50.0%","1.0 KiB/s","2.0 KiB/s",true,true){Hardware=reading,HardwarePeaks=reading.values.ToDictionary(x=>x.Key,x=>x.Value+10)};
        var window=new MonitorWindow(new MonitorSource(true),start:false);window.Show();window.Present(snapshot);Dispatcher.UIThread.RunJobs();
        var cards=window.GetVisualDescendants().OfType<Grid>().Single(x=>x.Name=="ReadingCards");
        var originals=cards.Children.ToArray();
        Check(originals.Select(x=>x.Name).SequenceEqual(new[]{"CardCPU","CardGPU","CardMemory","CardNVMe","CardAirflow","CardNetwork"}),"Original WPF device hierarchy and ordering");
        Check(originals.All(x=>x.IsVisible),"Supported devices visible");
        bool Text(string value)=>window.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.IsVisible&&x.Text==value);
        Check(Text("59.0 °C")&&Text("17.0 %")&&Text("Test CPU"),"CPU temperature, collector utilization and device name share card");
        Check(Text("VRAM 2.0 / 8.0 GB · 25.0%"),"GPU retains VRAM usage");
        reading.names["Network"]="Collector default adapter";
        window.Present(snapshot with {NetworkName="Selected traffic adapter"});
        Check(Text("Selected traffic adapter")&&!Text("Collector default adapter"),"Traffic subtitle identifies the sampled interface, not the collector default");
        var details=window.GetVisualDescendants().OfType<CheckBox>().Single(x=>x.Name=="Details");details.IsChecked=true;
        Check(Text("Core Voltage"),"Details reveals original full hardware labels");
        foreach(int width in new[]{360,800,1200}) {
            window.Width=width;window.Height=900;Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Check(cards.ColumnDefinitions.Count==(width==360?1:width==800?2:3),"One to three device columns");
            if(output!=null){using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(output,$"device-cards-{width}.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
        }
        window.Present(snapshot);
        Check(originals.SequenceEqual(cards.Children),"Polling reuses card controls");
        var stale=new Reading{state="STALE",available=reading.available,names=reading.names};
        window.Present(snapshot with {Hardware=stale});
        Check(!Text("59.0 °C")&&Text("—"),"Stale hardware clears live values and retains capability layout");
        Check(Text("Hardware readings unavailable. Waiting for the collector."),"Missing collector has explicit status independent of system counters");
        var mode=window.GetVisualDescendants().OfType<ComboBox>().Single(x=>x.Name=="ReadingMode");mode.SelectedIndex=1;
        Check(Text("69.0 °C"),"Session Max uses distinct hardware history");
        window.Close();Console.WriteLine("PASS device cards: WPF grouping, details, three columns, control reuse, stale data and peaks");
    }
}
