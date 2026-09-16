using HardwarePulse;

static class WindowsSystemTests {
    public static void Run() {
        var sample=new WindowsSystemSample(true,100,200,100,true,16UL<<30,4UL<<30);
        bool fail=false;
        var source=new WindowsSystemReadings(()=>fail?throw new IOException():sample);
        Reading Read()=>source.Read(DateTimeOffset.UtcNow);
        var first=Read();
        if(first.values.ContainsKey("cpuLoad")||first.usage["ram"].percent!=75||first.usage["ram"].used!=12)throw new Exception("Windows baseline/RAM units");
        sample=sample with {Idle=120,Kernel=250,User=150};
        if(Read().values["cpuLoad"]!=80)throw new Exception("Kernel idle counted twice");
        if(Read().values.ContainsKey("cpuLoad"))throw new Exception("Zero interval exposed");
        sample=sample with {Idle=1,Kernel=2,User=1};
        if(Read().values.ContainsKey("cpuLoad"))throw new Exception("Reset spike");
        fail=true;if(Read().state=="LIVE")throw new Exception("Failure retained live data");
        fail=false;if(Read().values.ContainsKey("cpuLoad"))throw new Exception("Failure did not reset baseline");
        sample=sample with {CpuValid=false,AvailableBytes=sample.TotalBytes+1};
        var absent=Read();if(absent.values.Count!=0||absent.usage.Count!=0)throw new Exception("Invalid counters exposed");
        sample=sample with {AvailableBytes=0};
        if(Read().usage["ram"].percent!=100)throw new Exception("Independent memory availability");
        if(OperatingSystem.IsWindows()) {
            var native=new WindowsSystemReadings();native.Read(DateTimeOffset.UtcNow);Thread.Sleep(200);
            var live=native.Read(DateTimeOffset.UtcNow);
            if(!live.usage.TryGetValue("ram",out var ram)||ram.total<=0||ram.percent<0||ram.percent>100
                ||!live.values.TryGetValue("cpuLoad",out double cpu)||cpu<0||cpu>100)throw new Exception("Native Windows CPU/RAM failed");
            Console.WriteLine("PASS native Windows system counters: CPU interval and physical RAM; no driver");
        }
        Console.WriteLine("PASS Windows system mapping: idle accounting, resets, missing/invalid counters and memory units");
    }
}
