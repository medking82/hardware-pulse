using System.Runtime.InteropServices;

namespace HardwarePulse;

// The pixel buffer is borrowed only for the serialized Read callback. Never persist it.
public readonly record struct WindowsCaptureFrame(byte[] Pixels,int Width,int Height,int X,int Y,int SourceWidth,int SourceHeight);

public sealed class WindowsBackgroundCapture : IDisposable {
    [StructLayout(LayoutKind.Sequential)] struct HighContrast {public uint Size,Flags;public nint Scheme;}
    [DllImport("user32.dll")] static extern bool SystemParametersInfoW(uint action,uint size,ref HighContrast value,uint flags);
    public static bool HighContrastActive {
        get {if(!OperatingSystem.IsWindows())return true;var value=new HighContrast{Size=(uint)Marshal.SizeOf<HighContrast>()};return !SystemParametersInfoW(0x42,value.Size,ref value,0)||(value.Flags&1)!=0;}
    }
    [StructLayout(LayoutKind.Sequential)] struct Rect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] struct Point {public int X,Y;}
    [StructLayout(LayoutKind.Sequential)] struct BitmapInfo {
        public uint Size;public int Width,Height;public ushort Planes,Bits;
        public uint Compression,ImageSize;public int XPels,YPels;public uint Used,Important;
    }
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint window,out uint process);
    [DllImport("user32.dll")] static extern bool GetWindowDisplayAffinity(nint window,out uint affinity);
    [DllImport("user32.dll")] static extern bool SetWindowDisplayAffinity(nint window,uint affinity);
    [DllImport("user32.dll")] static extern bool GetClientRect(nint window,out Rect rect);
    [DllImport("user32.dll")] static extern bool ClientToScreen(nint window,ref Point point);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] static extern nint GetDC(nint window);
    [DllImport("user32.dll")] static extern int ReleaseDC(nint window,nint dc);
    [DllImport("gdi32.dll")] static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] static extern nint CreateDIBSection(nint dc,ref BitmapInfo info,uint usage,out nint bits,nint section,uint offset);
    [DllImport("gdi32.dll")] static extern nint SelectObject(nint dc,nint obj);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")] static extern int SetStretchBltMode(nint dc,int mode);
    [DllImport("gdi32.dll")] static extern bool SetBrushOrgEx(nint dc,int x,int y,nint previous);
    [DllImport("gdi32.dll")] static extern bool StretchBlt(nint target,int x,int y,int width,int height,nint source,int sx,int sy,int sw,int sh,uint rop);
    [DllImport("gdi32.dll")] static extern bool GdiFlush();
    readonly nint handle;
    readonly object gate=new();
    nint dc,bitmap,previous,bits;
    byte[] pixels=[];
    int width,height;
    bool enabled,disposed;
    public static bool Supported=>OperatingSystem.IsWindowsVersionAtLeast(10,0,19041);
    public WindowsBackgroundCapture(nint handle) {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        this.handle=handle;
        if(!Owned())throw new InvalidOperationException("Background capture requires an owned live window.");
    }
    bool Owned()=>handle!=0&&GetWindowThreadProcessId(handle,out uint process)!=0&&process==(uint)Environment.ProcessId;
    public bool Enable() {
        lock(gate) {
            if(disposed||!Supported||!Owned())return false;
            if(enabled)return GetWindowDisplayAffinity(handle,out uint current)&&current==0x11;
            // Do not overwrite a different owner's affinity policy.
            if(!GetWindowDisplayAffinity(handle,out uint affinity)||affinity!=0)return false;
            return enabled=SetWindowDisplayAffinity(handle,0x11);
        }
    }
    public bool Read(Action<WindowsCaptureFrame> consume) {
        lock(gate) {
            if(disposed||!enabled||!Owned()||!IsWindowVisible(handle)||IsIconic(handle)||
                !GetWindowDisplayAffinity(handle,out uint affinity)||affinity!=0x11||!GetClientRect(handle,out var rect))return false;
            int sw=rect.Right-rect.Left,sh=rect.Bottom-rect.Top;
            var origin=new Point();
            if(sw<1||sh<1||(long)sw*sh>4000000||!ClientToScreen(handle,ref origin))return false;
            int step=ContrastAnalysis.SampleStep(sw,sh),w=(sw+step-1)/step,h=(sh+step-1)/step;
            nint screen=GetDC(0);if(screen==0)return false;
            try {
                if(!Buffers(screen,w,h))return false;
                if(!StretchBlt(dc,0,0,w,h,screen,origin.X,origin.Y,sw,sh,0x00CC0020)||!GdiFlush())return false;
                Marshal.Copy(bits,pixels,0,pixels.Length);
                consume(new(pixels,w,h,origin.X,origin.Y,sw,sh));return true;
            }finally{ReleaseDC(0,screen);}
        }
    }
    bool Buffers(nint screen,int w,int h) {
        if(dc!=0&&w==width&&h==height)return true;
        ReleaseBuffers();dc=CreateCompatibleDC(screen);if(dc==0)return false;
        var info=new BitmapInfo{Size=40,Width=w,Height=-h,Planes=1,Bits=32};
        bitmap=CreateDIBSection(screen,ref info,0,out bits,0,0);
        if(bitmap==0||bits==0){ReleaseBuffers();return false;}
        previous=SelectObject(dc,bitmap);
        if(previous==0||previous==-1){previous=0;ReleaseBuffers();return false;}
        SetStretchBltMode(dc,4);SetBrushOrgEx(dc,0,0,0);
        width=w;height=h;pixels=new byte[w*h*4];return true;
    }
    void ReleaseBuffers() {
        if(previous!=0)SelectObject(dc,previous);
        if(bitmap!=0)DeleteObject(bitmap);
        if(dc!=0)DeleteDC(dc);
        dc=bitmap=previous=bits=0;width=height=0;Array.Clear(pixels);pixels=[];
    }
    public void Dispose() {
        lock(gate) {
            if(disposed)return;disposed=true;
            if(enabled&&Owned()&&GetWindowDisplayAffinity(handle,out uint affinity)&&affinity==0x11)SetWindowDisplayAffinity(handle,0);
            enabled=false;ReleaseBuffers();
        }
    }
}
