using HardwarePulse.Desktop;

static class MeasurementTests {
    public static void Run() {
        double clock=0;int captures=0;
        var observer=new AppMeasurement(true,()=>clock,()=>++captures==1
            ?new ProcessSample(5,1000,5000,6000,1,2,3):new ProcessSample(11,1100,6200,6100,3,2,3));
        var valid=new MonitorSnapshot("1%","1 GiB","1 B/s","1 B/s",true,true);
        clock=9;if(observer.Observe(valid)!=null||captures!=0)throw new Exception("Warm-up leaked into baseline");
        clock=10;if(observer.Observe(valid)!=null||captures!=1)throw new Exception("Baseline must capture once");
        clock=40;if(observer.Observe(new("—","—","—","—",false,false))!=null)throw new Exception("Measurement ended early");
        clock=70;var report=observer.Observe(valid)??throw new Exception("Missing measurement");
        if(report.Seconds!=60||report.Polls!=2||report.CpuReadyPolls!=1||report.MemoryReadyPolls!=1||report.NetworkReadyPolls!=1
            ||report.AllocatedBytes!=1200||report.WorkingSetStart!=1000||report.WorkingSetEnd!=1100
            ||report.Gen0!=2||report.Gen1!=0||report.Gen2!=0||Math.Abs(report.CpuPercentOfMachine-10.0/Environment.ProcessorCount)>0.00001)
            throw new Exception("Measurement units or sample accounting incorrect");
        clock=100;if(observer.Observe(valid)!=null||captures!=2)throw new Exception("Measurement must complete once");
        Console.WriteLine("PASS Desktop measurement: warm-up exclusion, elapsed/CPU units, availability and snapshot accounting");
    }
}
