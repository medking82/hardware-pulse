using System;
using System.Diagnostics;
using System.Globalization;
using HardwarePulse;
internal static class FpsHistoryBench {
    static void Main(){
        AppDomain.MonitoringIsEnabled=true;var history=new FrameHistory();
        for(int i=0;i<14400;i++)history.Add("main",1000.0/240,i/240.0);
        for(int i=0;i<20;i++)history.ReadAt(60);
        using(var process=Process.GetCurrentProcess()){
            var cpu=process.TotalProcessorTime;var watch=Stopwatch.StartNew();
            long before=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
            for(int i=0;i<200;i++){var sample=history.ReadAt(60);if(!sample.Ready||sample.Count!=14400||Math.Abs(sample.Current-240)>0.001)throw new Exception("Invalid benchmark sample");}
            long allocated=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-before;
            watch.Stop();double used=(process.TotalProcessorTime-cpu).TotalMilliseconds;process.Refresh();
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,"{{\"cpuMsPerRead\":{0:F4},\"elapsedMsPerRead\":{1:F4},\"allocatedBytesPerRead\":{2},\"privateBytes\":{3},\"workingSet\":{4}}}",used/200,watch.Elapsed.TotalMilliseconds/200,allocated/200,process.PrivateMemorySize64,process.WorkingSet64));
        }
    }
}