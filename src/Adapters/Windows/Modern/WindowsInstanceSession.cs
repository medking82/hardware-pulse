using System.Security.Principal;

namespace HardwarePulse;

// One UI per current user's Windows session and profile identity. The event
// carries only the existing show-home notification; no command channel is added.
public sealed class WindowsInstanceSession : IDisposable {
    readonly Mutex mutex;
    readonly AppActivation activation;
    bool disposed;
    public bool IsPrimary {get;}
    public WindowsInstanceSession(string identity) {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        if(identity.Length is <1 or >128||identity.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='.'&&c!='-'))throw new ArgumentException("Invalid instance identity",nameof(identity));
        using var user=WindowsIdentity.GetCurrent();
        string name="Local\\HardwarePulse."+identity+"."+user.User!.Value;
        activation=new AppActivation(name+".ShowHome");
        try {
            mutex=new Mutex(false,name+".UI");
            try{IsPrimary=mutex.WaitOne(0);}catch(AbandonedMutexException){IsPrimary=true;}
        }catch{mutex?.Dispose();activation.Dispose();throw;}
    }
    public void Listen(Action<Action> post,Action restore) {
        ObjectDisposedException.ThrowIf(disposed,this);
        if(!IsPrimary)throw new InvalidOperationException("Only the primary instance may listen");
        activation.Listen(post,restore);
    }
    public void Notify() {
        ObjectDisposedException.ThrowIf(disposed,this);
        string executable=Environment.ProcessPath??"";
        // Development dotnet hosts are not identifiable by executable path alone.
        if(string.Equals(Path.GetFileNameWithoutExtension(executable),"dotnet",StringComparison.OrdinalIgnoreCase))executable="";
        activation.Notify(executable);
    }
    public void Dispose() {
        if(disposed)return;disposed=true;activation.Dispose();
        if(IsPrimary)mutex.ReleaseMutex();mutex.Dispose();
    }
}
