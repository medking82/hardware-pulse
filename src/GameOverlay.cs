using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

public sealed class GameOverlay : Window {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct Pt { public int X,Y; }
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr hwnd,out Rect rect);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr hwnd,ref Pt point);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr hwnd,int index,IntPtr value);
    readonly TextBlock text = new TextBlock();
    public void SetAppearance(string hex,double opacity) {
        if(!System.Text.RegularExpressions.Regex.IsMatch(hex??"","^#[0-9a-fA-F]{6}$"))hex="#111923";
        if(double.IsNaN(opacity)||double.IsInfinity(opacity))opacity=80;
        var color=(Color)ColorConverter.ConvertFromString(hex);
        ((Border)Content).Background=new SolidColorBrush(Color.FromArgb((byte)Math.Round(255*Math.Max(0,Math.Min(100,opacity))/100),color.R,color.G,color.B));
    }
    public GameOverlay() {
        Title="Pulse Game Overlay"; WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize;
        AllowsTransparency=true; Background=Brushes.Transparent; Topmost=true; ShowInTaskbar=false; ShowActivated=false; Focusable=false;
        SizeToContent=SizeToContent.WidthAndHeight;
        text.Foreground=Brushes.White; text.FontFamily=new FontFamily("Segoe UI"); text.FontSize=14; text.TextWrapping=TextWrapping.Wrap;
        Content=new Border { Background=new SolidColorBrush(Color.FromArgb(205,17,25,35)), CornerRadius=new CornerRadius(8), Padding=new Thickness(12,8,12,8), Child=text };
        SourceInitialized += delegate {
            IntPtr hwnd=new WindowInteropHelper(this).Handle;
            long style=GetWindowLongPtr(hwnd,-20).ToInt64();
            SetWindowLongPtr(hwnd,-20,new IntPtr(style | 0x20 | 0x08000000 | 0x80));
            HwndSource.FromHwnd(hwnd).AddHook((IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled)=> {
                if(msg==0x21) { handled=true; return new IntPtr(3); }
                if(msg==0x84) { handled=true; return new IntPtr(-1); }
                return IntPtr.Zero;
            });
        };
    }
    public static Point Anchor(Rect bounds,double width,double height,string position) {
        double x=position.EndsWith("left") ? bounds.Left+12 : position.EndsWith("right") ? bounds.Right-width-12 : bounds.Left+(bounds.Right-bounds.Left-width)/2;
        double y=position.StartsWith("bottom") ? bounds.Bottom-height-12 : bounds.Top+12;
        return new Point(Math.Max(bounds.Left,x),Math.Max(bounds.Top,y));
    }
    public void Display(IntPtr target,string message,string position) {
        Rect rect;
        if(target==IntPtr.Zero || target!=GetForegroundWindow() || IsIconic(target) || !GetClientRect(target,out rect) || rect.Right<100 || rect.Bottom<100) { Hide(); return; }
        var origin=new Pt(); if(!ClientToScreen(target,ref origin)) { Hide(); return; }
        if(!IsVisible) Show();
        var source=PresentationSource.FromVisual(this);
        var matrix=source.CompositionTarget.TransformFromDevice;
        Point top=matrix.Transform(new Point(origin.X,origin.Y));
        Point end=matrix.Transform(new Point(origin.X+rect.Right,origin.Y+rect.Bottom));
        text.MaxWidth=Math.Max(50,end.X-top.X-48); text.Text=message;
        MaxHeight=Math.Max(50,end.Y-top.Y-24); UpdateLayout();
        var bounds=new Rect { Left=(int)top.X,Top=(int)top.Y,Right=(int)end.X,Bottom=(int)end.Y };
        var point=Anchor(bounds,ActualWidth,ActualHeight,position); Left=point.X; Top=point.Y;
    }
}
