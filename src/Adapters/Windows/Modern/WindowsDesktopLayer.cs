using System.Runtime.InteropServices;
using System.Text;

namespace HardwarePulse;

// Only reorders the owned top-level window. Never parents into or writes to Explorer.
public sealed class WindowsDesktopLayer : IDisposable {
    delegate bool Enumerate(nint window,nint state);
    delegate void WinEvent(nint hook,uint kind,nint window,int obj,int child,uint thread,uint time);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint window,out uint process);
    [DllImport("user32.dll")] static extern bool EnumWindows(Enumerate callback,nint state);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern nint FindWindowEx(nint parent,nint after,string cls,string? title);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(nint window,StringBuilder name,int count);
    [DllImport("user32.dll")] static extern nint GetWindow(nint window,uint command);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern nint GetWindowLongPtr(nint window,int index);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] static extern bool SetWindowPos(nint window,nint after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")] static extern nint SetWinEventHook(uint min,uint max,nint module,WinEvent callback,uint process,uint thread,uint flags);
    [DllImport("user32.dll")] static extern bool UnhookWinEvent(nint hook);
    [StructLayout(LayoutKind.Sequential)] struct Rect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] struct Point {public int X,Y;}
    [DllImport("user32.dll")] static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool GetClientRect(nint window,out Rect rect);
    [DllImport("user32.dll")] static extern bool ClientToScreen(nint window,ref Point point);
    [DllImport("user32.dll")] static extern nint GetDC(nint window);
    [DllImport("user32.dll")] static extern int ReleaseDC(nint window,nint dc);
    [DllImport("gdi32.dll")] static extern uint GetPixel(nint dc,int x,int y);
    readonly nint handle;
    readonly WinEvent callback;
    nint hook;
    int queued;
    volatile bool disposed;
    public bool Attached {get;private set;}
    public WindowsDesktopLayer(nint handle,Action<Action> dispatch,Action refresh) {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        this.handle=handle;
        if(!Owned())throw new InvalidOperationException("Desktop placement requires an owned live window.");
        callback=(_,_,_,_,_,_,_)=>{
            if(disposed||Interlocked.Exchange(ref queued,1)!=0)return;
            dispatch(()=>{Interlocked.Exchange(ref queued,0);if(!disposed)refresh();});
        };
        hook=SetWinEventHook(3,3,0,callback,0,0,0);
    }
    bool Owned()=>handle!=0&&GetWindowThreadProcessId(handle,out uint process)!=0&&process==(uint)Environment.ProcessId;
    static nint DesktopHost() {
        nint result=0;
        EnumWindows((candidate,_)=>{
            var name=new StringBuilder(64);GetClassName(candidate,name,name.Capacity);
            if((name.ToString() is "Progman" or "WorkerW")&&IsWindowVisible(candidate)&&FindWindowEx(candidate,0,"SHELLDLL_DefView",null)!=0){result=candidate;return false;}
            return true;
        },0);
        return result;
    }
    public bool Refresh(bool topmost) {
        if(disposed||!Owned())return Attached=false;
        const uint flags=0x1|0x2|0x10|0x40|0x200; // show; no move, resize, activate or owner reorder
        if(topmost)return Attached=((GetWindowLongPtr(handle,-20).ToInt64()&8)!=0&&IsWindowVisible(handle))||SetWindowPos(handle,-1,0,0,0,0,flags);
        nint host=DesktopHost();
        if(host==0)return Attached=false;
        // Explicitly leave the topmost band before placing behind ordinary apps.
        if((GetWindowLongPtr(handle,-20).ToInt64()&8)!=0&&!SetWindowPos(handle,-2,0,0,0,0,flags))return Attached=false;
        nint preceding=GetWindow(host,3);
        if(preceding==handle)return Attached=IsWindowVisible(handle)||SetWindowPos(handle,0,0,0,0,0,flags|4);
        return Attached=SetWindowPos(handle,preceding,0,0,0,0,flags);
    }
    public void Dispose(){if(disposed)return;disposed=true;Attached=false;if(hook!=0)UnhookWinEvent(hook);hook=0;GC.KeepAlive(callback);}
    public double? SampleBackground() {
        if(disposed||!Attached||!Owned()||!IsWindowVisible(handle))return null;
        var name=new StringBuilder(64);GetClassName(GetForegroundWindow(),name,name.Capacity);
        if(name.ToString() is not ("Progman" or "WorkerW"))return null;
        if(!GetClientRect(handle,out var rect)||rect.Right<4||rect.Bottom<24)return null;
        var origin=new Point();if(!ClientToScreen(handle,ref origin))return null;
        nint dc=GetDC(0);if(dc==0)return null;
        try {
            double total=0;int count=0;
            static double Linear(byte value){double x=value/255d;return x<=.04045?x/12.92:Math.Pow((x+.055)/1.055,2.4);}
            // Six edge-adjacent pixels, outside our painted surface; never sample glyphs.
            foreach(double fraction in new[]{.2,.5,.8})foreach(int x in new[]{-2,rect.Right+2}) {
                uint pixel=GetPixel(dc,origin.X+x,origin.Y+(int)(rect.Bottom*fraction));if(pixel==uint.MaxValue)continue;
                total+=.2126*Linear((byte)pixel)+.7152*Linear((byte)(pixel>>8))+.0722*Linear((byte)(pixel>>16));count++;
            }
            return count==0?null:total/count;
        }finally{ReleaseDC(0,dc);}
    }
}
