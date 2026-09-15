using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace HardwarePulse.Desktop;

public static class Program {
    public static bool Demo { get; private set; }
    public static bool Smoke { get; private set; }
    [STAThread]
    public static int Main(string[] args) {
        if(args.Length==1&&args[0]=="--help") {
            Console.WriteLine("Pulse Desktop preview: [--demo] [--smoke-test]. Linux/macOS live CPU, RAM and selected network. Windows requires --demo; use the existing WPF App for live Windows monitoring.");return 0;
        }
        if(args.Any(a=>a!="--demo"&&a!="--smoke-test")||args.Distinct().Count()!=args.Length)return 2;
        Demo=args.Contains("--demo");Smoke=args.Contains("--smoke-test");
        if(!Demo&&!OperatingSystem.IsLinux()&&!OperatingSystem.IsMacOS())return 4;
        int result=BuildApp().StartWithClassicDesktopLifetime(args);
        return Environment.ExitCode!=0?Environment.ExitCode:result;
    }
    public static AppBuilder BuildApp()=>AppBuilder.Configure<PulseApplication>().UsePlatformDetect();
}

public sealed class PulseApplication : Application {
    public override void Initialize()=>Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted() {
        if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow=new MonitorWindow(new MonitorSource(Program.Demo),Program.Smoke,
                store:Program.Demo||Program.Smoke?null:PreviewSettingsStore.Default());
        base.OnFrameworkInitializationCompleted();
    }
}
