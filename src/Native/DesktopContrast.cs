using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace HardwarePulse {
    public static class DesktopContrast {
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr window,IntPtr dc);
        [DllImport("gdi32.dll")] static extern uint GetPixel(IntPtr dc,int x,int y);
        static double Linear(byte value){double s=value/255d;return s<=.04045?s/12.92:Math.Pow((s+.055)/1.055,2.4);}
        public static double Luminance(Color color){return .2126*Linear(color.R)+.7152*Linear(color.G)+.0722*Linear(color.B);}
        // Small hysteresis avoids flickering between dark/light on animated wallpapers.
        public static string Choose(double luminance,string previous){
            if(luminance>.22)return "#152127";
            if(luminance<.16)return "#F5F7FA";
            return previous=="#152127"?previous:"#F5F7FA";
        }
        public static Color Outline(Color foreground){return Luminance(foreground)>.4?Colors.Black:Colors.White;}
        public static double? Sample(Window window){
            if(!window.IsVisible||window.ActualHeight<24)return null;
            IntPtr dc=GetDC(IntPtr.Zero);if(dc==IntPtr.Zero)return null;
            try{
                double sum=0;int count=0;
                // Transparent padding, away from glyphs and rules: never sample our own text.
                foreach(double fraction in new[]{.2,.5,.8})foreach(double x in new[]{2,Math.Max(2,window.ActualWidth-2)}){
                    Point point=window.PointToScreen(new Point(x,window.ActualHeight*fraction));
                    uint pixel=GetPixel(dc,(int)point.X,(int)point.Y);if(pixel==uint.MaxValue)continue;
                    sum+=Luminance(Color.FromRgb((byte)pixel,(byte)(pixel>>8),(byte)(pixel>>16)));count++;
                }
                return count==0?(double?)null:sum/count;
            }finally{ReleaseDC(IntPtr.Zero,dc);}
        }
    }
}
