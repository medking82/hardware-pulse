using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HardwarePulse.Desktop;

public sealed record ProcessSample(double CpuSeconds,long WorkingSet,long Allocated,long Managed,int Gen0,int Gen1,int Gen2) {
    public static ProcessSample Capture() {
        using var process=Process.GetCurrentProcess();process.Refresh();
        return new(process.TotalProcessorTime.TotalSeconds,process.WorkingSet64,
            GC.GetTotalAllocatedBytes(true),GC.GetTotalMemory(false),GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2));
    }
}

public sealed record AppMeasurementResult(int Schema,string Platform,string Architecture,bool Demo,int LogicalCpus,
    double Seconds,int Polls,int CpuReadyPolls,int MemoryReadyPolls,int NetworkReadyPolls,double CpuPercentOfMachine,
    long AllocatedBytes,long WorkingSetStart,long WorkingSetEnd,
    long ManagedStart,long ManagedEnd,int Gen0,int Gen1,int Gen2);

// Developer-only observer: two process snapshots, no forced GC and no extra poll loop.
public sealed class AppMeasurement {
    readonly Func<double> seconds;
    readonly Func<ProcessSample> capture;
    readonly bool demo;
    readonly double began;
    ProcessSample? baseline;
    double start;
    int polls,cpu,memory,network;
    bool complete;
    public AppMeasurement(bool demo,Func<double>? seconds=null,Func<ProcessSample>? capture=null) {
        this.demo=demo;this.seconds=seconds??(()=>Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency);
        this.capture=capture??ProcessSample.Capture;began=this.seconds();
    }
    public AppMeasurementResult? Observe(MonitorSnapshot snapshot) {
        if(complete)return null;
        double now=seconds();
        if(baseline==null) {
            if(now-began<10)return null;
            baseline=capture();start=now;return null;
        }
        polls++;if(snapshot.CpuReady)cpu++;if(snapshot.MemoryReady)memory++;
        if(snapshot.Download!="—"&&snapshot.Upload!="—")network++;
        if(now-start<60)return null;
        var end=capture();complete=true;double elapsed=now-start;
        return new(1,OperatingSystem.IsLinux()?"linux":OperatingSystem.IsMacOS()?"macos":"windows",
            RuntimeInformation.ProcessArchitecture.ToString(),demo,Environment.ProcessorCount,elapsed,polls,cpu,memory,network,
            (end.CpuSeconds-baseline.CpuSeconds)/elapsed/Environment.ProcessorCount*100,
            end.Allocated-baseline.Allocated,baseline.WorkingSet,end.WorkingSet,
            baseline.Managed,end.Managed,end.Gen0-baseline.Gen0,end.Gen1-baseline.Gen1,end.Gen2-baseline.Gen2);
    }
}
