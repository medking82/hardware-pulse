using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace HardwarePulse.Desktop;

public static class Program {
    public static bool Demo { get; private set; }
    public static bool Smoke { get; private set; }
    public static bool Measure { get; private set; }
    static bool transientFramebuffer;
    [STAThread]
    public static int Main(string[] args) {
        if(args.Length==1&&args[0]=="--help") {
            Console.WriteLine("Pulse Desktop preview: [--demo] [--smoke-test | --measure-session [--diagnose-gc [--transient-framebuffer]]]. Measurement warms up for 10 seconds, then measures 60 seconds without personal settings. Optional GC diagnostics include startup and add observer overhead; transient framebuffer is a Linux-only diagnostic control. Linux/macOS live CPU, RAM and selected network. Windows requires --demo; use the existing WPF App for live Windows monitoring.");return 0;
        }
        if(args.Any(a=>a!="--demo"&&a!="--smoke-test"&&a!="--measure-session"&&a!="--diagnose-gc"&&a!="--transient-framebuffer")||args.Distinct().Count()!=args.Length)return 2;
        Demo=args.Contains("--demo");Smoke=args.Contains("--smoke-test");Measure=args.Contains("--measure-session");
        if(Smoke&&Measure)return 2;
        bool diagnose=args.Contains("--diagnose-gc");
        if(diagnose&&!Measure)return 2;
        transientFramebuffer=args.Contains("--transient-framebuffer");
        if(transientFramebuffer&&(!diagnose||!OperatingSystem.IsLinux()))return 2;
        if(!Demo&&!OperatingSystem.IsLinux()&&!OperatingSystem.IsMacOS())return 4;
        using var gc=diagnose?new GcDiagnostics():null;
        int result=BuildApp().StartWithClassicDesktopLifetime(args);
        if(gc!=null)Console.WriteLine("DIAG_GC "+gc.Report());
        return Environment.ExitCode!=0?Environment.ExitCode:result;
    }
    public static AppBuilder BuildApp()=>AppBuilder.Configure<PulseApplication>().UsePlatformDetect()
        // Reuse the software surface across live updates; resizing still replaces it.
        // Hardware rendering selection/fallback remains Avalonia's default.
        .With(new X11PlatformOptions { UseRetainedFramebuffer=!transientFramebuffer });
}

public sealed class PulseApplication : Application {
    DesktopTray? tray;
    public override void Initialize()=>Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted() {
        if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            desktop.MainWindow=new MonitorWindow(new MonitorSource(Program.Demo),Program.Smoke,
                store:Program.Demo||Program.Smoke||Program.Measure?null:PreviewSettingsStore.Default(),measure:Program.Measure);
            if(!Program.Measure)tray=new DesktopTray(desktop.MainWindow);
            desktop.Exit+=(_,_)=>tray?.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
