using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Controls;
using Avalonia.Threading;

namespace HardwarePulse.Desktop;

// Matches the native host's desktop Z-order policy without parenting into Explorer.
// Every mutation targets only the supplied Avalonia window. Input remains separately owned.
public sealed class WindowsDesktopLayer : IDisposable {
    delegate bool Enumerate(nint window,nint state);
    delegate void WinEvent(nint hook,uint kind,nint window,int obj,int child,uint thread,uint time);
    [DllImport("user32.dll")] static extern bool EnumWindows(Enumerate callback,nint state);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern nint FindWindowEx(nint parent,nint after,string cls,string? title);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(nint window,StringBuilder name,int count);
    [DllImport("user32.dll")] static extern nint GetWindow(nint window,uint command);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint window,out uint process);
    [DllImport("user32.dll")] static extern bool SetWindowPos(nint window,nint after,int x,int y,int cx,int cy,uint flags);
    [DllImport("user32.dll")] static extern nint SetWinEventHook(uint min,uint max,nint module,WinEvent callback,uint process,uint thread,uint flags);
    [DllImport("user32.dll")] static extern bool UnhookWinEvent(nint hook);
    readonly Window window;
    readonly nint handle;
    readonly Func<bool> locked;
    readonly WinEvent callback;
    nint hook;
    bool disposed,queued,placed;
    public bool Attached {get;private set;}
    public WindowsDesktopLayer(Window window,Func<bool> locked) {
        if(!OperatingSystem.IsWindows()||window.TryGetPlatformHandle() is not {HandleDescriptor:"HWND"} native)throw new PlatformNotSupportedException();
        handle=native.Handle;GetWindowThreadProcessId(handle,out uint process);
        if(process!=Environment.ProcessId)throw new InvalidOperationException("Desktop placement requires an owned window.");
        this.window=window;this.locked=locked;
        callback=(_,_,_,_,_,_,_)=>{
            if(disposed||queued)return;queued=true;
            Dispatcher.UIThread.Post(()=>{queued=false;if(!disposed)Refresh();},DispatcherPriority.Background);
        };
        hook=SetWinEventHook(3,3,0,callback,0,0,0); // out-of-context foreground notification
    }
    public static nint FindDesktopHost() {
        nint found=0;
        EnumWindows((candidate,_)=>{
            var name=new StringBuilder(64);GetClassName(candidate,name,name.Capacity);
            if((name.ToString() is "Progman" or "WorkerW")&&IsWindowVisible(candidate)&&FindWindowEx(candidate,0,"SHELLDLL_DefView",null)!=0){found=candidate;return false;}
            return true;
        },0);
        return found;
    }
    public void Refresh() {
        Attached=false;
        // Neither a notification nor a poll may resurrect a user-hidden window.
        if(disposed||!window.IsVisible)return;
        const uint flags=0x1|0x2|0x10|0x200; // no resize, move, activation or owner reordering
        if(!locked()) {
            if(placed){SetWindowPos(handle,window.Topmost?-1:0,0,0,0,0,flags);placed=false;}
            return;
        }
        if(window.Topmost){Attached=SetWindowPos(handle,-1,0,0,0,0,flags);return;}
        nint host=FindDesktopHost();
        if(host==0)return; // normal non-topmost fallback; next existing sample retries discovery
        nint preceding=GetWindow(host,3);
        Attached=preceding==handle||SetWindowPos(handle,preceding,0,0,0,0,flags);
        placed|=Attached;
    }
    public void Dispose(){if(disposed)return;disposed=true;Attached=false;if(hook!=0)UnhookWinEvent(hook);hook=0;}
}
