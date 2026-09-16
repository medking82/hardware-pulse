using System;
using System.Runtime.InteropServices;

namespace HardwarePulse {
    // NSWindow is borrowed from the host; never retain, release or modify foreign windows.
    public sealed class MacWindowInput {
        const string ObjC="/usr/lib/libobjc.A.dylib";
        readonly IntPtr window;
        public MacWindowInput(IntPtr window) {
            if(!OperatingSystem.IsMacOS())throw new PlatformNotSupportedException();
            this.window=window;CheckOwner();
        }
        void CheckOwner() {
            if(!BoolMessage(objc_getClass("NSThread"),sel_registerName("isMainThread")))
                throw new InvalidOperationException("Window input changes require the main thread");
            var app=Message(objc_getClass("NSApplication"),sel_registerName("sharedApplication"));
            var windows=Message(app,sel_registerName("windows"));
            nuint count=CountMessage(windows,sel_registerName("count"));
            for(nuint i=0;i<count;i++)if(window!=IntPtr.Zero&&IndexMessage(windows,sel_registerName("objectAtIndex:"),i)==window)return;
            throw new InvalidOperationException("An owned live NSWindow is required");
        }
        public void SetPassThrough(bool enabled) {
            CheckOwner();
            SetBoolMessage(window,sel_registerName("setIgnoresMouseEvents:"),enabled);
            if(BoolMessage(window,sel_registerName("ignoresMouseEvents"))!=enabled)
                throw new InvalidOperationException("Native window did not apply mouse transparency");
        }
        [DllImport(ObjC)] static extern IntPtr objc_getClass(string name);
        [DllImport(ObjC)] static extern IntPtr sel_registerName(string name);
        [DllImport(ObjC,EntryPoint="objc_msgSend")] static extern IntPtr Message(IntPtr receiver,IntPtr selector);
        [DllImport(ObjC,EntryPoint="objc_msgSend")] static extern nuint CountMessage(IntPtr receiver,IntPtr selector);
        [DllImport(ObjC,EntryPoint="objc_msgSend")] static extern IntPtr IndexMessage(IntPtr receiver,IntPtr selector,nuint index);
        [DllImport(ObjC,EntryPoint="objc_msgSend")][return:MarshalAs(UnmanagedType.I1)] static extern bool BoolMessage(IntPtr receiver,IntPtr selector);
        [DllImport(ObjC,EntryPoint="objc_msgSend")] static extern void SetBoolMessage(IntPtr receiver,IntPtr selector,[MarshalAs(UnmanagedType.I1)]bool value);
    }
}
