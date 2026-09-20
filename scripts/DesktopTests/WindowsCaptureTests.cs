using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using HardwarePulse;

static class WindowsCaptureTests {
    [DllImport("user32.dll")] static extern nint GetDesktopWindow();
    [DllImport("user32.dll")] static extern bool GetWindowDisplayAffinity(nint window,out uint affinity);
    [DllImport("user32.dll")] static extern uint GetGuiResources(nint process,uint flags);
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
    static void Pump(){using var stop=new CancellationTokenSource(200);Dispatcher.UIThread.MainLoop(stop.Token);}
    public static void Native() {
        if(!OperatingSystem.IsWindows())return;
        Check(WindowsBackgroundCapture.Supported,"Capture acceptance requires Windows 10 2004+");
        bool refused=false;try{using var foreign=new WindowsBackgroundCapture(GetDesktopWindow());}catch(InvalidOperationException){refused=true;}
        Check(refused,"Capture accepted a foreign window");
        var below=new Window{Width=640,Height=480,Position=new PixelPoint(120,120),Background=Brushes.Blue,Topmost=true};
        var above=new Window{Width=400,Height=300,Position=new PixelPoint(160,160),Background=Brushes.Red,Topmost=true};
        below.Show();above.Show();Pump();
        nint hwnd=above.TryGetPlatformHandle()!.Handle;
        using var process=System.Diagnostics.Process.GetCurrentProcess();
        uint before=GetGuiResources(process.Handle,0);
        byte[]? buffer=null;
        try {
            using(var capture=new WindowsBackgroundCapture(hwnd)) {
                Check(!capture.Read(_=>throw new Exception("Disabled capture read pixels")),"Read worked before opt-in");
                Check(capture.Enable(),"Could not exclude own window");Pump();
                Check(GetWindowDisplayAffinity(hwnd,out uint affinity)&&affinity==0x11,"Missing capture exclusion");
                bool blue=false;string sample="no frame";
                for(int retry=0;retry<15&&!blue;retry++) {
                    capture.Read(frame=>{
                        buffer=frame.Pixels;int p=((frame.Height/2)*frame.Width+frame.Width/2)*4;
                        sample=$"BGR={frame.Pixels[p]},{frame.Pixels[p+1]},{frame.Pixels[p+2]}; grid={frame.Width}x{frame.Height}";
                        blue=frame.Pixels[p]>220&&frame.Pixels[p+1]<30&&frame.Pixels[p+2]<30;
                        Check(frame.Width*frame.Height<=ContrastAnalysis.MaximumPixels,"Capture grid exceeded analysis bound");
                    });if(!blue)Pump();
                }
                Check(blue,$"Capture did not see underlying blue fixture through excluded red window: {sample}; below={below.Position}/{below.Bounds}; above={above.Position}/{above.Bounds}; scale={above.RenderScaling}");
                Check(capture.Read(frame=>Check(ReferenceEquals(buffer,frame.Pixels),"Capture reallocated same-size buffer")),"Second frame unavailable");
                above.Hide();Check(!capture.Read(_=>throw new Exception("Hidden capture read pixels")),"Hidden capture ran");
                above.Show();Pump();above.Width=460;Pump();
                Check(capture.Read(frame=>Check(!ReferenceEquals(buffer,frame.Pixels),"Resize did not replace buffer")),"Resized capture unavailable");
                capture.Dispose();Check(!capture.Read(_=>throw new Exception("Disposed capture read pixels")),"Disposed capture ran");
                Check(GetWindowDisplayAffinity(hwnd,out affinity)&&affinity==0,"Screenshot visibility was not restored");
            }
            for(int i=0;i<15;i++)using(var capture=new WindowsBackgroundCapture(hwnd)){Check(capture.Enable(),"Resume failed");Check(capture.Read(_=>{}),"Resumed capture failed");}
            Check(GetGuiResources(process.Handle,0)<=before+3,"Capture/dispose leaked GDI handles");
            Check(buffer!=null&&buffer.All(x=>x==0),"Released pixel buffer retained captured content");
        }finally{above.Close();below.Close();}
        Console.WriteLine("PASS Windows background capture: self-exclusion, bounded reusable pixels, hidden/disabled rejection, resize, affinity restore and GDI lifetime");
    }
}
