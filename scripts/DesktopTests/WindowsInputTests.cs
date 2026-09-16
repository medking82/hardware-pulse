using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using HardwarePulse;

static class WindowsInputTests {
    [StructLayout(LayoutKind.Sequential)] struct Point {public int X,Y;}
    [DllImport("user32.dll")] static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] static extern nint GetDesktopWindow();
    [DllImport("user32.dll")] static extern nint GetWindowLongPtrW(nint window,int index);
    [DllImport("user32.dll")] static extern nint GetDC(nint window);
    [DllImport("user32.dll")] static extern int ReleaseDC(nint window,nint dc);
    [DllImport("gdi32.dll")] static extern uint GetPixel(nint dc,int x,int y);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Pump(){using var slice=new CancellationTokenSource(TimeSpan.FromMilliseconds(150));Dispatcher.UIThread.MainLoop(slice.Token);}
    public static void Run() {
        if(!OperatingSystem.IsWindows())return;
        bool refused=false;try{new WindowsWindowInput(GetDesktopWindow());}catch(InvalidOperationException){refused=true;}
        Check(refused,"Input adapter accepted a foreign window");
        var below=new Window{Width=300,Height=220,Position=new PixelPoint(160,160),Background=Brushes.Blue};
        var above=new Window{Width=300,Height=220,Position=new PixelPoint(160,160),Background=Brushes.Red,Topmost=true};
        below.Show();above.Show();Pump();
        var handle=above.TryGetPlatformHandle()!.Handle;
        var input=new WindowsWindowInput(handle);
        try {
            var point=above.PointToScreen(new Avalonia.Point(120,100));var sample=new Point{X=point.X,Y=point.Y};
            Check(WindowFromPoint(sample)==handle,"Native fixture is not above the underlying window");
            long before=GetWindowLongPtrW(handle,-20).ToInt64();
            input.SetPassThrough(true);Pump();
            Check(WindowFromPoint(sample)==below.TryGetPlatformHandle()!.Handle,"Locked window still intercepts native hit testing");
            var dc=GetDC(0);try{Check(GetPixel(dc,sample.X,sample.Y)==0x0000ff,"Locked window lost its visible red content");}finally{ReleaseDC(0,dc);}
            input.SetPassThrough(true);
            input.SetPassThrough(false);Pump();
            Check(WindowFromPoint(sample)==handle,"Unlock did not restore native hit testing");
            Check(GetWindowLongPtrW(handle,-20).ToInt64()==before,"Unlock changed unrelated styles");
            Console.WriteLine("PASS Windows input adapter: own-window boundary, native pass-through, idempotence and unlock restoration");
        } finally {above.Close();below.Close();}
    }
}
