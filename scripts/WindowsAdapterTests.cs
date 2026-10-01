using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using HardwarePulse;

class WindowsAdapterTests {
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static int Main(string[] args){
        if(Array.IndexOf(args,"--terminate_existing_session")>=0)return 0;
        if(Array.IndexOf(args,"--process_id")>=0){
            // Model a presenter whose display/GPU completion events are unavailable.
            // Application present timing remains available and needs no display tracking.
            if(Array.IndexOf(args,"--no_track_gpu")<0||Array.IndexOf(args,"--no_track_display")<0||Array.IndexOf(args,"--no_track_input")<0)return 17;
            int targetIndex=Array.IndexOf(args,"--process_id")+1;
            if(targetIndex>=args.Length||Array.IndexOf(args,"--v1_metrics")<0||Array.IndexOf(args,"--output_stdout")<0)return 18;
            Console.WriteLine("Application,ProcessID,SwapChainAddress,msBetweenPresents");
            Console.WriteLine("Presenter.exe,"+args[targetIndex]+",0x1,10.0");
            return 0;
        }
        if(IsAntigravityFakeChild(args)){
            Console.WriteLine("Gemini Models\tWeekly Limit Remaining\t0%\t2099-10-01T00:00:00Z");
            Console.WriteLine("Gemini Models\tFive Hour Limit Remaining\t100%\t2099-09-30T12:00:00+08:00");
            return string.Equals(Environment.GetEnvironmentVariable("AGY_CLI_DISABLE_AUTO_UPDATE"),"true",StringComparison.Ordinal)?0:17;
        }
        Check(WindowsCompatibility.RequiresDriverFreeCollector(new Version(6,1,7601)),"Win7 must not load PawnIO path");
        Check(WindowsCompatibility.RequiresDriverFreeCollector(new Version(6,3,9600)),"Win8.1 must not load PawnIO path");
        Check(!WindowsCompatibility.RequiresDriverFreeCollector(new Version(10,0,19045)),"Modern Windows lost hardware path");
        Check(!WindowsCompatibility.SupportsFpsCapture(new Version(6,1,7601)),"Win7 FPS capture must be unavailable");
        Check(WindowsCompatibility.SupportsFpsCapture(new Version(10,0,19045)),"Modern FPS capture gate regressed");
        Check(!WindowsCompatibility.SupportsCaptureExclusion(new Version(6,1,7601))&&!WindowsCompatibility.SupportsCaptureExclusion(new Version(10,0,18363)),"Legacy capture exclusion must not be enabled");
        Check(WindowsCompatibility.SupportsCaptureExclusion(new Version(10,0,19041)),"Capture exclusion boundary regressed");
        var systemSample=new WindowsSystemSample(true,100,200,100,true,16UL<<30,4UL<<30);
        var system=new WindowsSystemReadings(delegate{return systemSample;});
        Check(!system.Read(DateTimeOffset.UtcNow).values.ContainsKey("cpuLoad"),"Initial CPU baseline fabricated load");
        systemSample.Idle+=20;systemSample.Kernel+=50;systemSample.User+=50;
        var systemReading=system.Read(DateTimeOffset.UtcNow);
        Check(systemReading.values["cpuLoad"]==80&&systemReading.usage["ram"].percent==75,"Framework CPU/RAM counter mapping changed");
        Check(!system.Read(DateTimeOffset.UtcNow).values.ContainsKey("cpuLoad"),"Zero CPU interval fabricated load");
        Console.WriteLine("PASS Framework driver-free CPU/RAM and Windows version policy");
        CodexQuotaTests.Run();
        QuotaLoginFileTests.Run();
        ClaudeQuotaRequestTests.Run();
        TestAntigravityChildEnvironment();
        AntigravityEndpointTests.Run();
        string architecture=System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();
        string expected=Environment.GetEnvironmentVariable("PULSE_TEST_ARCH");
        Check(string.IsNullOrEmpty(expected)||expected==architecture,"Expected adapter process architecture "+expected+", got "+architecture);
        Console.WriteLine("Adapter test host: "+System.Runtime.InteropServices.RuntimeInformation.OSDescription+" / "+architecture+" / "+System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
        Check(typeof(FrameCapture).Assembly==typeof(SensorProfile).Assembly,"FPS capture did not enter adapter assembly");
        using(var presentCapture=new FrameCapture()){
            presentCapture.Start(Assembly.GetExecutingAssembly().Location,42);
            var wait=System.Diagnostics.Stopwatch.StartNew();
            while(!presentCapture.Read().Ready&&wait.ElapsedMilliseconds<3000)Thread.Sleep(20);
            var sample=presentCapture.Read();
            Check(sample.Ready&&sample.Count==1&&sample.Current==100,"Application FPS must remain available without GPU/display completion tracking");
        }
        Console.WriteLine("PASS actual capture launch and stdout parser with unavailable display/GPU events");
        var csv=FrameCapture.ParseCsv("\"Game, \"\"Demo\"\".exe\",42,0x1,16.0");
        Check(csv.Length==4&&csv[0]=="Game, \"Demo\".exe"&&csv[1]=="42","Quoted CSV fields changed");
        using(var capture=new FrameCapture()){
            capture.Reset(42);capture.Feed("Game.exe,42,0x1,16");
            Check(!capture.Read().Ready,"Headerless data must not become a frame");
            capture.Feed("Application,ProcessID,SwapChainAddress,MsBetweenPresents");
            capture.Feed("\"Game, Demo.exe\",42,0x1,16.0");
            capture.Feed("Other.exe,43,0x1,1.0");capture.Feed("Game.exe,42,0x1,NaN");capture.Feed("short,42");
            var reading=capture.Read();Check(reading.Ready&&reading.Count==1&&reading.Current==62.5,"CSV PID/invalid frame filtering changed");
            capture.Reset(43);Check(!capture.Read().Ready,"Target switch must clear history");
            capture.Feed("Game.exe,43,0x1,16");Check(!capture.Read().Ready,"Reset must require a fresh header");
            capture.Feed("Application,ProcessID,SwapChainAddress,Runtime,SyncInterval,PresentFlags,Dropped,TimeInSeconds,msInPresentAPI,msBetweenPresents");
            capture.Feed("Presenter.exe,43,0x2,DXGI,1,0,0,3.01,0.1,8.0");
            Check(capture.Read().Ready&&capture.Read().Current==125,"Actual present-only v1 schema must work without display/GPU columns");
            capture.Reset(43);
            capture.Add("main",20,10);Check(capture.ReadAt(10).Current==50&&!capture.ReadAt(12).Ready,"Core history bridge or freshness changed");
            capture.Dispose();Check(!capture.IsRunning&&capture.Read().Status=="FPS capture stopped","Dispose must retain stopped state");
        }
        Console.WriteLine("PASS Windows FPS adapter: assembly ownership, quoted CSV, PID filtering, reset, freshness and stopped state; no real PresentMon launched");
        Check(typeof(WindowsHardware).Assembly==typeof(SensorProfile).Assembly,"Hardware queries did not enter adapter assembly");
        var memory=WindowsHardware.ReadMemory();
        Check(memory!=null&&memory.totalGb>0&&memory.usedGb>=0&&memory.usedGb<=memory.totalGb,"Live physical memory query returned invalid capacity/usage");
        string memoryName;var modules=WindowsHardware.ReadMemoryModules(out memoryName);
        Check(!string.IsNullOrEmpty(memoryName),"Memory display name lost its fallback");
        foreach(var module in modules)Check(module.capacityGb>=0&&module.brand!=null&&module.part!=null&&module.slot!=null,"Invalid module metadata");
        foreach(var disk in WindowsHardware.ReadDisks())Check(disk.model!=null&&disk.volumes!=null,"Invalid disk metadata");
        Console.WriteLine("PASS Windows hardware adapter: live RAM bounds, optional module and disk metadata; no identifiers exported");
        Check(typeof(WindowsNetwork).Assembly==typeof(SensorProfile).Assembly,"Network sampling did not enter the adapter assembly");
        Check(WifiSignal.Read("not-an-interface-id")==null,"Invalid Wi-Fi interface fabricated a signal");
        var mapped=new HashSet<string>();
        var network=new WindowsNetwork(id=>{var value="test:"+id;mapped.Add(value);return value;});
        // Read-only live smoke check: no assumption that this machine has Wi-Fi or two links.
        for(int sample=0;sample<2;sample++){
            mapped.Clear();var links=network.Read();
            foreach(var link in links){
                Check(mapped.Contains(link.hardwareId),"Network identifier bypassed the host mapping");
                Check(link.connectionType=="Wi-Fi"||link.connectionType=="Ethernet"||link.connectionType=="Network","Unknown link classification");
                Check(!link.bitsPerSecond.HasValue||link.bitsPerSecond>0,"Unavailable link rate became zero/negative");
                Check(!link.signalPercent.HasValue||(link.signalPercent>=0&&link.signalPercent<=100&&link.connected&&link.connectionType=="Wi-Fi"),"Invalid signal capability");
            }
        }
        Console.WriteLine("PASS Windows network adapter: standalone assembly, host identifiers, optional links, speed/signal semantics and invalid Wi-Fi input");
        return 0;
    }

    static bool IsAntigravityFakeChild(string[] args){
        return args!=null&&args.Length==6&&args[0]=="--print"&&args[1]=="/usage"&&args[2]=="--print-timeout"&&args[3]=="15s"&&args[4]=="--log-file"&&args[5]=="NUL";
    }

    static void TestAntigravityChildEnvironment(){
        string previous=Environment.GetEnvironmentVariable("AGY_CLI_DISABLE_AUTO_UPDATE");
        try{
            Environment.SetEnvironmentVariable("AGY_CLI_DISABLE_AUTO_UPDATE","parent-sentinel");
            var type=typeof(QuotaProviders).Assembly.GetType("HardwarePulse.AntigravityCliQuota",true);
            var read=type.GetMethod("Read",BindingFlags.Static|BindingFlags.NonPublic);
            Check(read!=null,"Antigravity CLI quota reader was not found");
            QuotaReading reading=null;
            try{reading=(QuotaReading)read.Invoke(null,new object[]{System.Reflection.Assembly.GetExecutingAssembly().Location,CancellationToken.None});}
            catch(TargetInvocationException error){throw new Exception("Antigravity CLI child did not receive AGY_CLI_DISABLE_AUTO_UPDATE=true",error.InnerException??error);}
            Check(reading!=null&&reading.Status=="Live"&&reading.Source=="CLI","Antigravity CLI fake child did not return a live reading");
            Check(Environment.GetEnvironmentVariable("AGY_CLI_DISABLE_AUTO_UPDATE")=="parent-sentinel","Antigravity CLI changed the parent process environment");
        }finally{Environment.SetEnvironmentVariable("AGY_CLI_DISABLE_AUTO_UPDATE",previous);}
        Console.WriteLine("PASS Antigravity CLI child environment is scoped to the child");
    }
}
