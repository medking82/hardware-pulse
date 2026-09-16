using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Threading;

namespace HardwarePulse.Desktop;

// Borrow the host's live XID. Never change a window-manager frame or foreign window.
public sealed class X11WindowInput : IDisposable {
    readonly Window window;
    readonly object backend;
    readonly nint handle;
    readonly WindowDecorations decorations;
    readonly List<nint> shaped=new();
    nint display;
    bool applied;
    X11WindowInput(Window window,nint display) {
        this.window=window;this.display=display;backend=window.PlatformImpl!;
        handle=window.TryGetPlatformHandle()!.Handle;decorations=window.WindowDecorations;
    }
    public static X11WindowInput? TryCreate(Window window) {
        Dispatcher.UIThread.VerifyAccess();
        if(!OperatingSystem.IsLinux()||window.TryGetPlatformHandle()?.HandleDescriptor!="XID"||window.PlatformImpl==null)return null;
        nint display=0;
        try {
            display=XOpenDisplay(null);if(display==0)return null;
            int major=2,minor=0;
            if(XFixesQueryExtension(display,out _,out _)==0||XFixesQueryVersion(display,ref major,ref minor)==0||major<2)return null;
            if(XShapeQueryVersion(display,out major,out minor)==0||major<1||(major==1&&minor<1))return null;
            var result=new X11WindowInput(window,display);display=0;return result;
        } catch(DllNotFoundException){return null;}
          catch(EntryPointNotFoundException){return null;}
        finally {if(display!=0)XCloseDisplay(display);}
    }
    List<nint> Surfaces() {
        var result=new List<nint>{handle};
        // Avalonia's GPU render surface is a direct child of its owned XID.
        if(XQueryTree(display,handle,out _,out _,out nint children,out uint count)==0)throw new InvalidOperationException("Cannot inspect own X11 surfaces");
        try {for(int i=0;i<count;i++)result.Add(Marshal.ReadIntPtr(children,i*IntPtr.Size));}
        finally {if(children!=0)XFree(children);}
        return result;
    }
    public void SetPassThrough(bool enabled) {
        Dispatcher.UIThread.VerifyAccess();
        if(display==0||!ReferenceEquals(window.PlatformImpl,backend))throw new ObjectDisposedException(nameof(X11WindowInput));
        if(applied==enabled)return;
        if(enabled) {
            var surfaces=Surfaces();
            try {
                nint empty=XFixesCreateRegion(display,0,0);
                try {foreach(var surface in surfaces){shaped.Add(surface);XFixesSetWindowShapeRegion(display,surface,2,0,0,empty);}}
                finally {XFixesDestroyRegion(display,empty);}
                // A WM-owned frame is a separate input target; remove it through our host.
                window.WindowDecorations=WindowDecorations.None;applied=true;
            } catch {Restore();throw;}
        } else {Restore();window.WindowDecorations=decorations;applied=false;}
        XSync(display,0);
    }
    void Restore() {
        var current=Surfaces();
        // Pulse creates these surfaces with default input shapes. Restore None,
        // not a snapshot rectangle, so later resizing keeps the whole window interactive.
        foreach(var surface in shaped)if(current.Contains(surface))XFixesSetWindowShapeRegion(display,surface,2,0,0,0);
        shaped.Clear();
    }
    public void Dispose() {
        Dispatcher.UIThread.VerifyAccess();
        if(display==0)return;
        // Closed ends the borrowed-handle lifetime; only free our own resources.
        shaped.Clear();XCloseDisplay(display);display=0;
    }
    const string X11="libX11.so.6",Fixes="libXfixes.so.3",Shape="libXext.so.6";
    [DllImport(X11)] static extern nint XOpenDisplay(string? name);
    [DllImport(X11)] static extern int XCloseDisplay(nint display);
    [DllImport(X11)] static extern int XFree(nint data);
    [DllImport(X11)] static extern int XSync(nint display,int discard);
    [DllImport(X11)] static extern int XQueryTree(nint display,nint window,out nint root,out nint parent,out nint children,out uint count);
    [DllImport(Fixes)] static extern int XFixesQueryExtension(nint display,out int eventBase,out int errorBase);
    [DllImport(Fixes)] static extern int XFixesQueryVersion(nint display,ref int major,ref int minor);
    [DllImport(Fixes)] static extern nint XFixesCreateRegion(nint display,nint rectangles,int count);
    [DllImport(Fixes)] static extern void XFixesDestroyRegion(nint display,nint region);
    [DllImport(Fixes)] static extern void XFixesSetWindowShapeRegion(nint display,nint window,int kind,int x,int y,nint region);
    [DllImport(Shape)] static extern int XShapeQueryVersion(nint display,out int major,out int minor);
}
