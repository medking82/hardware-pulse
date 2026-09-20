using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse;
using HardwarePulse.Desktop;

static class DeviceCardsTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run(string? output) {
        var reading=new Reading{state="LIVE",identity="fixture",values={{"cpu",59},{"cpuLoad",17},{"vcore",1.2},{"cpuFan",800},{"gpu",49},{"gpuLoad",20},{"gpuVolt",.85},{"ramA",41},{"ramB",42},{"diskC",43},{"system",38},{"bottom",900},{"top",1000}},
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
        var cpu=(Border)originals[0];
        TextBlock CpuText(string text)=>cpu.GetVisualDescendants().OfType<TextBlock>().Single(x=>x.IsEffectivelyVisible&&x.Text==text);
        var load=CpuText("Load").TranslatePoint(new Avalonia.Point(),cpu)!.Value;
        var voltage=CpuText("Vcore").TranslatePoint(new Avalonia.Point(),cpu)!.Value;
        Check(Math.Abs(load.Y-voltage.Y)<1&&voltage.X>load.X,"Compact CPU metrics share a row when there is room, like WPF 0.6.27");
        if(output!=null){using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"device-cards-compact.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
        cpu.Width=140;cpu.HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Left;Dispatcher.UIThread.RunJobs();
        load=CpuText("Load").TranslatePoint(new Point(),cpu)!.Value;voltage=CpuText("Vcore").TranslatePoint(new Point(),cpu)!.Value;
        Check(voltage.Y>load.Y,"Compact metrics fall back to one column when narrow");
        var title=CpuText("CPU");var temperature=CpuText("59.0 °C");
        Check(temperature.TranslatePoint(new Point(),cpu)!.Value.Y>=title.TranslatePoint(new Point(),cpu)!.Value.Y+title.Bounds.Height,"Narrow hero stacks below title without overlap");
        cpu.Width=double.NaN;cpu.HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Stretch;Dispatcher.UIThread.RunJobs();
        window.Height=900;Dispatcher.UIThread.RunJobs();
        reading.names["Network"]="Collector default adapter";
        window.Present(snapshot with {NetworkName="Selected traffic adapter"});
        Check(Text("Selected traffic adapter")&&!Text("Collector default adapter"),"Traffic subtitle identifies the sampled interface, not the collector default");
        var details=window.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>().Single(x=>x.Name=="Details");details.IsChecked=true;
        Dispatcher.UIThread.RunJobs();
        Check(Text("Core Voltage"),"Details reveals original full hardware labels");
        Check(CpuText("Vcore · Motherboard").TranslatePoint(new Point(),cpu)!.Value.Y>CpuText("Utilization").TranslatePoint(new Point(),cpu)!.Value.Y,"Details restores full-width rows");
        foreach(int width in new[]{360,800,1200}) {
            window.Width=width;window.Height=900;Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Check(cards.ColumnDefinitions.Count==(width==360?1:width==800?2:3),"One to three device columns");
            foreach(var card in originals)foreach(var text in card.GetVisualDescendants().OfType<TextBlock>().Where(x=>x.IsEffectivelyVisible)) {
                var position=text.TranslatePoint(new Point(),card)!.Value;
                Check(position.X>=0&&position.X+text.Bounds.Width<=card.Bounds.Width+1,$"Device text stays inside card at {width}: {text.Text}");
            }
            if(output!=null){using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(output,$"device-cards-{width}.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
        }
        window.Present(snapshot);
        Check(originals.SequenceEqual(cards.Children),"Polling reuses card controls");
        var stale=new Reading{state="STALE",available=reading.available,names=reading.names};
        window.Present(snapshot with {Hardware=stale});
        Check(!Text("59.0 °C")&&Text("—"),"Stale hardware clears live values and retains capability layout");
        Check(Text("Hardware readings unavailable. Waiting for the collector."),"Missing collector has explicit status independent of system counters");
        var mode=window.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="SessionMax");mode.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Check(Text("69.0 °C"),"Session Max uses distinct hardware history");
        void Click(string name){window.GetVisualDescendants().OfType<Button>().Single(x=>x.Name==name).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));Dispatcher.UIThread.RunJobs();}
        window.Present(snapshot);
        foreach(int size in new[]{10,12,16}) {
            Click("OpenSettings");window.GetVisualDescendants().OfType<TabControl>().Single(x=>x.Name=="SettingsTabs").SelectedIndex=1;Dispatcher.UIThread.RunJobs();
            window.GetVisualDescendants().OfType<Slider>().Single(x=>x.Name=="AppFontSize").Value=size;Click("Back");
            foreach(int width in new[]{360,800,1200}) {
                window.Width=width;window.Height=900;Dispatcher.UIThread.RunJobs();
                Check(Math.Abs(CpuText("CPU").FontSize-13.0*size/12)<.01,"Card typography follows original font scale");
                foreach(var card in originals)foreach(var text in card.GetVisualDescendants().OfType<TextBlock>().Where(x=>x.IsEffectivelyVisible)) {
                    var position=text.TranslatePoint(new Point(),card)!.Value;
                    Check(position.X>=0&&position.X+text.Bounds.Width<=card.Bounds.Width+1,$"Scaled device text stays inside card at {size}/{width}: {text.Text}");
                }
                if(output!=null&&width==800){using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(output,$"device-font-{size}.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
            }
        }
        window.Width=360;window.Height=400;details.IsChecked=false;Dispatcher.UIThread.RunJobs();
        Check(originals.SelectMany(x=>x.GetVisualDescendants().OfType<TextBlock>()).Where(x=>x.Name=="DeviceSubtitle").All(x=>!x.IsVisible),"Shortest compact density hides subtitles when cards cannot fit");
        details.IsChecked=true;Dispatcher.UIThread.RunJobs();
        Check(originals.SelectMany(x=>x.GetVisualDescendants().OfType<TextBlock>()).Where(x=>x.Name=="DeviceSubtitle").All(x=>x.IsVisible),"Details always retains device identity even when scrolling is necessary");
        window.Close();Console.WriteLine("PASS device cards: WPF grouping, details, three columns, control reuse, stale data and peaks");
    }
}
