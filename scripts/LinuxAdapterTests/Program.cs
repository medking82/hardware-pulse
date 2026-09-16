using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using HardwarePulse;

static class Program {
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Main(string[] args){
        string expected=Environment.GetEnvironmentVariable("PULSE_TEST_ARCH");
        Check(string.IsNullOrEmpty(expected)||expected==RuntimeInformation.ProcessArchitecture.ToString(),"Unexpected process architecture");
        Console.WriteLine(RuntimeInformation.OSDescription+" / "+RuntimeInformation.ProcessArchitecture);
        string stat="cpu 100 0 0 100 0 0 0 0 50 0\ncpu0 100 0 0 100 0 0 0 0 50 0";
        string mem="MemTotal: 8388608 kB\nMemFree: 0 kB\nMemAvailable: 2097152 kB";
        bool cpuFailure=false,ramFailure=false;
        var source=new LinuxReadings(path=>{
            if((path=="/proc/stat"&&cpuFailure)||(path=="/proc/meminfo"&&ramFailure))throw new IOException("Fixture unavailable");
            return path=="/proc/stat"?stat:mem;
        });
        var now=DateTimeOffset.UtcNow;
        var first=source.Read(now);
        Check(first.state=="LIVE"&&!first.values.ContainsKey("cpuLoad"),"First sample must warm up");
        Check(first.usage["ram"].used==6&&first.usage["ram"].total==8&&first.usage["ram"].percent==75,"Available memory and GiB mapping");
        stat="cpu 120 0 0 120 10 0 0 0 70 0";
        var second=source.Read(now);
        Check(second.values["cpuLoad"]==40,"Guest counted once and iowait excluded from busy");
        Check(first.identity!=second.identity&&second.time==now,"Distinct poll identity with host time");
        Check(!second.values.ContainsKey("cpu")&&second.gpuFanCount==0,"No fabricated temperature or fans");
        Check(!source.Read(now).values.ContainsKey("cpuLoad"),"Zero elapsed counters are unavailable, not zero load");
        stat="cpu 130 0 0 130 9 0 0 0";
        Check(!source.Read(now).values.ContainsKey("cpuLoad"),"Decreasing iowait resets baseline");
        stat="cpu 130 0 0 150 9 0 0 0";
        Check(source.Read(now).values["cpuLoad"]==0,"Idle interval is real zero");
        cpuFailure=true;
        Check(source.Read(now).usage.ContainsKey("ram"),"CPU failure preserves RAM");
        cpuFailure=false;
        Check(!source.Read(now).values.ContainsKey("cpuLoad"),"Recovery warms up");
        mem="MemTotal: 8388608 kB\nMemAvailable: 8388609 kB";
        Check(!source.Read(now).usage.ContainsKey("ram"),"Reject available above total");
        foreach(string bad in new[]{"MemTotal: 8 kB","MemTotal: 0 kB\nMemAvailable: 0 kB","MemTotal: 8 MB\nMemAvailable: 4 kB","MemTotal: 8 kB\nMemTotal: 8 kB\nMemAvailable: 4 kB"}){
            mem=bad;Check(!source.Read(now).usage.ContainsKey("ram"),"Reject malformed RAM");
        }
        ramFailure=true;stat="cpu nonsense";
        Check(source.Read(now).state=="OFFLINE","No valid metrics means offline");
        stat="cpu 1 0 0 1 0 0 0 0";source.Read(now);
        stat="cpu 2 0 0 1 0 0 0 0";
        Check(source.Read(now).values["cpuLoad"]==100,"RAM failure preserves CPU");
        if(Array.IndexOf(args,"--live")>=0){
            Check(OperatingSystem.IsLinux(),"Live test requires Linux");
            var live=new LinuxReadings();var session=new ReadingSession(live.Read);
            session.Poll(DateTimeOffset.UtcNow);Thread.Sleep(250);session.Poll(DateTimeOffset.UtcNow);
            var reading=session.Latest;
            Check(reading.state=="LIVE"&&reading.error==null&&session.HasUsage("ram"),"Live RAM/session integration");
            Check(reading.values.TryGetValue("cpuLoad",out double load)&&load>=0&&load<=100,"Live CPU interval");
            Check(reading.usage["ram"].total>0&&reading.usage["ram"].percent>=0&&reading.usage["ram"].percent<=100,"Live RAM bounds");
            Console.WriteLine("PASS Linux live CPU / RAM and Core ReadingSession");
        }else if(!OperatingSystem.IsLinux()){
            bool rejected=false;try{new LinuxReadings();}catch(PlatformNotSupportedException){rejected=true;}
            Check(rejected,"Reject live procfs on non-Linux host");
        }
        Console.WriteLine("PASS Linux adapter fixtures");
        HwmonTests.Run(Array.IndexOf(args,"--live")>=0);
        NetworkTests.Run(Array.IndexOf(args,"--live")>=0);
        CodexTests.Run();
        if(Array.IndexOf(args,"--measure")>=0)PollingBenchmark.Run();
    }
}
