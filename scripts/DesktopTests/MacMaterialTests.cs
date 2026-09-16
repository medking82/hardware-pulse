using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Threading;
using HardwarePulse.Desktop;

// Inspect only this test window's borrowed AppKit views; never alter framework-owned views.
static class MacMaterialTests {
    const string ObjC="/usr/lib/libobjc.A.dylib";
    [DllImport(ObjC)] static extern nint sel_registerName(string name);
    [DllImport(ObjC)] static extern nint objc_getClass(string name);
    [DllImport(ObjC,EntryPoint="objc_msgSend")] static extern nint Object(nint receiver,nint selector);
    [DllImport(ObjC,EntryPoint="objc_msgSend")] static extern nuint Number(nint receiver,nint selector);
    [DllImport(ObjC,EntryPoint="objc_msgSend")] static extern nint At(nint receiver,nint selector,nuint index);
    [DllImport(ObjC,EntryPoint="objc_msgSend")][return:MarshalAs(UnmanagedType.I1)] static extern bool Flag(nint receiver,nint selector);
    [DllImport(ObjC,EntryPoint="objc_msgSend")][return:MarshalAs(UnmanagedType.I1)] static extern bool IsKind(nint receiver,nint selector,nint type);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static IEnumerable<nint> Backdrops(nint view,int depth=0) {
        if(view==0||depth>12)yield break;
        if(IsKind(view,sel_registerName("isKindOfClass:"),objc_getClass("NSVisualEffectView"))&&Number(view,sel_registerName("blendingMode"))==0)yield return view;
        var children=Object(view,sel_registerName("subviews"));var count=Number(children,sel_registerName("count"));
        Check(count<256,"Unexpected native test view hierarchy");
        for(nuint i=0;i<count;i++)foreach(var child in Backdrops(At(children,sel_registerName("objectAtIndex:"),i),depth+1))yield return child;
    }
    static void Pump(){using var slice=new CancellationTokenSource(TimeSpan.FromMilliseconds(150));Dispatcher.UIThread.MainLoop(slice.Token);}
    public static void Run() {
        if(!OperatingSystem.IsMacOS())return;
        var window=new FloatingMonitorWindow(new UiLanguage("en"),new PreviewSettings{FloatingBackgroundOpacity=35});
        window.Show();Pump();
        try {
            var handle=window.TryGetPlatformHandle()!;Check(handle.HandleDescriptor=="NSWindow","Missing NSWindow material host");
            var content=Object(handle.Handle,sel_registerName("contentView"));
            bool Visible()=>Backdrops(content).Any(view=>!Flag(view,sel_registerName("isHiddenOrHasHiddenAncestor")));
            Check(!Visible(),"Blur disabled but a native backdrop remains visible");
            window.SetBackgroundBlur(true);Pump();
            Check(window.ActualTransparencyLevel==WindowTransparencyLevel.Blur&&Visible(),"macOS did not activate native behind-window blur");
            Check(window.MaterialStatus=="Background blur is active."&&window.Opacity==1,"Native material status/foreground mismatch");
            Check(window.SetLocked(true)&&window.SetLocked(false)&&Visible(),"Input lock altered native blur");
            window.SetBackgroundBlur(false);Pump();
            Check(!Visible()&&window.ActualTransparencyLevel==WindowTransparencyLevel.Transparent,"Blur disabled without restoring native transparency");
            Console.WriteLine("PASS macOS material: native NSVisualEffectView visibility, actual blur, opaque foreground and lock restoration");
        } finally {window.Close();}
    }
}
