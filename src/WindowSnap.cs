using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

public static class WindowSnap {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }

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
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd,int attribute,out int value,int size);

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
    public static void Attach(Window window) {
        IntPtr own=new WindowInteropHelper(window).Handle;

        HwndSource.FromHwnd(own).AddHook(delegate(IntPtr hwnd,int message,IntPtr w,IntPtr l,ref bool handled){
            // Snap only after the user releases the drag. Never constrain movement.
            if(message!=0x0232 || (Keyboard.Modifiers & ModifierKeys.Alt)!=0)return IntPtr.Zero;
            Rect rect;if(!GetWindowRect(own,out rect))return IntPtr.Zero;
            MonitorInfo monitor=new MonitorInfo();monitor.Size=Marshal.SizeOf(typeof(MonitorInfo));
            if(!GetMonitorInfo(MonitorFromRect(ref rect,2),ref monitor))return IntPtr.Zero;
            var targets=new List<Rect>();
            EnumWindows(delegate(IntPtr candidate,IntPtr state){
                if(candidate==own || !IsWindowVisible(candidate) || IsIconic(candidate) || GetWindowTextLength(candidate)==0)return true;
                int cloaked; if(DwmGetWindowAttribute(candidate,14,out cloaked,4)==0 && cloaked!=0)return true;
                Rect other;if(GetWindowRect(candidate,out other) && other.Right>other.Left && other.Bottom>other.Top)targets.Add(other);
                return true;
            },IntPtr.Zero);
            Rect snapped=Snap(rect,monitor.Work,targets,12);
            if(snapped.Left!=rect.Left || snapped.Top!=rect.Top)SetWindowPos(own,IntPtr.Zero,snapped.Left,snapped.Top,0,0,0x0015);
            return IntPtr.Zero;
        });
    }
}
