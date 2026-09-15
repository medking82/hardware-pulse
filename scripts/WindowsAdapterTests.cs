using System;
using System.Collections.Generic;
using HardwarePulse;

class WindowsAdapterTests {
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Main(){
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
