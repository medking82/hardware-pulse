using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

public static class WindowSnap {
    public static readonly DependencyProperty PositionLockedProperty = DependencyProperty.RegisterAttached("PositionLocked",typeof(bool),typeof(WindowSnap),new PropertyMetadata(false));
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct CursorPoint { public int X,Y; }
    [DllImport("user32.dll")] static extern bool GetCursorPos(out CursorPoint point);
    public static Rect DragRect(Rect origin,int startX,int startY,int cursorX,int cursorY) {
        int dx=cursorX-startX,dy=cursorY-startY;
        origin.Left+=dx;origin.Right+=dx;origin.Top+=dy;origin.Bottom+=dy;
        return origin;
    }

    [StructLayout(LayoutKind.Sequential)] struct MonitorInfo { public int Size; public Rect Monitor,Work; public uint Flags; }
    delegate bool EnumProc(IntPtr hwnd, IntPtr state);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc proc, IntPtr state);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr hwnd);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromRect(ref Rect rect,uint flags);
    [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd,int attribute,out int value,int size);
    [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")] static extern int DwmGetFrame(IntPtr hwnd,int attribute,out Rect value,int size);

    internal static double ResolveDpiScale(Func<uint> readDpi,double wpfScale,ref bool nativeAvailable) {
        uint dpi=0;
        try{if(nativeAvailable)dpi=readDpi();}
        catch(EntryPointNotFoundException){nativeAvailable=false;}
        catch(DllNotFoundException){nativeAvailable=false;}
        if(dpi>0)return Math.Max(96,dpi)/96.0;
        return Double.IsNaN(wpfScale)||Double.IsInfinity(wpfScale)?1.0:Math.Max(1.0,wpfScale);
    }

    public static Rect Snap(Rect rect,Rect work,IEnumerable<Rect> windows,int threshold) {
        int dx=threshold+1,dy=threshold+1;
        Action<int> x=delegate(int delta){if(Math.Abs(delta)<=threshold && Math.Abs(delta)<Math.Abs(dx))dx=delta;};
        Action<int> y=delegate(int delta){if(Math.Abs(delta)<=threshold && Math.Abs(delta)<Math.Abs(dy))dy=delta;};
        x(work.Left-rect.Left);x(work.Right-rect.Right);
        y(work.Top-rect.Top);y(work.Bottom-rect.Bottom);
        foreach(Rect other in windows){
            // Nearby side-by-side windows also share top/bottom guides; stacked windows share left/right guides.
            int gapX=Math.Max(0,Math.Max(other.Left-rect.Right,rect.Left-other.Right));
            int gapY=Math.Max(0,Math.Max(other.Top-rect.Bottom,rect.Top-other.Bottom));
            if(gapX<=threshold*3 && rect.Bottom>other.Top && rect.Top<other.Bottom){
                y(other.Top-rect.Top);y(other.Bottom-rect.Bottom);
            }
            if(gapY<=threshold*3 && rect.Right>other.Left && rect.Left<other.Right){
                x(other.Left-rect.Left);x(other.Right-rect.Right);
            }
            if(rect.Bottom>other.Top && rect.Top<other.Bottom){
                x(other.Right-rect.Left);x(other.Left-rect.Right);
            }
            if(rect.Right>other.Left && rect.Left<other.Right){
                y(other.Bottom-rect.Top);y(other.Top-rect.Bottom);
            }
        }
        if(Math.Abs(dx)<=threshold){rect.Left+=dx;rect.Right+=dx;}
        if(Math.Abs(dy)<=threshold){rect.Top+=dy;rect.Bottom+=dy;}
        return rect;
    }
    public static void Attach(Window window, bool includeWindows=true, double edgePadding=0) {
        IntPtr own=new WindowInteropHelper(window).Handle;
        Rect dragOrigin=new Rect();CursorPoint dragStart=new CursorPoint();bool tracking=false;
        var targets=new List<Rect>();bool targetsReady=false;

        var source=HwndSource.FromHwnd(own);
        bool nativeDpiAvailable=true;
        Func<uint> readDpi=delegate{return GetDpiForWindow(own);};
        source.AddHook(delegate(IntPtr hwnd,int message,IntPtr w,IntPtr l,ref bool handled){
            if(message==0x0231){targetsReady=false;tracking=GetWindowRect(own,out dragOrigin) && GetCursorPos(out dragStart);return IntPtr.Zero;}
            if(message==0x0232){tracking=false;}
            if((bool)window.GetValue(PositionLockedProperty)) {
                long command=w.ToInt64() & 0xFFF0;
                if(message==0x0112 && (command==0xF010 || command==0xF000)){handled=true;return IntPtr.Zero;}
                if(message==0x0232)return IntPtr.Zero;
            }
            // Windows can rebase the proposed RECT after snapping. Anchor to the cursor
            // at drag start instead, so small pointer steps accumulate and release the magnet.
            bool moving=message==0x0216;
            if((!moving && message!=0x0232) || (Keyboard.Modifiers & ModifierKeys.Alt)!=0)return IntPtr.Zero;
            if((bool)window.GetValue(PositionLockedProperty))return IntPtr.Zero;
            Rect rect;
            if(moving){
                CursorPoint cursor;
                if(!tracking || !GetCursorPos(out cursor))return IntPtr.Zero;
                rect=DragRect(dragOrigin,dragStart.X,dragStart.Y,cursor.X,cursor.Y);
            }
            else if(!GetWindowRect(own,out rect))return IntPtr.Zero;
            MonitorInfo monitor=new MonitorInfo();monitor.Size=Marshal.SizeOf(typeof(MonitorInfo));
            if(!GetMonitorInfo(MonitorFromRect(ref rect,2),ref monitor))return IntPtr.Zero;
            if(!targetsReady){targets.Clear();if(includeWindows)EnumWindows(delegate(IntPtr candidate,IntPtr state){
                if(candidate==own || !IsWindowVisible(candidate) || IsIconic(candidate) || GetWindowTextLength(candidate)==0)return true;
                int cloaked; if(DwmGetWindowAttribute(candidate,14,out cloaked,4)==0 && cloaked!=0)return true;
                Rect other;bool found=DwmGetFrame(candidate,9,out other,Marshal.SizeOf(typeof(Rect)))==0;
                if(!found)found=GetWindowRect(candidate,out other);
                if(found && other.Right>other.Left && other.Bottom>other.Top)targets.Add(other);
                return true;
            },IntPtr.Zero);targetsReady=true;}
            // Win7 has system DPI through WPF, but no GetDpiForWindow export.
            double scale=ResolveDpiScale(readDpi,source.CompositionTarget==null?1.0:source.CompositionTarget.TransformToDevice.M11,ref nativeDpiAvailable);
            int threshold=(int)Math.Round((moving?24.0:4.0)*scale);
            int padding=(int)Math.Round(Math.Max(0,edgePadding)*scale);
            int insetX=Math.Min(padding,Math.Max(0,(monitor.Work.Right-monitor.Work.Left-(rect.Right-rect.Left))/2));
            int insetY=Math.Min(padding,Math.Max(0,(monitor.Work.Bottom-monitor.Work.Top-(rect.Bottom-rect.Top))/2));
            monitor.Work.Left+=insetX;monitor.Work.Right-=insetX;monitor.Work.Top+=insetY;monitor.Work.Bottom-=insetY;
            Rect snapped=Snap(rect,monitor.Work,targets,threshold);
            if(moving)snapped=Attract(rect,snapped,threshold);
            if(moving){Marshal.StructureToPtr(snapped,l,false);handled=true;return new IntPtr(1);}
            if(snapped.Left!=rect.Left || snapped.Top!=rect.Top)SetWindowPos(own,IntPtr.Zero,snapped.Left,snapped.Top,0,0,0x0015);
            return IntPtr.Zero;
        });
    }
    public static Rect Attract(Rect raw,Rect target,int threshold){
        Func<int,int> ease=delta=>{double t=Math.Max(0,1-Math.Abs(delta)/(double)Math.Max(1,threshold));return (int)Math.Round(delta*t*t*(3-2*t));};
        int dx=ease(target.Left-raw.Left),dy=ease(target.Top-raw.Top);
        raw.Left+=dx;raw.Right+=dx;raw.Top+=dy;raw.Bottom+=dy;return raw;
    }
}
