using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using HardwarePulse.Desktop;

static class X11InputTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Pump(){using var slice=new CancellationTokenSource(TimeSpan.FromMilliseconds(20));Dispatcher.UIThread.MainLoop(slice.Token);}
    static void Until(Func<bool> predicate,string message){var end=DateTime.UtcNow.AddSeconds(3);while(!predicate()&&DateTime.UtcNow<end)Pump();Check(predicate(),message);}
    public static void Run() {
        if(!OperatingSystem.IsLinux())return;
        // Pointer movement is allowed only in the CI script's private Xvfb display.
        Check(Environment.GetEnvironmentVariable("PULSE_TEST_PRIVATE_X11")=="1","X11 input tests require the private Xvfb session script");
        var below=new Window{Width=400,Height=320,Position=new(100,100),Background=Brushes.Blue,Topmost=true};
        var above=new Window{Width=300,Height=220,Position=new(150,150),Background=Brushes.Red,Topmost=true};
        nint display=XOpenDisplay(null);Check(display!=0,"No X11 display");
        try {
            below.Show();above.Show();above.Activate();
            nint top=above.TryGetPlatformHandle()!.Handle,bottom=below.TryGetPlatformHandle()!.Handle,root=XDefaultRootWindow(display);
            using var input=X11WindowInput.TryCreate(above);Check(input!=null,"XFixes input adapter unavailable in fixture");
            above.Closed+=(_,_)=>input!.Dispose();
            var decorations=above.WindowDecorations;
            bool Hit(nint expected,int localX=120) {
                XTranslateCoordinates(display,top,root,localX,100,out int x,out int y,out _);
                XWarpPointer(display,0,root,0,0,0,0,x,y);XSync(display,0);
                nint current=root;
                for(int i=0;i<12;i++) {
                    if(current==expected)return true;
                    if(XQueryPointer(display,current,out _,out nint child,out _,out _,out _,out _,out _)==0||child==0)return false;
                    current=child;
                }
                return false;
            }
            Until(()=>Hit(top),"Unlocked X11 fixture does not receive pointer input");
            input!.SetPassThrough(true);input.SetPassThrough(true);
            Until(()=>Hit(bottom),"Locked X11 window still intercepts pointer input");
            Check(above.IsVisible&&above.WindowDecorations==WindowDecorations.None,"Lock hid content or retained interactive frame");
            // Read the composed screen, not a render-tree assertion: red content
            // must remain visible while native pointer input reaches blue below.
            XTranslateCoordinates(display,top,root,120,100,out int px,out int py,out _);
            nint image=XGetImage(display,root,px,py,1,1,nuint.MaxValue,2);
            Check(image!=0,"Could not read Xvfb composed pixel");
            try {Check((XGetPixel(image,0,0)&0xffffff)==0xff0000,"Locked X11 content was not visible");} finally {XDestroyImage(image);}
            input.SetPassThrough(false);
            Until(()=>Hit(top),"Unlock did not restore X11 pointer input");
            Check(above.WindowDecorations==decorations,"Unlock lost original frame");
            above.Width=380;
            Until(()=>Hit(top,350),"Restored input region did not grow after resize");
            above.Close();bool rejected=false;try{input.SetPassThrough(true);}catch(ObjectDisposedException){rejected=true;}
            Check(rejected,"Closed X11 adapter accepted input operation");
            Console.WriteLine("PASS X11 input: underlying native pointer target, visible content, restored frame/input and closed lifetime");
        } finally {above.Close();below.Close();XCloseDisplay(display);}
    }
    const string X11="libX11.so.6";
    [DllImport(X11)] static extern nint XOpenDisplay(string? name);
    [DllImport(X11)] static extern int XCloseDisplay(nint display);
    [DllImport(X11)] static extern nint XDefaultRootWindow(nint display);
    [DllImport(X11)] static extern int XTranslateCoordinates(nint display,nint source,nint destination,int x,int y,out int dx,out int dy,out nint child);
    [DllImport(X11)] static extern int XWarpPointer(nint display,nint source,nint destination,int sx,int sy,uint width,uint height,int x,int y);
    [DllImport(X11)] static extern int XQueryPointer(nint display,nint window,out nint root,out nint child,out int rx,out int ry,out int wx,out int wy,out uint mask);
    [DllImport(X11)] static extern int XSync(nint display,int discard);
    [DllImport(X11)] static extern nint XGetImage(nint display,nint drawable,int x,int y,uint width,uint height,nuint mask,int format);
    [DllImport(X11)] static extern nuint XGetPixel(nint image,int x,int y);
    [DllImport(X11)] static extern int XDestroyImage(nint image);
}
