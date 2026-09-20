using Avalonia.Threading;
using System.Security.Principal;

namespace HardwarePulse.Desktop;

// Read-only cooperation with the existing per-user installer/collector STOP file.
public sealed class DesktopStopMonitor : IDisposable {
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(500)};
    readonly string path;
    readonly Action shutdown;
    bool disposed;
    public static string WindowsPath() {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        using var identity=WindowsIdentity.GetCurrent();
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"HardwarePulse",
            identity.User?.Value??throw new InvalidOperationException("Current user unavailable"),"runtime","STOP");
    }
    public DesktopStopMonitor(string path,Action shutdown){
        this.path=Path.GetFullPath(path);this.shutdown=shutdown;timer.Tick+=Tick;timer.Start();
    }
    void Tick(object? sender,EventArgs e){if(disposed||!File.Exists(path))return;Dispose();shutdown();}
    public void Dispose(){if(disposed)return;disposed=true;timer.Stop();timer.Tick-=Tick;}
}
