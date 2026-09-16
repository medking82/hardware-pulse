using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using HardwarePulse;
using HardwarePulse.Desktop;

static class UiBenchmark {
    public static string Directory="",Scene="";
    public static int Width,Height;
    public static int Run(string[] args) {
        if(args.Length!=5||!int.TryParse(args[3],out Width)||!int.TryParse(args[4],out Height)||Width<360||Width>1600||Height<400||Height>1600)return 2;
        Directory=Path.GetFullPath(args[1]);Scene=args[2];
        if(Scene is not ("monitor" or "tray" or "desktop" or "desktop-contrast" or "desktop-dynamic" or "desktop-dynamic-contrast"))return 2;
        if(!File.Exists(Path.Combine(Directory,"runtime","snapshot.json")))return 2;
        return AppBuilder.Configure<BenchmarkApplication>().UsePlatformDetect().With(DesktopFonts.Options()).StartWithClassicDesktopLifetime([]);
    }
    sealed class Source : IMonitorSource {
        readonly ReadingSession readings=new(new WindowsSnapshotReadings(Path.Combine(Directory,"runtime","snapshot.json")).Read);
        public bool IsDemo=>true;
        public string[] Interfaces()=>[];
        public MonitorSnapshot Poll(string? name) {
            readings.Poll(DateTimeOffset.UtcNow);
            if(readings.Latest.state!="LIVE")throw new InvalidOperationException("Benchmark snapshot is not live");
            return MonitorSnapshot.Capture(readings,readings,readings) with{WindowsHardwareSupported=true,WindowsHardware=WindowsHardwarePresentation.Capture(readings,false),PeakWindowsHardware=WindowsHardwarePresentation.Capture(readings,true)};
        }
    }
    public sealed class BenchmarkApplication : Application {
        public override void Initialize()=>Styles.Add(new FluentTheme());
        public override void OnFrameworkInitializationCompleted() {
            var lifetime=(IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
            lifetime.ShutdownMode=ShutdownMode.OnExplicitShutdown;
            var source=new Source();var monitor=new MonitorWindow(source,start:false){Width=Width,Height=Height,ShowActivated=false,ShowInTaskbar=false};
            var tray=new DesktopTray(monitor);
            bool desktop=Scene.StartsWith("desktop"),contrast=Scene.EndsWith("-contrast"),dynamicScene=Scene.StartsWith("desktop-dynamic");
            bool externalBackground=Environment.GetEnvironmentVariable("PULSE_BENCHMARK_EXTERNAL_BACKGROUND")=="1";
            Window? backdrop=null;FloatingMonitorWindow? floating=null;int backgroundUpdates=0;
            var motion=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(33)};
            if(desktop) {
                var gradient=new LinearGradientBrush{StartPoint=new RelativePoint(0,0,RelativeUnit.Relative),EndPoint=new RelativePoint(1,1,RelativeUnit.Relative),GradientStops=new(){new(Colors.Black,0),new(Colors.White,.4),new(Colors.Gray,.7),new(Colors.Black,1)}};
                if(!externalBackground){
                backdrop=new Window{Width=Width,Height=Height,WindowDecorations=WindowDecorations.None,ShowInTaskbar=false,ShowActivated=false,Topmost=true,Background=gradient};backdrop.Show();
                backdrop.Position=new PixelPoint((int)(100*backdrop.RenderScaling),(int)(30*backdrop.RenderScaling));
                }
                var settings=new PreviewSettings{FloatingWidth=Width,FloatingHeight=Height,FloatingFontSize=16,FloatingRowSpacing=6,DesktopColumns=1,FloatingBackgroundOpacity=0,FloatingTopmost=true,FloatingLocalContrast=contrast};
                floating=new FloatingMonitorWindow(monitor.Language,settings){ShowActivated=false,ShowInTaskbar=false};
                floating.Present(source.Poll(null));floating.Show();floating.Position=backdrop?.Position??new PixelPoint((int)(100*floating.RenderScaling),(int)(30*floating.RenderScaling));floating.SetLocked(true);
                if(dynamicScene&&!externalBackground){var clock=Stopwatch.StartNew();motion.Tick+=(_,_)=>{double offset=.35*Math.Sin(clock.Elapsed.TotalSeconds*Math.PI/4);gradient.StartPoint=new(offset,0,RelativeUnit.Relative);gradient.EndPoint=new(1+offset,1,RelativeUnit.Relative);backgroundUpdates++;};motion.Start();}
            }
            monitor.Present(source.Poll(null));if(!desktop)monitor.Show();
            Window view=floating??(Window)monitor;
            var poll=new DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};
            poll.Tick+=(_,_)=>{
                try {
                    if(File.Exists(Path.Combine(Directory,"BENCH-STOP"))){
                        poll.Stop();motion.Stop();File.WriteAllText(Path.Combine(Directory,"completed.json"),JsonSerializer.Serialize(new{backgroundUpdates}));lifetime.Shutdown();return;
                    }
                    var snapshot=source.Poll(null);monitor.Present(snapshot);floating?.Present(snapshot);
                    if(contrast&&floating?.ContrastStatus!="Local contrast active")throw new InvalidOperationException("Contrast unavailable during benchmark");
                }catch(Exception e){Console.Error.WriteLine(e.Message);lifetime.Shutdown(1);}
            };
            lifetime.Startup+=(_,_)=>Dispatcher.UIThread.Post(async ()=>{
                await Task.Delay(500);
                if(Scene=="tray")monitor.Hide();
                var origin=view.PointToScreen(default);var end=view.PointToScreen(new Point(view.ClientSize.Width,view.ClientSize.Height));
                File.WriteAllText(Path.Combine(Directory,"ready.json"),JsonSerializer.Serialize(new{scene=Scene,widthDip=view.ClientSize.Width,heightDip=view.ClientSize.Height,widthPixels=end.X-origin.X,heightPixels=end.Y-origin.Y,leftPixels=origin.X,topPixels=origin.Y,externalBackground,localContrast=contrast,backgroundIntervalMilliseconds=dynamicScene?33:0,backgroundPeriodSeconds=dynamicScene?8:0}));poll.Start();
            });
            lifetime.Exit+=(_,_)=>{poll.Stop();motion.Stop();floating?.Close();backdrop?.Close();monitor.Close();tray.Dispose();};
            base.OnFrameworkInitializationCompleted();
        }
    }
}
