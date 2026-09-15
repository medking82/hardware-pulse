using System;

namespace HardwarePulse {
    // Platform-independent analysis. The caller owns capture, cadence and the BGRA buffer.
    // One instance processes frames serially; queries refer to its latest analyzed frame.
    public sealed class ContrastAnalysis {
        public const int MaximumPixels=160000;
        int width,height;byte[] previous;float[] luminance,raw,scratch,history;int[] integral;
        readonly double[] red=new double[256],green=new double[256],blue=new double[256];
        int backing;bool paletteReady;
        static readonly double[] linear=LinearValues();
        static double[] LinearValues(){var values=new double[256];for(int i=0;i<256;i++){double s=i/255d;values[i]=s<=.04045?s/12.92:Math.Pow((s+.055)/1.055,2.4);}return values;}
        public static int SampleStep(int width,int height){
            if(width<1||height<1)throw new ArgumentOutOfRangeException();
            int step=Math.Max(1,(int)Math.Ceiling(Math.Sqrt(width*(double)height/MaximumPixels)));
            while((((long)width+step-1)/step)*(((long)height+step-1)/step)>MaximumPixels)step++;
            return step;
        }
        public static byte Select(double luminance,byte previous){return luminance>.22?(byte)20:luminance<.16?(byte)245:previous==20?(byte)20:(byte)245;}
        public static double Stabilize(double current,double previous){return Math.Abs(current-previous)>.18?current:previous*.65+current*.35;}
        public static byte ChooseRegion(int dark,int count,byte previous,out double minority){
            minority=0;if(count<=0)return previous;double share=dark/(double)count;minority=Math.Min(share,1-share);
            return share>.55?(byte)20:share<.45?(byte)245:previous;
        }
        public byte RegionColor(int x,int y,int regionWidth,int regionHeight,byte prior,out double minority){
            minority=0;if(integral==null||regionWidth<=0||regionHeight<=0)return prior;
            int left=Math.Max(0,x),top=Math.Max(0,y),right=(int)Math.Min(width,(long)x+regionWidth),bottom=(int)Math.Min(height,(long)y+regionHeight);
            if(right<=left||bottom<=top)return prior;
            int stride=width+1;
            int dark=integral[bottom*stride+right]-integral[top*stride+right]-integral[bottom*stride+left]+integral[top*stride+left];
            return ChooseRegion(dark,(right-left)*(bottom-top),prior,out minority);
        }
        public void Analyze(byte[] pixels,int width,int height,int radius,byte r,byte g,byte b,byte a,bool reset){
            if(width<1||height<1||(long)width*height>MaximumPixels||pixels==null||pixels.Length<(long)width*height*4)throw new ArgumentException("Invalid contrast grid");
            int count=width*height;
            if(this.width!=width||this.height!=height){
                this.width=width;this.height=height;previous=new byte[count];luminance=new float[count];raw=new float[count];scratch=new float[count];history=new float[count];integral=new int[(width+1)*(height+1)];reset=true;
            }else if(reset)Array.Clear(previous,0,previous.Length);
            int key=r|(g<<8)|(b<<16)|(a<<24);
            if(!paletteReady||backing!=key){double alpha=a/255d;for(int i=0;i<256;i++){red[i]=.2126*linear[(byte)(i*(1-alpha)+r*alpha)];green[i]=.7152*linear[(byte)(i*(1-alpha)+g*alpha)];blue[i]=.0722*linear[(byte)(i*(1-alpha)+b*alpha)];}backing=key;paletteReady=true;}
            for(int p=0,i=0;p<count;p++,i+=4)raw[p]=luminance[p]=(float)(red[pixels[i+2]]+green[pixels[i+1]]+blue[pixels[i]]);
            Smooth(luminance,width,height,radius,scratch);
            int stride=width+1;
            for(int y=0,p=0,i=0;y<height;y++){
                int dark=0;for(int x=0;x<width;x++,p++,i+=4){
                    if(raw[p]<.06&&luminance[p]>.16||raw[p]>.5&&luminance[p]<.22)luminance[p]=raw[p];
                    history[p]=reset?luminance[p]:(float)Stabilize(luminance[p],history[p]);
                    byte value=Select(history[p],previous[p]);previous[p]=value;
                    pixels[i]=pixels[i+1]=pixels[i+2]=value;pixels[i+3]=255;
                    if(value<128)dark++;integral[(y+1)*stride+x+1]=integral[y*stride+x+1]+dark;
                }
            }
        }
        public static void Smooth(float[] values,int width,int height,int radius,float[] temporary){
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
    }
}
