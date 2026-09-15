using System;
using System.Collections.Generic;
using HardwarePulse;

class WindowsAdapterTests {
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Main(){
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
