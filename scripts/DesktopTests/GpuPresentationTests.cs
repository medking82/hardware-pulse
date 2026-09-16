using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class GpuPresentationTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run(string? output) {
        var window=new MonitorWindow(new MonitorSource(true),start:false){Width=800,Height=1000};
        window.Show();
        var snapshot=new MonitorSnapshot("21.0%","4.0 / 16.0 GiB · 25.0%","—","—",true,true) {
            CpuModel="Apple M4 Pro",CpuPhysicalCores=14,CpuLogicalCores=14,GpusSupported=true,
            Gpus=[new("gpu/1","Apple GPU","25.0%",20),new("gpu/2","External GPU","—")],
            PeakGpus=[new("gpu/1","Apple GPU","75.0%",20),new("gpu/2","External GPU","—")]
        };
        window.Present(snapshot);window.OpenFloatingMonitor();
        bool Has(Window host,string text)=>host.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text==text);
        Check(Has(window,"Apple M4 Pro\n14 physical cores · 14 logical cores"),"CPU identity visible");
        Check(Has(window,"Apple GPU · 20 GPU cores")&&Has(window,"External GPU · — GPU cores"),"GPU measured/unknown cores distinguished");
        Check(Has(window.FloatingMonitor!,"25.0%")&&Has(window.FloatingMonitor!,"Apple GPU · 20 GPU cores"),"Floating GPU uses same snapshot");
        var mode=window.GetVisualDescendants().OfType<ComboBox>().Single(x=>x.Name=="ReadingMode");mode.SelectedIndex=1;
        Check(Has(window,"75.0%")&&Has(window.FloatingMonitor!,"75.0%"),"GPU peaks switch without another sample");
        window.Language.Select("zh-CN");
        Check(Has(window,"Apple M4 Pro\n14 个物理核心 · 14 个逻辑核心")&&Has(window.FloatingMonitor!,"Apple GPU · 20 个 GPU 核心"),"Identity metadata follows language without translating model");
        window.Language.Select("en");mode.SelectedIndex=0;
        foreach(int width in new[]{800,360}) {
            window.Width=width;Dispatcher.UIThread.RunJobs();AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var panel=window.GetVisualDescendants().OfType<HardwareSensorPanel>().Single(x=>x.Name=="GpuReadings");
            foreach(var row in panel.GetVisualDescendants().OfType<Grid>()) {
                Check(row.Children[0].Bounds.Right<=row.Children[1].Bounds.Left&&row.Children[1].Bounds.Right<=row.Bounds.Width+.1,"GPU metadata and value layout");
            }
            if(output!=null){using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(output,$"gpu-identity-{width}.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
        }
        window.Present(snapshot with {Gpus=[],PeakGpus=[]});
        Check(!Has(window,"Apple GPU · 20 GPU cores")&&!Has(window.FloatingMonitor!,"25.0%"),"Removed GPU clears both views");
        window.Close();Console.WriteLine("PASS CPU/GPU identity and GPU live/peak presentation, localization, narrow layout and removal");
    }
}
