using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using HardwarePulse;

static class Program {
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Main(string[] args){
        string expected=Environment.GetEnvironmentVariable("PULSE_TEST_ARCH");
        Check(string.IsNullOrEmpty(expected)||expected==RuntimeInformation.ProcessArchitecture.ToString(),"Unexpected process architecture");
        Console.WriteLine(RuntimeInformation.OSDescription+" / "+RuntimeInformation.ProcessArchitecture+" / "+RuntimeInformation.FrameworkDescription);
        var ticks=new MacCpuTicks {User=10,System=20,Idle=100,Nice=5};bool fail=false;
        var reader=new MacCpuReadings(()=>fail?throw new IOException("Fixture unavailable"):ticks);
        var now=DateTimeOffset.UtcNow;
        var first=reader.Read(now);
        Check(first.values.Count==0&&first.error==null,"First sample warms up");
        ticks=new MacCpuTicks {User=20,System=25,Idle=130,Nice=10};
        var next=reader.Read(now.AddDays(-1));
        Check(next.values["cpuLoad"]==40&&next.state=="LIVE","User/system/nice interval load independent of wall clock");
        Check(next.identity!=first.identity&&next.usage.Count==0&&!next.values.ContainsKey("cpu"),"Distinct identity; no invented RAM or temperature");
        Check(reader.Read(now).values.Count==0,"No elapsed counters unavailable");
        ticks.Idle+=10;Check(reader.Read(now).values["cpuLoad"]==0,"Idle is real zero");
        ticks.User+=10;Check(reader.Read(now).values["cpuLoad"]==100,"Fully busy interval");
        ticks.User=1;Check(reader.Read(now).values.Count==0,"Reset/wrap establishes new baseline");
        ticks.User=2;Check(reader.Read(now).values["cpuLoad"]==100,"Recover after reset");
        fail=true;Check(reader.Read(now).error!=null,"Source failure unavailable");fail=false;
        Check(reader.Read(now).values.Count==0,"Recovery needs baseline");
        ticks=new MacCpuTicks();reader.Read(now);
        ticks=new MacCpuTicks {User=uint.MaxValue,System=uint.MaxValue,Idle=uint.MaxValue,Nice=uint.MaxValue};
        Check(reader.Read(now).values["cpuLoad"]==75,"Counter sum does not overflow uint");
        if(Array.IndexOf(args,"--live")>=0){
            Check(OperatingSystem.IsMacOS(),"Live test requires macOS");
            var live=new MacCpuReadings();var session=new ReadingSession(live.Read);
            session.Poll(DateTimeOffset.UtcNow);Thread.Sleep(1000);session.Poll(DateTimeOffset.UtcNow);
            Check(session.Latest.error==null&&session.Latest.state=="LIVE","Native Mach result through Core session");
            Check(session.Latest.values.TryGetValue("cpuLoad",out double load)&&load>=0&&load<=100,"Live CPU bounds");
            for(int i=0;i<1000;i++)Check(live.Read(DateTimeOffset.UtcNow).error==null,"Repeated host port acquire/read/release");
            Console.WriteLine("PASS macOS live CPU / Core session / 1000 Mach acquire-release cycles");
        }else if(!OperatingSystem.IsMacOS()){
            bool rejected=false;try{new MacCpuReadings();}catch(PlatformNotSupportedException){rejected=true;}
            Check(rejected,"Reject native calls on unsupported OS");
        }
        Console.WriteLine("PASS macOS CPU fixtures");
        MemoryTests.Run(Array.IndexOf(args,"--live")>=0);
        NetworkTests.Run(Array.IndexOf(args,"--live")>=0);
        CodexTests.Run();
        ClaudeQuotaTests.Run();
        KeychainTests.Run();
        if(Array.IndexOf(args,"--measure")>=0)PollingBenchmark.Run();
    }
}
