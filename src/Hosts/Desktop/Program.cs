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
            Console.WriteLine("Pulse Desktop preview: [--demo] [--smoke-test | --measure-session [--diagnose-gc [--transient-framebuffer]]]. Measurement warms up for 10 seconds, then measures 60 seconds without personal settings. Optional GC diagnostics include startup and add observer overhead; transient framebuffer is a Linux-only diagnostic control. Windows/Linux/macOS live CPU, RAM and selected network; Linux kernel-exposed temperature and fan channels when available. Session Max preserves CPU, network and Linux sensor peaks; RAM and quota remain current. Windows FPS and game overlay share one session; frame capture requires the matching collector. Other platforms do not provide game overlay capture.");return 0;
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
        if(DesktopProfile.IsInstalledStable&&!Demo&&!Smoke&&!Measure&&File.Exists(DesktopStopMonitor.WindowsPath()))return 0;
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
    DesktopStopMonitor? stopMonitor;
    public override void Initialize() {
        Styles.Add(new FluentTheme());
        Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://Pulse.Desktop/")){Source=new Uri("avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml")});
    }
    public override void OnFrameworkInitializationCompleted() {
        if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            desktop.MainWindow=new MonitorWindow(new MonitorSource(Program.Demo),Program.Smoke,
                store:Program.Demo||Program.Smoke||Program.Measure?null:PreviewSettingsStore.Default(),measure:Program.Measure);
            var monitor=(MonitorWindow)desktop.MainWindow;
            Program.Instance?.Listen(action=>Avalonia.Threading.Dispatcher.UIThread.Post(action),monitor.RestoreMain);
            if(Program.Smoke)desktop.MainWindow.Opened+=(_,_)=>{
                var coverage=FontCoverage.Capture();Console.WriteLine("FONT_COVERAGE "+System.Text.Json.JsonSerializer.Serialize(coverage));
                if(coverage.Any(x=>x.Missing.Length!=0)){Environment.ExitCode=3;desktop.Shutdown(3);}
            };
            if(!Program.Measure)tray=new DesktopTray(monitor,closeToTray:!Program.Smoke&&!Program.Demo&&(OperatingSystem.IsWindows()||OperatingSystem.IsMacOS()));
            if(DesktopProfile.IsInstalledStable&&!Program.Demo&&!Program.Smoke&&!Program.Measure)
                stopMonitor=new DesktopStopMonitor(DesktopStopMonitor.WindowsPath(),()=>desktop.Shutdown());
            desktop.Exit+=(_,_)=>{stopMonitor?.Dispose();tray?.Dispose();};
        }
        base.OnFrameworkInitializationCompleted();
    }
}
