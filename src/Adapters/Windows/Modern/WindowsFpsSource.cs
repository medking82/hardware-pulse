#nullable enable
using System.Diagnostics;

namespace HardwarePulse;

public interface IDesktopFpsSource : IDisposable {
    FrameMetrics Poll(string processName);
    void Reset();
}

// Serial caller owns this session. The pipe client retains the existing fixed
// protocol and server identity check; no UI process launches PresentMon.
public sealed class WindowsFpsSource : IDesktopFpsSource {
    readonly FpsClient client;
    Process? target;
    public WindowsFpsSource() {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        client=new FpsClient(Path.Combine(AppContext.BaseDirectory,"worker","HardwarePulse.Collector.exe"));
    }
    public static string[] Targets() {
        if(!OperatingSystem.IsWindows())return [];
        var names=new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var process in Process.GetProcesses())using(process) {
            try{if(OverlayTarget.Eligible(process))names.Add(process.ProcessName);}catch{}
        }
        return names.ToArray();
    }
    public FrameMetrics Poll(string processName) {
        // Legacy Resolve explicitly accepts null for the initial target.
        var next=OverlayTarget.Resolve(processName,target!);
        if(!ReferenceEquals(target,next)){target?.Dispose();target=next;}
        if(target==null){client.Select(0,0);return new(){Status="Waiting for target app"};}
        try {target.Refresh();client.Select(target.Id,target.StartTime.ToUniversalTime().Ticks);return client.Read();}
        catch(Exception e) when(e is InvalidOperationException or System.ComponentModel.Win32Exception or ArgumentException){target.Dispose();target=null;client.Select(0,0);return new(){Status="Waiting for target app"};}
    }
    public void Reset()=>client.Reset();
    public void Dispose(){client.Dispose();target?.Dispose();target=null;}
}
