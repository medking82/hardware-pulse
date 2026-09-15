using System;
using HardwarePulse;

class CoreTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static byte[] Frame(int width,int height,Func<int,int,byte> shade){var data=new byte[width*height*4];for(int y=0;y<height;y++)for(int x=0;x<width;x++){int i=(y*width+x)*4;data[i]=data[i+1]=data[i+2]=shade(x,y);data[i+3]=255;}return data;}
    static void Main(){
        foreach(var shape in new[]{new[]{480,1275},new[]{1600,2500},new[]{1,4000000},new[]{399,401},new[]{64,32}}){
            int step=ContrastAnalysis.SampleStep(shape[0],shape[1]);
            Check(((long)shape[0]+step-1)/step*(((long)shape[1]+step-1)/step)<=ContrastAnalysis.MaximumPixels,"Sampling budget exceeded");
        }
        var analysis=new ContrastAnalysis();double minority;
        var pixels=Frame(64,32,(x,y)=>(byte)(x<32?0:255));analysis.Analyze(pixels,64,32,6,0,0,0,0,true);
        Check(analysis.RegionColor(0,0,28,32,20,out minority)==245&&minority==0,"Dark background must use light text");
        Check(analysis.RegionColor(36,0,28,32,245,out minority)==20&&minority==0,"Light background must use dark text");
        Check(analysis.RegionColor(0,0,64,32,245,out minority)==245&&minority==.5,"Mixed background must preserve shade and expose edge need");
        Check(analysis.RegionColor(200,0,4,4,20,out minority)==20&&minority==0,"Out-of-bounds region must remain unchanged");
        // Independent direct pixel count checks the summed-area query, including clipped rectangles.
        var random=new Random(19);pixels=Frame(64,32,(x,y)=>(byte)random.Next(256));analysis.Analyze(pixels,64,32,3,20,29,38,100,true);
        for(int i=0;i<100;i++){
            int x=random.Next(-5,60),y=random.Next(-5,28),w=random.Next(1,30),h=random.Next(1,20),count=0,dark=0;
            for(int yy=Math.Max(0,y);yy<Math.Min(32,y+h);yy++)for(int xx=Math.Max(0,x);xx<Math.Min(64,x+w);xx++){count++;if(pixels[(yy*64+xx)*4]<128)dark++;}
            byte result=analysis.RegionColor(x,y,w,h,245,out minority);double share=count==0?0:dark/(double)count;
            Check(result==(count>0&&share>.55?20:245),"Region palette differs from direct histogram");
            Check(Math.Abs(minority-(count==0?0:Math.Min(share,1-share)))<1e-10,"Region edge confidence differs from direct histogram");
        }
        pixels=Frame(64,32,(x,y)=>(byte)0);analysis.Analyze(pixels,64,32,6,255,255,255,255,true);
        Check(analysis.RegionColor(0,0,64,32,245,out minority)==20,"Opaque white backing was ignored");
        pixels=Frame(64,32,(x,y)=>(byte)0);analysis.Analyze(pixels,64,32,6,0,0,0,0,false);
        Check(analysis.RegionColor(0,0,64,32,20,out minority)==245,"Strong change must switch on next frame");
        Check(ContrastAnalysis.Select(.19,20)==20&&ContrastAnalysis.Select(.19,245)==245,"Hysteresis lost");
        AppDomain.MonitoringIsEnabled=true;
        for(int i=0;i<10;i++)analysis.RegionColor(0,0,64,32,20,out minority);
        long before=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
        for(int i=0;i<10000;i++)analysis.RegionColor(0,0,64,32,20,out minority);
        Check(AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-before<1024,"Region queries allocate per reading");
        foreach(var reference in typeof(ContrastAnalysis).Assembly.GetReferencedAssemblies())Check(reference.Name=="mscorlib"||reference.Name=="System"||reference.Name=="System.Core","Core depends on platform assembly: "+reference.Name);
        Console.WriteLine("PASS Core: bounded analysis, local palette/edges, backing, hysteresis, clipped regions and allocation-free queries; no UI/OS assembly dependencies");
    }
}
