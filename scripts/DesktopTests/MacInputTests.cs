using System.Runtime.InteropServices;
using Avalonia.Controls;
using HardwarePulse;

static class MacInputTests {
    [DllImport("/usr/lib/libobjc.A.dylib")] static extern nint sel_registerName(string name);
    [DllImport("/usr/lib/libobjc.A.dylib",EntryPoint="objc_msgSend")]
    [return:MarshalAs(UnmanagedType.I1)] static extern bool ReadBool(nint window,nint selector);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run() {
        if(!OperatingSystem.IsMacOS())return;
        bool refused=false;try{new MacWindowInput(0);}catch(InvalidOperationException){refused=true;}
        Check(refused,"Mac input accepted a null handle");
        var window=new Window{Width=300,Height=220};window.Show();
        var handle=window.TryGetPlatformHandle()!;
        try {
            Check(handle.HandleDescriptor=="NSWindow","Expected borrowed NSWindow handle");
            using var input=new MacWindowInput(handle.Handle);
            window.Closed+=(_,_)=>input.Dispose();
            var getter=sel_registerName("ignoresMouseEvents");
            Check(!ReadBool(handle.Handle,getter),"New window unexpectedly ignores input");
            input.SetPassThrough(true);input.SetPassThrough(true);
            Check(ReadBool(handle.Handle,getter)&&window.IsVisible,"Lock failed or hid macOS window");
            input.SetPassThrough(false);
            Check(!ReadBool(handle.Handle,getter),"Unlock did not restore AppKit input");
            bool threadRefused=Task.Run(()=>{try{input.SetPassThrough(true);return false;}catch(InvalidOperationException){return true;}}).GetAwaiter().GetResult();
            Check(threadRefused&&!ReadBool(handle.Handle,getter),"Off-thread operation mutated AppKit window");
            window.Close();refused=false;try{input.SetPassThrough(true);}catch(InvalidOperationException){refused=true;}
            Check(refused,"Closed NSWindow was accepted");
            Console.WriteLine("PASS macOS input: native property round trip, visibility, main-thread and live-window boundaries");
        } finally {window.Close();}
    }
}
