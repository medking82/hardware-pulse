using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing=System.Drawing;

namespace HardwarePulse {
    // Capture only our bounds, excluding our own window. No images leave memory.
    public sealed class LocalContrast : IDisposable {
        [DllImport("user32.dll")] static extern bool SetWindowDisplayAffinity(IntPtr window,uint affinity);
        readonly Window window;IntPtr handle;bool excluded;
        byte[] previous;Rect previousBounds;
        float[] luminance,raw,scratch,history;
        static readonly double[] linear=LinearValues();
        static double[] LinearValues(){var result=new double[256];for(int i=0;i<256;i++)result[i]=DesktopContrast.Luminance(Color.FromRgb((byte)i,(byte)i,(byte)i));return result;}
        public LocalContrast(Window window){this.window=window;}
        public bool Enable(){
            if(excluded)return true;
            // Older Windows accepts 0x11 but substitutes a black rectangle.
            using(var key=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion")){
                int build;if(key==null||!int.TryParse(Convert.ToString(key.GetValue("CurrentBuildNumber")),out build)||build<19041)return false;
            }
            handle=new WindowInteropHelper(window).Handle;
            excluded=handle!=IntPtr.Zero&&SetWindowDisplayAffinity(handle,0x11);return excluded;
        }
        public static byte Select(double luminance,byte previous){
            // Hysteresis, not a fixed delay: large changes switch on the next frame.
            return luminance>.22?(byte)20:luminance<.16?(byte)245:previous==20?(byte)20:(byte)245;
        }
        public static byte RegionColor(BitmapSource image,Int32Rect region,byte previous,out double minority){
            minority=0;
            int left=Math.Max(0,region.X),top=Math.Max(0,region.Y),right=Math.Min(image.PixelWidth,region.X+region.Width),bottom=Math.Min(image.PixelHeight,region.Y+region.Height);
            if(right<=left||bottom<=top)return previous;
            int width=right-left,height=bottom-top;var pixels=new byte[width*height*4];image.CopyPixels(new Int32Rect(left,top,width,height),pixels,width*4,0);
            int dark=0;for(int i=0;i<pixels.Length;i+=4)if(pixels[i]<128)dark++;
            double share=dark/(double)(width*height);
            minority=Math.Min(share,1-share);
            return share>.55?(byte)20:share<.45?(byte)245:previous;
        }
        public static double Stabilize(double current,double previous){return Math.Abs(current-previous)>.18?current:previous*.65+current*.35;}
        public static void Smooth(float[] values,int width,int height,int radius,float[] temporary){
            // Two linear-time box passes remove sub-glyph texture without blurring text.
            radius=Math.Max(1,Math.Min(32,radius));
            for(int y=0;y<height;y++){
                double sum=0;int start=0,end=Math.Min(width-1,radius),offset=y*width;
                for(int x=start;x<=end;x++)sum+=values[offset+x];
                for(int x=0;x<width;x++){
                    int left=Math.Max(0,x-radius),right=Math.Min(width-1,x+radius);
                    while(start<left)sum-=values[offset+start++];while(end<right)sum+=values[offset+(++end)];
                    temporary[offset+x]=(float)(sum/(end-start+1));
                }
            }
            for(int x=0;x<width;x++){
                double sum=0;int start=0,end=Math.Min(height-1,radius);
                for(int y=start;y<=end;y++)sum+=temporary[y*width+x];
                for(int y=0;y<height;y++){
                    int top=Math.Max(0,y-radius),bottom=Math.Min(height-1,y+radius);
                    while(start<top)sum-=temporary[(start++)*width+x];while(end<bottom)sum+=temporary[(++end)*width+x];
                    values[y*width+x]=(float)(sum/(end-start+1));
                }
            }
        }
        public BitmapSource Capture(Color backing){
            if(!excluded||!window.IsVisible)return null;
            return Capture(Bounds(),backing);
        }
        public Rect Bounds(){
            var origin=window.PointToScreen(new Point());var end=window.PointToScreen(new Point(window.ActualWidth,window.ActualHeight));
            return new Rect(origin,new Size(Math.Ceiling(end.X-origin.X),Math.Ceiling(end.Y-origin.Y)));
        }
        public BitmapSource Capture(Rect bounds,Color backing,int radius=6){
            if(!excluded)return null;
            int width=(int)bounds.Width,height=(int)bounds.Height;
            if(width<1||height<1||(long)width*height>4000000)return null;
            using(var bitmap=new Drawing.Bitmap(width,height,Drawing.Imaging.PixelFormat.Format32bppArgb)){
                using(var graphics=Drawing.Graphics.FromImage(bitmap))graphics.CopyFromScreen((int)bounds.X,(int)bounds.Y,0,0,new Drawing.Size(width,height),Drawing.CopyPixelOperation.SourceCopy);
                var data=bitmap.LockBits(new Drawing.Rectangle(0,0,width,height),Drawing.Imaging.ImageLockMode.ReadOnly,Drawing.Imaging.PixelFormat.Format32bppArgb);
                byte[] pixels=new byte[width*height*4];
                try{for(int y=0;y<height;y++)Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),pixels,y*width*4,width*4);}finally{bitmap.UnlockBits(data);}
                bool reset=previous==null||previous.Length!=width*height||previousBounds!=bounds;
                if(previous==null||previous.Length!=width*height){previous=new byte[width*height];luminance=new float[width*height];raw=new float[width*height];scratch=new float[width*height];history=new float[width*height];}
                else if(reset)Array.Clear(previous,0,previous.Length);
                double alpha=backing.A/255d;
                for(int i=0,p=0;i<pixels.Length;i+=4,p++){
                    var color=Color.FromRgb((byte)(pixels[i+2]*(1-alpha)+backing.R*alpha),(byte)(pixels[i+1]*(1-alpha)+backing.G*alpha),(byte)(pixels[i]*(1-alpha)+backing.B*alpha));
                    raw[p]=luminance[p]=(float)(.2126*linear[color.R]+.7152*linear[color.G]+.0722*linear[color.B]);
                }
                Smooth(luminance,width,height,radius,scratch);
                for(int i=0,p=0;i<pixels.Length;i+=4,p++){
                    // Do not let spatial averaging erase glyphs at a strong dark/light edge.
                    if(raw[p]<.06&&luminance[p]>.16||raw[p]>.5&&luminance[p]<.22)luminance[p]=raw[p];
                    history[p]=reset?luminance[p]:(float)Stabilize(luminance[p],history[p]);
                    byte value=Select(history[p],previous[p]);previous[p]=value;
                    pixels[i]=pixels[i+1]=pixels[i+2]=value;pixels[i+3]=255;
                }
                previousBounds=bounds;var image=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,pixels,width*4);image.Freeze();return image;
            }
        }
        public void Dispose(){if(excluded)SetWindowDisplayAffinity(handle,0);excluded=false;}
    }
}
