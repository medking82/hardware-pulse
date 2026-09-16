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
    static void Until(Func<bool> condition,string message) {
        var deadline=DateTime.UtcNow.AddSeconds(5);
        while(!condition()&&DateTime.UtcNow<deadline)Pump();
        Check(condition(),message);
    }
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
            Point Sample(){var point=above.PointToScreen(new Avalonia.Point(120,100));return new Point{X=point.X,Y=point.Y};}
            above.Activate();
            try {Until(()=>WindowFromPoint(Sample())==handle,"Native fixture is not above the underlying window");}
            finally {
                var p=Sample();Console.WriteLine($"INPUT_FIXTURE above={handle} below={below.TryGetPlatformHandle()!.Handle} hit={WindowFromPoint(p)} point={p.X},{p.Y} position={above.Position} client={above.ClientSize} scale={above.RenderScaling} active={above.IsActive}");
            }
            var sample=Sample();
            long before=GetWindowLongPtrW(handle,-20).ToInt64();
            input.SetPassThrough(true);
            Until(()=>WindowFromPoint(sample)==below.TryGetPlatformHandle()!.Handle,"Locked window still intercepts native hit testing");
            var dc=GetDC(0);try{Check(GetPixel(dc,sample.X,sample.Y)==0x0000ff,"Locked window lost its visible red content");}finally{ReleaseDC(0,dc);}
            input.SetPassThrough(true);
            input.SetPassThrough(false);
            Until(()=>WindowFromPoint(sample)==handle,"Unlock did not restore native hit testing");
            Check(GetWindowLongPtrW(handle,-20).ToInt64()==before,"Unlock changed unrelated styles");
            Console.WriteLine("PASS Windows input adapter: own-window boundary, native pass-through, idempotence and unlock restoration");
        } finally {above.Close();below.Close();}
    }
}
