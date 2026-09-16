using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;

namespace HardwarePulse.Desktop;

public interface IDesktopShortcut : IDisposable {
    bool Set(string gesture);
    void Clear();
}

// Owns only this window's two registration IDs; replacement is transactional.
public sealed class WindowsDesktopShortcut : IDesktopShortcut {
    readonly Window window;
    readonly nint handle;
    readonly Action action;
    int active;
    string? current;
    bool disposed;
    [DllImport("user32.dll",SetLastError=true)] static extern bool RegisterHotKey(nint window,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(nint window,int id);
    public WindowsDesktopShortcut(Window window,Action action) {
        if(!OperatingSystem.IsWindows()||window.TryGetPlatformHandle() is not {HandleDescriptor:"HWND"} native)throw new PlatformNotSupportedException();
        this.window=window;handle=native.Handle;this.action=action;
        Win32Properties.AddWndProcHookCallback(window,Hook);
    }
    public static bool TryParse(string text,out KeyGesture? gesture,out uint modifiers,out uint key) {
        gesture=null;modifiers=key=0;
        try{gesture=KeyGesture.Parse(text);}catch(ArgumentException){return false;}catch(FormatException){return false;}
        var m=gesture.KeyModifiers;var k=gesture.Key;
        if((m&~(KeyModifiers.Control|KeyModifiers.Alt|KeyModifiers.Shift))!=0)return false;
        int count=((m&KeyModifiers.Control)!=0?1:0)+((m&KeyModifiers.Alt)!=0?1:0)+((m&KeyModifiers.Shift)!=0?1:0);
        if(count<2)return false;
        if(k>=Key.A&&k<=Key.Z)key=(uint)(0x41+k-Key.A);
        else if(k>=Key.D0&&k<=Key.D9)key=(uint)(0x30+k-Key.D0);
        else if(k>=Key.F1&&k<=Key.F11)key=(uint)(0x70+k-Key.F1);
        else return false;
        modifiers=((m&KeyModifiers.Alt)!=0?1u:0)|((m&KeyModifiers.Control)!=0?2u:0)|((m&KeyModifiers.Shift)!=0?4u:0);
        return true;
    }
    public bool Set(string text) {
        if(disposed||!TryParse(text,out var gesture,out uint modifiers,out uint key))return false;
        string normalized=gesture!.ToString();
        if(active!=0&&normalized==current)return true;
        int next=active==0x504?0x505:0x504;
        if(!RegisterHotKey(handle,next,modifiers|0x4000,key))return false;
        if(active!=0)UnregisterHotKey(handle,active);
        active=next;current=normalized;return true;
    }
    nint Hook(nint hwnd,uint message,nint w,nint l,ref bool handled) {
        if(!disposed&&message==0x312&&active!=0&&w==active){handled=true;action();}
        return 0;
    }
    public void Clear(){if(active!=0)UnregisterHotKey(handle,active);active=0;current=null;}
    public void Dispose(){if(disposed)return;Clear();disposed=true;Win32Properties.RemoveWndProcHookCallback(window,Hook);}
}
