using System;
using HardwarePulse;

class CoreTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static byte[] Frame(int width,int height,Func<int,int,byte> shade){var data=new byte[width*height*4];for(int y=0;y<height;y++)for(int x=0;x<width;x++){int i=(y*width+x)*4;data[i]=data[i+1]=data[i+2]=shade(x,y);data[i+3]=255;}return data;}
    static void Main(){
        Check(typeof(MaterialPolicy).Assembly==typeof(Reading).Assembly,"Material policy still depends on platform assembly");
        var lockedMaterial=new MaterialPolicy(20,true,false,false,false);
        Check(lockedMaterial.Clear&&lockedMaterial.EffectiveOpacity(true)==.05,"Locked monitor opacity changed");
        var settingsMaterial=new MaterialPolicy(20,true,true,false,false);
        var unlockedMaterial=new MaterialPolicy(20,false,false,false,false);
        Check(!settingsMaterial.Clear&&settingsMaterial.EffectiveOpacity(true)==.2&&unlockedMaterial.EffectiveOpacity(true)==.2,"Settings/unlock lost saved opacity");
        var zeroMaterial=new MaterialPolicy(0,false,false,false,false);
        Check(zeroMaterial.Clear&&zeroMaterial.EffectiveOpacity(true)==0,"Zero opacity lost transparency");
        Check(lockedMaterial.EffectiveOpacity(false)==1&&!lockedMaterial.CanAdjustOpacity(false),"Unsupported backdrop lost readable fallback");
        foreach(var material in new[]{new MaterialPolicy(0,true,false,true,false),new MaterialPolicy(0,true,false,false,true)})
            Check(material.Solid&&material.EffectiveOpacity(true)==1&&!material.CanAdjustOpacity(true),"Solid/high contrast must override transparent lock preferences");
        Console.WriteLine("PASS portable material policy: lock/settings/unlock, zero opacity and solid/high-contrast/unsupported fallback");
        CoreQuotaSessionTests.Run();
        CoreQuotaDecoderTests.Run();
        CoreFrameHistoryTests.Run();
        Check(typeof(QuotaReading).Assembly==typeof(ContrastAnalysis).Assembly&&typeof(NetworkRate).Assembly==typeof(ContrastAnalysis).Assembly,"Quota/network boundary depends on the app");
        var quota=new QuotaReading();
        quota.AllWindows.Add(new QuotaWindow {Label="5-hour",Remaining=0});
        Check(quota.Status=="Quota unavailable"&&quota.Windows.Count==0&&quota.AllWindows[0].Remaining==0&&!new QuotaWindow().Remaining.HasValue,"Quota defaults, separate window lists or unknown/zero semantics changed");
        var culture=System.Threading.Thread.CurrentThread.CurrentCulture;
        try {
            System.Threading.Thread.CurrentThread.CurrentCulture=System.Globalization.CultureInfo.InvariantCulture;
            Check(ReadingFormat.SensorNumber(54,"°C")=="54.0"&&ReadingFormat.SensorNumber(42,"%")=="42.0","Whole temperatures/utilization lost fixed decimal");
            Check(ReadingFormat.SensorNumber(45.96,"°C")=="46.0"&&ReadingFormat.SensorNumber(750.4," RPM")=="750"&&ReadingFormat.SensorNumber(99,"FPS")=="99","Sensor rounding or integer precision changed");
            Check(ReadingFormat.SensorNumber(1.234,"V")=="1.234"&&ReadingFormat.SensorNumber(1.234," V",true)=="1.2","View-specific voltage precision changed");
            Check(ReadingFormat.UsageText(new Usage{used=8,total=16,percent=50})=="8.0 / 16.0 GB · 50.0%","Usage precision changed");
            Check(NetworkRate.Link(0)=="Disconnected"&&NetworkRate.Link(5760000000)=="5.76 Gbit/s"&&NetworkRate.Link(65000000)=="65 Mbit/s","Link units or disconnected state changed");
            Check(NetworkRate.Format(1000000,"auto")=="1 MB/s"&&NetworkRate.Format(999000,"auto")=="999 KB/s"&&NetworkRate.Format(125000,"Mbit/s")=="1 Mbit/s","Network decimal units or auto threshold changed");
            Check(NetworkRate.Format(1250,"invalid")=="1.25 KB/s"&&NetworkRate.Format(1000000,"KB/s")=="1000 KB/s","Explicit unit or fallback changed");
            foreach(double invalid in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity,-1d})Check(NetworkRate.Link(invalid)=="—"&&NetworkRate.Format(invalid,"auto")=="—","Unavailable network value became a reading");
            System.Threading.Thread.CurrentThread.CurrentCulture=System.Globalization.CultureInfo.GetCultureInfo("de-DE");
            Check(ReadingFormat.SensorNumber(54,"°C")=="54,0"&&ReadingFormat.UsageText(new Usage{used=8,total=16,percent=50})=="8,0 / 16,0 GB · 50,0%","Reading formatting ignored host culture");
            Check(NetworkRate.Format(1250,"KB/s")=="1,25 KB/s"&&NetworkRate.Link(5760000000)=="5,76 Gbit/s","Host culture formatting changed");
        } finally {System.Threading.Thread.CurrentThread.CurrentCulture=culture;}
        Console.WriteLine("PASS portable quota contracts and network formatting: unknown vs zero, full/compact windows, units, invalid values and host culture");
        // Exercise the exact session assembly consumed by the app with no files or Windows adapter.
        Check(typeof(Reading).Assembly==typeof(ContrastAnalysis).Assembly&&typeof(ReadingSession).Assembly==typeof(ContrastAnalysis).Assembly,"Readings/session were not extracted into Core");
        var now=new DateTimeOffset(2026,9,15,0,0,0,TimeSpan.Zero);int calls=0;
        var next=new Reading();
        var session=new ReadingSession(time=>{Check(time==now,"Host time was not forwarded");calls++;return next;});
        session.Poll(now);Check(session.Latest.state=="OFFLINE"&&session.Peaks.Count==0,"Initial unavailable reading changed");
        next=new Reading {state="LIVE",identity="portable:1",available=new System.Collections.Generic.Dictionary<string,bool>{{"cpu",true}}};
        next.values["cpu"]=54;next.names["CPU"]="Portable CPU";next.usage["ram"]=new Usage {used=8,total=16,percent=50};
        session.Poll(now);Check(session.Peaks["cpu"]==54&&session.HasUsage("ram"),"Injected live reading did not establish history");
        next=new Reading {state="LIVE",identity="portable:1",available=session.Latest.available,names=session.Latest.names,usage=session.Latest.usage};next.values["cpu"]=90;
        session.Poll(now);Check(session.Peaks["cpu"]==54&&session.Latest.values["cpu"]==90,"Duplicate source identity changed peak policy");
        next=new Reading {state="STALE"};session.Poll(now);
        Check(session.Latest.values.Count==0&&session.Latest.available["cpu"]&&session.Latest.names["CPU"]=="Portable CPU"&&session.HasUsage("ram"),"Unavailable state lost capability metadata or retained live values");
        next=new Reading {state="LIVE",identity="portable:2",available=new System.Collections.Generic.Dictionary<string,bool>()};next.values["cpu"]=65;
        session.Poll(now);Check(session.Peaks["cpu"]==65&&!session.HasUsage("ram"),"Live recovery did not replace capabilities");
        Check(calls==5,"Session introduced extra sampling");
        var separate=new ReadingSession(time=>new Reading {state="OFFLINE"});separate.Poll(now);Check(separate.Peaks.Count==0&&session.Peaks["cpu"]==65,"Session history leaked across consumers");
        Console.WriteLine("PASS portable readings/session: injected source, host clock, duplicate identity, peaks, stale/recovery and independent state; no file adapter");
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
#if !NET10_0
        AppDomain.MonitoringIsEnabled=true;
#endif
        for(int i=0;i<10;i++)analysis.RegionColor(0,0,64,32,20,out minority);
        long before=AllocatedBytes();
        for(int i=0;i<10000;i++)analysis.RegionColor(0,0,64,32,20,out minority);
        Check(AllocatedBytes()-before<1024,"Region queries allocate per reading");
#if NET10_0
        var allowedReferences=new[]{"System.Runtime","System.Collections","System.Linq","System.Threading","System.Threading.Tasks","System.Memory"};
#else
        var allowedReferences=new[]{"mscorlib","System","System.Core"};
#endif
        foreach(var reference in typeof(ContrastAnalysis).Assembly.GetReferencedAssemblies())Check(Array.IndexOf(allowedReferences,reference.Name)>=0,"Core depends on unexpected assembly: "+reference.Name);
        Console.WriteLine("PASS Core: bounded analysis, local palette/edges, backing, hysteresis, clipped regions and allocation-free queries; no UI/OS assembly dependencies");
    }
    static long AllocatedBytes(){
#if NET10_0
        return GC.GetAllocatedBytesForCurrentThread();
#else
        return AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
#endif
    }
}
