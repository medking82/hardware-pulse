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
        readonly object gate=new object();
        readonly ContrastAnalysis analysis=new ContrastAnalysis();
        Rect previousBounds;
        Drawing.Bitmap bitmap,sampled;Drawing.Graphics captureGraphics,sampleGraphics;
        byte[] pixels;
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
        public static byte Select(double luminance,byte previous){return ContrastAnalysis.Select(luminance,previous);}
        public static double Stabilize(double current,double previous){return ContrastAnalysis.Stabilize(current,previous);}
        public static void Smooth(float[] values,int width,int height,int radius,float[] temporary){ContrastAnalysis.Smooth(values,width,height,radius,temporary);}
        public static byte RegionColor(BitmapSource image,Int32Rect region,byte previous,out double minority){
            minority=0;
            int left=Math.Max(0,region.X),top=Math.Max(0,region.Y),right=Math.Min(image.PixelWidth,region.X+region.Width),bottom=Math.Min(image.PixelHeight,region.Y+region.Height);
            if(right<=left||bottom<=top)return previous;
            int width=right-left,height=bottom-top;var pixels=new byte[width*height*4];image.CopyPixels(new Int32Rect(left,top,width,height),pixels,width*4,0);
            int dark=0;for(int i=0;i<pixels.Length;i+=4)if(pixels[i]<128)dark++;
            return ContrastAnalysis.ChooseRegion(dark,width*height,previous,out minority);
        }
        public byte RegionColor(Int32Rect region,byte previous,out double minority){
            lock(gate)return analysis.RegionColor(region.X,region.Y,region.Width,region.Height,previous,out minority);
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
            lock(gate){
                if(!excluded)return null;
                int width=(int)bounds.Width,height=(int)bounds.Height;
                if(width<1||height<1||(long)width*height>4000000)return null;
                int step=ContrastAnalysis.SampleStep(width,height),sampleWidth=(width+step-1)/step,sampleHeight=(height+step-1)/step;
                if(bitmap==null||bitmap.Width!=width||bitmap.Height!=height){
                    ReleaseBuffers();
                    bitmap=new Drawing.Bitmap(width,height,Drawing.Imaging.PixelFormat.Format32bppArgb);
                    captureGraphics=Drawing.Graphics.FromImage(bitmap);
                    sampled=new Drawing.Bitmap(sampleWidth,sampleHeight,Drawing.Imaging.PixelFormat.Format32bppArgb);
                    sampleGraphics=Drawing.Graphics.FromImage(sampled);
                    sampleGraphics.CompositingMode=Drawing.Drawing2D.CompositingMode.SourceCopy;
                    sampleGraphics.InterpolationMode=Drawing.Drawing2D.InterpolationMode.Bilinear;
                    sampleGraphics.PixelOffsetMode=Drawing.Drawing2D.PixelOffsetMode.Half;
                    pixels=new byte[sampleWidth*sampleHeight*4];
                }
                captureGraphics.CopyFromScreen((int)bounds.X,(int)bounds.Y,0,0,new Drawing.Size(width,height),Drawing.CopyPixelOperation.SourceCopy);
                Drawing.Bitmap source=bitmap;
                if(step>1){sampleGraphics.DrawImage(bitmap,new Drawing.Rectangle(0,0,sampleWidth,sampleHeight),0,0,width,height,Drawing.GraphicsUnit.Pixel);source=sampled;}
                var data=source.LockBits(new Drawing.Rectangle(0,0,sampleWidth,sampleHeight),Drawing.Imaging.ImageLockMode.ReadOnly,Drawing.Imaging.PixelFormat.Format32bppArgb);
                try{for(int y=0;y<sampleHeight;y++)Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),pixels,y*sampleWidth*4,sampleWidth*4);}finally{source.UnlockBits(data);}
                analysis.Analyze(pixels,sampleWidth,sampleHeight,Math.Max(1,(int)Math.Round(radius/(double)step)),backing.R,backing.G,backing.B,backing.A,previousBounds!=bounds);
                previousBounds=bounds;
                // BitmapSource copies the reusable buffer and is safe for the UI thread.
                var image=BitmapSource.Create(sampleWidth,sampleHeight,96,96,PixelFormats.Bgra32,null,pixels,sampleWidth*4);image.Freeze();return image;
            }
        }
        void ReleaseBuffers(){
            if(sampleGraphics!=null)sampleGraphics.Dispose();if(captureGraphics!=null)captureGraphics.Dispose();
            if(sampled!=null)sampled.Dispose();if(bitmap!=null)bitmap.Dispose();
            sampleGraphics=captureGraphics=null;sampled=bitmap=null;pixels=null;previousBounds=Rect.Empty;
        }
        public void Dispose(){lock(gate){if(excluded)SetWindowDisplayAffinity(handle,0);excluded=false;ReleaseBuffers();}}
    }
}
