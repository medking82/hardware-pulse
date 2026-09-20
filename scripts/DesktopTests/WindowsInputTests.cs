using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using HardwarePulse;

static class WindowsInputTests {
    [StructLayout(LayoutKind.Sequential)] struct Point {public int X,Y;}
    [DllImport("user32.dll")] static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] static extern nint GetAncestor(nint window,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(nint window,StringBuilder name,int count);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint window,out uint process);
    [StructLayout(LayoutKind.Sequential)] struct Rect {public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")] static extern bool GetWindowRect(nint window,out Rect rect);
    [DllImport("user32.dll")] static extern nint GetDesktopWindow();
    [DllImport("user32.dll")] static extern nint GetWindowLongPtrW(nint window,int index);
    [DllImport("user32.dll")] static extern nint GetDC(nint window);
    [DllImport("user32.dll")] static extern int ReleaseDC(nint window,nint dc);
    [DllImport("gdi32.dll")] static extern uint GetPixel(nint dc,int x,int y);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static nint HitWindow(Point point)=>GetAncestor(WindowFromPoint(point),2); // GA_ROOT includes native child surfaces.
    static string Describe(nint window) {
        var name=new StringBuilder(256);GetClassName(window,name,name.Capacity);GetWindowThreadProcessId(window,out uint process);GetWindowRect(window,out var rect);
        string processName="unavailable";
        try{using var owner=System.Diagnostics.Process.GetProcessById((int)process);processName=owner.ProcessName;}catch(ArgumentException){}catch(System.ComponentModel.Win32Exception){}
        return $"hwnd={window} root={GetAncestor(window,2)} class={name} pid={process} process={processName} rect={rect.Left},{rect.Top},{rect.Right},{rect.Bottom} exStyle={GetWindowLongPtrW(window,-20).ToInt64():X}";
    }
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
        var below=new Window{Width=300,Height=220,Position=new PixelPoint(160,160),Background=Brushes.Blue,Topmost=true};
        var above=new Window{Width=300,Height=220,Position=new PixelPoint(160,160),Background=Brushes.Red,Topmost=true};
        below.Show();above.Show();Pump();
        var handle=above.TryGetPlatformHandle()!.Handle;
        var input=new WindowsWindowInput(handle);
        try {
            Point Sample(){var point=above.PointToScreen(new Avalonia.Point(120,100));return new Point{X=point.X,Y=point.Y};}
            above.Activate();
            try {Until(()=>HitWindow(Sample())==handle,"Native fixture is not above the underlying window");}
            finally {
                var p=Sample();Console.WriteLine($"INPUT_FIXTURE above={handle} below={below.TryGetPlatformHandle()!.Handle} hit={WindowFromPoint(p)} point={p.X},{p.Y} position={above.Position} client={above.ClientSize} scale={above.RenderScaling} active={above.IsActive}");
                Console.WriteLine("INPUT_WINDOW above "+Describe(handle));
                Console.WriteLine("INPUT_WINDOW hit "+Describe(WindowFromPoint(p)));
            }
            var sample=Sample();
            long before=GetWindowLongPtrW(handle,-20).ToInt64();
            input.SetPassThrough(true);
            try{Until(()=>HitWindow(sample)==below.TryGetPlatformHandle()!.Handle,"Locked native hit target is not the underlying fixture");}
            finally {
                Console.WriteLine("INPUT_LOCKED hit "+Describe(WindowFromPoint(sample)));
                Console.WriteLine("INPUT_LOCKED below "+Describe(below.TryGetPlatformHandle()!.Handle));
            }
            // Hit testing can update before the compositor presents the changed
            // window style. Wait for the same exact pixel, without weakening it.
            Until(()=>{var dc=GetDC(0);try{return GetPixel(dc,sample.X,sample.Y)==0x0000ff;}finally{ReleaseDC(0,dc);}},"Locked window lost its visible red content");
            Check(HitWindow(sample)==below.TryGetPlatformHandle()!.Handle,"Visual recovery lost native pass-through");
            input.SetPassThrough(true);
            input.SetPassThrough(false);
            Until(()=>HitWindow(sample)==handle,"Unlock did not restore native hit testing");
            Check(GetWindowLongPtrW(handle,-20).ToInt64()==before,"Unlock changed unrelated styles");
            Console.WriteLine("PASS Windows input adapter: own-window boundary, native pass-through, idempotence and unlock restoration");
        } finally {above.Close();below.Close();}
    }
}
