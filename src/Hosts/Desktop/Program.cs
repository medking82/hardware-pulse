using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace HardwarePulse.Desktop;

public static class Program {
    public static bool Demo { get; private set; }
    public static bool Smoke { get; private set; }
    public static bool Measure { get; private set; }
    static bool transientFramebuffer;
    internal static WindowsInstanceSession? Instance {get;private set;}
    [STAThread]
    public static int Main(string[] args) {
        if(args.Length==1&&args[0]=="--help") {
            Console.WriteLine("Pulse Desktop preview: [--demo] [--smoke-test | --measure-session [--diagnose-gc [--transient-framebuffer]]]. Measurement warms up for 10 seconds, then measures 60 seconds without personal settings. Optional GC diagnostics include startup and add observer overhead; transient framebuffer is a Linux-only diagnostic control. Windows/Linux/macOS live CPU, RAM and selected network; hardware support depends on the platform and device. Windows FPS requires the matching installed collector. Session Max preserves available peaks; RAM and quota remain current. Normal Windows launches reuse the existing preview window; diagnostics remain isolated.");return 0;
        }
        if(args.Any(a=>a!="--demo"&&a!="--smoke-test"&&a!="--measure-session"&&a!="--diagnose-gc"&&a!="--transient-framebuffer")||args.Distinct().Count()!=args.Length)return 2;
        Demo=args.Contains("--demo");Smoke=args.Contains("--smoke-test");Measure=args.Contains("--measure-session");
        if(Smoke&&Measure)return 2;
        bool diagnose=args.Contains("--diagnose-gc");
        if(diagnose&&!Measure)return 2;
        transientFramebuffer=args.Contains("--transient-framebuffer");
        if(transientFramebuffer&&(!diagnose||!OperatingSystem.IsLinux()))return 2;
        if(!Demo&&!OperatingSystem.IsLinux()&&!OperatingSystem.IsMacOS()&&!OperatingSystem.IsWindows())return 4;
        using var instance=OperatingSystem.IsWindows()&&!Demo&&!Smoke&&!Measure?new WindowsInstanceSession(DesktopProfile.InstanceScope):null;
        if(instance?.IsPrimary==false){instance.Notify();return 0;}
        Instance=instance;
        using var gc=diagnose?new GcDiagnostics():null;
        int result;
        try{result=BuildApp().StartWithClassicDesktopLifetime(args);}finally{Instance=null;}
        if(gc!=null)Console.WriteLine("DIAG_GC "+gc.Report());
        return Environment.ExitCode!=0?Environment.ExitCode:result;
    }
    public static AppBuilder BuildApp()=>AppBuilder.Configure<PulseApplication>().UsePlatformDetect()
        .With(DesktopFonts.Options())
        // Reuse the software surface across live updates; resizing still replaces it.
        // Hardware rendering selection/fallback remains Avalonia's default.
        .With(new X11PlatformOptions { UseRetainedFramebuffer=!transientFramebuffer });
}

public sealed class PulseApplication : Application {
    DesktopTray? tray;
    public override void Initialize()=>Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted() {
        if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            var monitor=new MonitorWindow(new MonitorSource(Program.Demo),Program.Smoke,
                store:Program.Demo||Program.Smoke||Program.Measure?null:PreviewSettingsStore.Default(),measure:Program.Measure);
            Program.Instance?.Listen(action=>Avalonia.Threading.Dispatcher.UIThread.Post(action),monitor.RestoreMain);
            if(Program.Smoke)monitor.Opened+=(_,_)=>{
                var coverage=FontCoverage.Capture();Console.WriteLine("FONT_COVERAGE "+System.Text.Json.JsonSerializer.Serialize(coverage));
                if(coverage.Any(x=>x.Missing.Length!=0)){Environment.ExitCode=3;desktop.Shutdown(3);}
            };
            if(!Program.Measure)tray=new DesktopTray(monitor,
                closeToTray:!Program.Smoke&&(OperatingSystem.IsWindows()||OperatingSystem.IsMacOS()));
            ConfigureLifetime(desktop,monitor,tray);
        }
        base.OnFrameworkInitializationCompleted();
    }
    public static void ConfigureLifetime(IClassicDesktopStyleApplicationLifetime desktop,MonitorWindow monitor,DesktopTray? tray) {
        desktop.ShutdownMode=Avalonia.Controls.ShutdownMode.OnExplicitShutdown;
        desktop.MainWindow=null;
        bool exiting=false,closed=false;
        monitor.Closing+=(_,e)=>{if(e.CloseReason is Avalonia.Controls.WindowCloseReason.ApplicationShutdown or Avalonia.Controls.WindowCloseReason.OSShutdown)exiting=true;};
        monitor.Closed+=(_,_)=>{closed=true;if(!exiting)desktop.Shutdown(Environment.ExitCode);};
        desktop.Startup+=(_,_)=>Avalonia.Threading.Dispatcher.UIThread.Post(()=>{
            if(closed)return;
            // The framework's automatic ShowMainWindow has already seen null.
            desktop.MainWindow=monitor;monitor.StartConfigured(tray?.IsAvailable==true);
        });
        desktop.Exit+=(_,_)=>{exiting=true;if(!closed)monitor.Close();tray?.Dispose();};
    }
}
