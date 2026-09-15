using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using HardwarePulse;

static class PollingBenchmark {
    const int Polls=1000;
    static void Log(string kind,object data){Console.WriteLine("BENCH "+kind+" "+JsonSerializer.Serialize(data));}
    static void Collect(){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();}
    public static void Run(){
        if(!OperatingSystem.IsMacOS())throw new PlatformNotSupportedException("Live polling measurement requires macOS");
        Log("metadata",new {arch=RuntimeInformation.ProcessArchitecture.ToString(),runtime=RuntimeInformation.FrameworkDescription,
            processors=Environment.ProcessorCount,interfaces=NetworkInterface.GetAllNetworkInterfaces().Length,
            tieredCompilation=Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),polls=Polls,warmup=100,rounds=3,pacedSeconds=60});
        for(int round=0;round<3;round++){
            Measure(round,"cpu",new MacCpuReadings().Read);
            Measure(round,"ram",new MacMemoryReadings().Read);
            Measure(round,"network",new MacNetworkReadings("lo0").Read);
        }
        Paced();
    }
    static void Measure(int round,string source,Func<DateTimeOffset,Reading> read){
        for(int i=0;i<100;i++)read(DateTimeOffset.UtcNow);
        Collect();
        using(var process=Process.GetCurrentProcess()){
            var cpuBefore=process.TotalProcessorTime;
            int gen0=GC.CollectionCount(0),gen1=GC.CollectionCount(1),gen2=GC.CollectionCount(2),invalid=0,available=0;
            long allocatedBefore=GC.GetAllocatedBytesForCurrentThread(),start=Stopwatch.GetTimestamp();
            for(int i=0;i<Polls;i++){
                var reading=read(DateTimeOffset.UtcNow);
                if(reading.error!=null||(source!="cpu"&&reading.state!="LIVE"))invalid++;
                if(reading.state=="LIVE")available++;
            }
            long end=Stopwatch.GetTimestamp(),allocated=GC.GetAllocatedBytesForCurrentThread()-allocatedBefore;
            var cpu=process.TotalProcessorTime-cpuBefore;
            Log("poll",new {round,source,allocatedBytesPerPoll=allocated/(double)Polls,
                elapsedMsPerPoll=(end-start)*1000.0/Stopwatch.Frequency/Polls,processCpuMsPerPoll=cpu.TotalMilliseconds/Polls,
                gen0=GC.CollectionCount(0)-gen0,gen1=GC.CollectionCount(1)-gen1,gen2=GC.CollectionCount(2)-gen2,invalid,available});
            if(invalid!=0)throw new Exception("Invalid live reading in polling measurement");
        }
    }
    static void Paced(){
        var sessions=new[]{new ReadingSession(new MacCpuReadings().Read),new ReadingSession(new MacMemoryReadings().Read),
            new ReadingSession(new MacNetworkReadings("lo0").Read)};
        for(int i=0;i<100;i++)foreach(var session in sessions)session.Poll(DateTimeOffset.UtcNow);
        // Buffer observations so console/JSON work is outside the timed window.
        var samples=new object[7];int invalid=0;long allocated=0;
        using(var process=Process.GetCurrentProcess()){
            Collect();long managedBefore=GC.GetTotalMemory(false);
            process.Refresh();long workingSetBefore=process.WorkingSet64;
            var cpuBefore=process.TotalProcessorTime;int gen0=GC.CollectionCount(0),gen1=GC.CollectionCount(1),gen2=GC.CollectionCount(2);
            var watch=Stopwatch.StartNew();
            samples[0]=new {seconds=0.0,workingSetBytes=workingSetBefore,managedBytes=managedBefore};
            for(int second=1;second<=60;second++){
                double wait=second*1000-watch.Elapsed.TotalMilliseconds;
                if(wait>0)Thread.Sleep(TimeSpan.FromMilliseconds(wait));
                long before=GC.GetAllocatedBytesForCurrentThread();
                var now=DateTimeOffset.UtcNow;
                foreach(var session in sessions){
                    session.Poll(now);
                    if(session.Latest.error!=null||session.Latest.state!="LIVE")invalid++;
                }
                allocated+=GC.GetAllocatedBytesForCurrentThread()-before;
                if(second%10==0){
                    process.Refresh();samples[second/10]=new {seconds=watch.Elapsed.TotalSeconds,
                        workingSetBytes=process.WorkingSet64,managedBytes=GC.GetTotalMemory(false)};
                }
            }
            double elapsed=watch.Elapsed.TotalSeconds,cpuMs=(process.TotalProcessorTime-cpuBefore).TotalMilliseconds;
            int collections0=GC.CollectionCount(0)-gen0,collections1=GC.CollectionCount(1)-gen1,collections2=GC.CollectionCount(2)-gen2;
            Collect();long managedAfterCollection=GC.GetTotalMemory(false);
            Log("paced",new {seconds=elapsed,pollsPerSource=60,invalid,processCpuMs=cpuMs,
                processCpuPercentOfMachine=cpuMs/(elapsed*1000*Environment.ProcessorCount)*100,
                adapterAndSessionAllocatedBytes=allocated,managedBefore,managedAfterCollection,
                gen0=collections0,gen1=collections1,gen2=collections2,samples});
            GC.KeepAlive(sessions);
            if(invalid!=0)throw new Exception("Invalid live reading in paced measurement");
        }
    }
}
