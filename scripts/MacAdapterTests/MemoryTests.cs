using System;
using System.IO;
using HardwarePulse;

static class MemoryTests {
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    public static void Run(bool live){
        var data=new MacMemoryCounters {TotalBytes=8UL*1073741824,PageSize=4096,InternalPages=1048576,
            PurgeablePages=262144,WiredPages=262144,CompressorPages=262144};
        bool fail=false;
        var reader=new MacMemoryReadings(()=>fail?throw new IOException("Fixture unavailable"):data);
        var now=DateTimeOffset.UtcNow;var result=reader.Read(now);
        Check(result.state=="LIVE"&&result.usage["ram"].used==5&&result.usage["ram"].total==8&&result.usage["ram"].percent==62.5,"Memory estimate/GiB mapping");
        Check(result.values.Count==0&&result.time==now,"No invented CPU/temperature");
        data.PageSize=16384;data.InternalPages/=4;data.PurgeablePages/=4;data.WiredPages/=4;data.CompressorPages/=4;
        Check(reader.Read(now).usage["ram"].used==5,"16 KiB kernel pages");
        var valid=data;
        data.PurgeablePages=data.InternalPages+1;Check(reader.Read(now).usage.Count==0,"Reject inconsistent purgeable counters");
        data=valid;data.TotalBytes=1;Check(reader.Read(now).error!=null,"Reject used beyond total");
        data=valid;data.PageSize=0;Check(reader.Read(now).error!=null,"Reject zero page size");
        data=valid;data.PageSize=3;Check(reader.Read(now).error!=null,"Reject non-power-of-two page size");
        data=valid;data.TotalBytes=0;Check(reader.Read(now).error!=null,"Reject missing physical memory");
        data=valid;fail=true;Check(reader.Read(now).state=="OFFLINE","Native failure unavailable");fail=false;
        Check(reader.Read(now).state=="LIVE","RAM recovery immediate");
        data=new MacMemoryCounters {TotalBytes=ulong.MaxValue,PageSize=16384,InternalPages=uint.MaxValue,WiredPages=uint.MaxValue,CompressorPages=uint.MaxValue};
        Check(reader.Read(now).usage["ram"].used==(double)uint.MaxValue*3*16384/1073741824,"No uint overflow in page sum");
        if(live){
            var native=new MacMemoryReadings();var session=new ReadingSession(native.Read);
            for(int i=0;i<100;i++){
                session.Poll(DateTimeOffset.UtcNow);var snapshot=session.Latest;
                Check(snapshot.state=="LIVE"&&snapshot.error==null&&session.HasUsage("ram"),"Live RAM through Core");
                var ram=snapshot.usage["ram"];
                Check(ram.total>0&&ram.used>=0&&ram.used<=ram.total&&ram.percent>=0&&ram.percent<=100,"Live RAM bounds");
            }
            Console.WriteLine("PASS macOS live RAM estimate / Core session / 100 native reads");
        }else if(!OperatingSystem.IsMacOS()){
            bool rejected=false;try{new MacMemoryReadings();}catch(PlatformNotSupportedException){rejected=true;}
            Check(rejected,"No native RAM calls on unsupported OS");
        }
        Console.WriteLine("PASS macOS RAM fixtures");
    }
}
