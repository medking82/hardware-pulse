using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class Tests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    [STAThread]
    static void Main(string[] args) {
        var expected=Environment.GetEnvironmentVariable("PULSE_TEST_ARCH");
        Check(expected==null||expected==System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),"Architecture mismatch");
        AppBuilder.Configure<PulseApplication>().UseSkia().UseHeadless(new(){UseHeadlessDrawing=false}).SetupWithoutStarting();
        var source=new MonitorSource(true);
        var window=new MonitorWindow(source,start:false);
        window.Show();window.Present(source.Poll(null));
        foreach(int width in new[]{800,360,1200}) {
            window.Width=width;window.Height=700;Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var cards=window.GetVisualDescendants().OfType<Grid>().First(x=>x.Children.OfType<Border>().Count()==4);
            Check(cards.ColumnDefinitions.Count==(width>=660?2:1),"Responsive columns");
            foreach(var value in window.GetVisualDescendants().OfType<TextBlock>())
                Check(value.Bounds.Width<=width,"Text exceeds window width");
            Check(window.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="24.0%"),"CPU value preserved");
            if(args.Length==1) {
                using var frame=window.CaptureRenderedFrame();
                frame!.Save(System.IO.Path.Combine(args[0],$"desktop-{width}.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            }
        }
        var pause=window.GetVisualDescendants().OfType<CheckBox>().Single(x=>x.Name=="PauseHardware");
        pause.Focus();window.KeyPress(Key.Space,RawInputModifiers.None,PhysicalKey.Space," ");window.KeyRelease(Key.Space,RawInputModifiers.None,PhysicalKey.Space," ");
        Check(pause.IsChecked==true,"Pause keyboard interaction");
        window.Present(new("—","—","—","—",false,false));
        Check(!window.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="24.0%"),"Unavailable clears stale values");
        window.Close();
        var live=new MonitorWindow(new MonitorSource(true));live.Show();
        var limit=DateTime.UtcNow.AddSeconds(5);
        while(!live.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="24.0%")&&DateTime.UtcNow<limit){Dispatcher.UIThread.RunJobs();Thread.Sleep(10);}
        Check(live.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="24.0%"),"Worker publishes to UI");
        live.Close();
        while(!live.Sampling.IsCompleted&&DateTime.UtcNow<limit){Dispatcher.UIThread.RunJobs();Thread.Sleep(10);}
        Check(live.Sampling.IsCompletedSuccessfully,"Close cancels and completes sampling");
        QuotaPanelTests.Run(args.Length==1?args[0]:null);
        SettingsTests.Run(args.Length==1?args[0]:null);
        Console.WriteLine("PASS Desktop rendering, responsive cards, unavailable state, keyboard and worker shutdown");
    }
}
