using System;
using System.Collections.Generic;
using HardwarePulse;

class WindowsAdapterTests {
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Main(){
        string architecture=System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();
        string expected=Environment.GetEnvironmentVariable("PULSE_TEST_ARCH");
        Check(string.IsNullOrEmpty(expected)||expected==architecture,"Expected adapter process architecture "+expected+", got "+architecture);
        Console.WriteLine("Adapter test host: "+System.Runtime.InteropServices.RuntimeInformation.OSDescription+" / "+architecture+" / "+System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
        Check(typeof(FrameCapture).Assembly==typeof(SensorProfile).Assembly,"FPS capture did not enter adapter assembly");
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
            capture.Add("main",20,10);Check(capture.ReadAt(10).Current==50&&!capture.ReadAt(12).Ready,"Core history bridge or freshness changed");
            capture.Dispose();Check(!capture.IsRunning&&capture.Read().Status=="FPS capture stopped","Dispose must retain stopped state");
        }
        Console.WriteLine("PASS Windows FPS adapter: assembly ownership, quoted CSV, PID filtering, reset, freshness and stopped state; no process launched");
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
    }
}
