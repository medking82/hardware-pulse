using System.ComponentModel;
using System.Diagnostics;

namespace HardwarePulse;

public enum DesktopStartupState { Unavailable,Disabled,Enabled }
public enum DesktopStartupResult { Success,Canceled,Failed }
public interface IDesktopStartupManagement {
    Task<DesktopStartupState> ReadAsync();
    Task<DesktopStartupResult> SetEnabledAsync(bool enabled);
    Task<DesktopStartupResult> StartCollectorAsync();
}

// Only the matching installed worker may manage tasks. Ownership/rollback stays
// in Startup.Shared inside that worker; the UI never constructs task XML.
public sealed class WindowsStartupManagement : IDesktopStartupManagement {
    static string? Worker() {
        if(!OperatingSystem.IsWindows())return null;
        try {
            string ui=Path.Combine(AppContext.BaseDirectory,"HardwarePulse.exe");
            string worker=Path.Combine(AppContext.BaseDirectory,"worker","HardwarePulse.Collector.exe");
            return string.Equals(Environment.ProcessPath,ui,StringComparison.OrdinalIgnoreCase)&&FpsProtocol.ProtectedTool(ui)&&FpsProtocol.ProtectedTool(worker)?worker:null;
        }catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException){return null;}
    }
    public async Task<DesktopStartupState> ReadAsync() {
        int code=await Run("--startup-enabled",false);
        return code==0?DesktopStartupState.Enabled:code==3?DesktopStartupState.Disabled:DesktopStartupState.Unavailable;
    }
    public Task<DesktopStartupResult> SetEnabledAsync(bool enabled)=>Change(enabled?"--enable-startup":"--disable-startup");
    public Task<DesktopStartupResult> StartCollectorAsync()=>Change("--start-collector");
    static async Task<DesktopStartupResult> Change(string command) {
        int code=await Run(command,true);
        return code==0?DesktopStartupResult.Success:code==1223?DesktopStartupResult.Canceled:DesktopStartupResult.Failed;
    }
    static Task<int> Run(string command,bool elevated)=>Task.Run(async ()=>{
        string? worker=Worker();if(worker==null)return 4;
        try {
            var info=new ProcessStartInfo(worker){UseShellExecute=elevated,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(worker)!};
            info.ArgumentList.Add(command);if(elevated)info.Verb="runas";
            using var child=Process.Start(info);if(child==null)return 1;
            // Do not kill a management operation in the middle of task rollback.
            // Closing the UI discards its result; the worker finishes independently.
            if(elevated)await child.WaitForExitAsync();
            else {
                using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try{await child.WaitForExitAsync(timeout.Token);}
                catch(OperationCanceledException){try{if(!child.HasExited)child.Kill();}catch(InvalidOperationException){}return 1;}
            }
            return child.ExitCode;
        }catch(Win32Exception e){return e.NativeErrorCode==1223?1223:1;}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or InvalidOperationException){return 1;}
    });
}
