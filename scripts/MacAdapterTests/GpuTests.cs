using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using HardwarePulse;

static class GpuTests {
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
    public static void Run(bool live) {
        IReadOnlyList<MacGpuSample> values=new[]{new MacGpuSample(10,"Apple GPU",25,null,10),new MacGpuSample(20,"Other GPU",null,0)};
        bool fail=false;
        var reader=new MacGpuReadings(()=>fail?throw new IOException("Fixture failure"):values);
        var session=new ReadingSession(reader.Read);session.Poll(DateTimeOffset.UtcNow);
        Check(session.Latest.values["gpu/10"]==25&&session.Latest.values["gpu/20"]==0,"GPU load and real idle, fallback property");
        Check(reader.Channels[0].Cores==10&&reader.Channels[1].Cores==null,"Measured GPU cores, unknown is not zero");
        values=new[]{new MacGpuSample(20,"Other GPU",101,double.NaN,-1),new MacGpuSample(10,"Apple GPU",null,5,10)};
        session.Poll(DateTimeOffset.UtcNow);
        Check(session.Latest.values["gpu/10"]==5&&session.Peaks["gpu/10"]==25&&!session.Latest.values.ContainsKey("gpu/20"),"Stable registry identity across order and invalid utilization");
        Check(reader.Channels[0].Cores==null,"Invalid GPU core count unavailable");
        fail=true;session.Poll(DateTimeOffset.UtcNow);
        Check(session.Latest.values.Count==0&&session.Latest.error!=null&&reader.Channels.Count==2,"Transient failure clears live values, keeps known devices");
        fail=false;values=Array.Empty<MacGpuSample>();session.Poll(DateTimeOffset.UtcNow);
        Check(reader.Channels.Count==0&&session.Latest.values.Count==0,"Unplugged devices removed");
        values=new[]{new MacGpuSample(10,"bad\nname",100,null),new MacGpuSample(10,"Duplicate",1,null),new MacGpuSample(0,"Missing ID",1,null)};
        session.Poll(DateTimeOffset.UtcNow);Check(reader.Channels.Count==1&&reader.Channels[0].Label=="GPU"&&session.Latest.values["gpu/10"]==100,"Duplicate/invalid identities and labels");
        values=Enumerable.Range(1,33).Select(i=>new MacGpuSample((ulong)i,"GPU",1,null)).ToArray();
        Check(reader.Read(DateTimeOffset.UtcNow).error!=null,"Bounded GPU enumeration");
        if(live) {
            var native=new MacGpuReadings();var watch=Stopwatch.StartNew();var result=native.Read(DateTimeOffset.UtcNow);
            Check(native.Channels.Count<=32&&result.values.All(x=>x.Value>=0&&x.Value<=100),"Native GPU values bounded");
            Console.WriteLine($"GPU_NATIVE devices={native.Channels.Count} readings={result.values.Count} coreCounts={native.Channels.Count(x=>x.Cores.HasValue)} error={result.error!=null} firstPollMs={watch.Elapsed.TotalMilliseconds:F2}");
            if(result.values.Count==0)Console.WriteLine("GPU utilization availability unverified on this runner");
            if(native.Channels.All(x=>!x.Cores.HasValue))Console.WriteLine("GPU core count availability unverified on this runner");
        } else if(!OperatingSystem.IsMacOS()) {
            bool rejected=false;try{new MacGpuReadings();}catch(PlatformNotSupportedException){rejected=true;}
            Check(rejected,"GPU native calls reject unsupported OS");
        }
        Console.WriteLine("PASS macOS GPU: stable device identity, bounds, missing values, real zero and core-count metadata");
    }
}
