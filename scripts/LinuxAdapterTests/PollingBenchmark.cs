using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using HardwarePulse;

static class PollingBenchmark {
    public static void Run(){
        if(!OperatingSystem.IsLinux())throw new PlatformNotSupportedException("Live polling measurement requires Linux");
        Console.WriteLine("BENCH metadata "+JsonSerializer.Serialize(new {
            arch=RuntimeInformation.ProcessArchitecture.ToString(),runtime=RuntimeInformation.FrameworkDescription,
            processors=Environment.ProcessorCount,statCharacters=File.ReadAllText("/proc/stat").Length,
            tieredCompilation=Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
            polls=2000,warmup=100,pairs=3
        }));
        for(int pair=0;pair<3;pair++){
            // Alternate order to expose rather than hide warm-cache/order effects.
            if(pair%2==0){Measure(pair,"full-stat",new LinuxReadings(File.ReadAllText));Measure(pair,"first-line",new LinuxReadings());}
            else{Measure(pair,"first-line",new LinuxReadings());Measure(pair,"full-stat",new LinuxReadings(File.ReadAllText));}
        }
    }
    static void Measure(int pair,string mode,LinuxReadings reader){
        for(int i=0;i<100;i++)reader.Read(DateTimeOffset.UtcNow);
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        using(var process=Process.GetCurrentProcess()){
            var cpuBefore=process.TotalProcessorTime;
            int gen0=GC.CollectionCount(0),gen1=GC.CollectionCount(1),gen2=GC.CollectionCount(2),invalid=0,cpuSamples=0;
            long before=GC.GetAllocatedBytesForCurrentThread(),start=Stopwatch.GetTimestamp();
            for(int i=0;i<2000;i++){
                var reading=reader.Read(DateTimeOffset.UtcNow);
                if(reading.error!=null||reading.state!="LIVE"||!reading.usage.ContainsKey("ram"))invalid++;
                if(reading.values.ContainsKey("cpuLoad"))cpuSamples++;
            }
            long end=Stopwatch.GetTimestamp(),allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            var cpu=process.TotalProcessorTime-cpuBefore;
            Console.WriteLine("BENCH poll "+JsonSerializer.Serialize(new {
                pair,mode,allocatedBytesPerPoll=allocated/2000.0,
                elapsedMsPerPoll=(end-start)*1000.0/Stopwatch.Frequency/2000,
                processCpuMsPerPoll=cpu.TotalMilliseconds/2000,
                gen0=GC.CollectionCount(0)-gen0,gen1=GC.CollectionCount(1)-gen1,gen2=GC.CollectionCount(2)-gen2,
                invalid,cpuSamples
            }));
            if(invalid!=0)throw new Exception("Polling benchmark encountered invalid live readings");
        }
    }
}
