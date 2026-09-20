using System.Runtime.InteropServices;

namespace HardwarePulse;

// One shortcut on the owning window; no keyboard hook or input collection.
public sealed class WindowsDesktopHotkey : IDisposable {
    [DllImport("user32.dll")] static extern bool RegisterHotKey(nint window,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(nint window,int id);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint window,out uint process);
    readonly nint handle;
    int active;
    uint modifiers,key;
    bool disposed;
    public WindowsDesktopHotkey(nint handle) {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        this.handle=handle;
        if(!Owned())throw new InvalidOperationException("Shortcut requires an owned live window.");
    }
    bool Owned()=>handle!=0&&GetWindowThreadProcessId(handle,out uint process)!=0&&process==(uint)Environment.ProcessId;
    public static bool Valid(uint modifiers,uint key)=>modifiers is 3 or 5 or 6 or 7&&
        (key is >=65 and <=90 or >=48 and <=57 or >=112 and <=122);
    public bool Set(uint modifiers,uint key) {
        if(disposed||!Owned()||!Valid(modifiers,key))return false;
        if(active!=0&&this.modifiers==modifiers&&this.key==key)return true;
        int next=active==0x504?0x505:0x504;
        if(!RegisterHotKey(handle,next,modifiers|0x4000,key))return false;
        if(active!=0)UnregisterHotKey(handle,active);
        active=next;this.modifiers=modifiers;this.key=key;return true;
    }
    public bool Matches(uint message,nint id,nint data)=>!disposed&&active!=0&&message==0x312&&id==active&&
        ((long)data&0xffff)==modifiers&&(((long)data>>16)&0xffff)==key;
    public void Clear(){if(active!=0&&Owned())UnregisterHotKey(handle,active);active=0;modifiers=key=0;}
    public void Dispose(){if(disposed)return;Clear();disposed=true;}
}
