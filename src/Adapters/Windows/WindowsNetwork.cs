using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Net.NetworkInformation;

namespace HardwarePulse {
    // Called serially by the collector; no independent sampling loop.
    public sealed class WindowsNetwork {
        readonly Func<string,string> identifier;
        public WindowsNetwork(Func<string,string> identifier){if(identifier==null)throw new ArgumentNullException("identifier");this.identifier=identifier;}
        DateTime nextAdapterDiscovery;
        Dictionary<string,bool> physicalAdapters=new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string,NetworkInterval> intervals=new Dictionary<string,NetworkInterval>(StringComparer.OrdinalIgnoreCase);
        // Driver-free counterpart to the hardware library's Throughput sensors.
        // Reuse Core interval/reset rules and the host's existing interface identity.
        public Sensor[] ReadThroughput(){
            var readings=new List<Sensor>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try{foreach(var adapter in NetworkInterface.GetAllNetworkInterfaces())try{
                if(adapter.OperationalStatus!=OperationalStatus.Up||adapter.NetworkInterfaceType==NetworkInterfaceType.Loopback)continue;
                var stats=adapter.GetIPStatistics();if(stats.BytesReceived<0||stats.BytesSent<0)continue;
                seen.Add(adapter.Id);NetworkInterval interval;
                if(!intervals.TryGetValue(adapter.Id,out interval)){interval=new NetworkInterval();intervals[adapter.Id]=interval;}
                double down,up;bool valid=interval.Update((ulong)stats.BytesReceived,(ulong)stats.BytesSent,Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency,out down,out up);
                string id=identifier(adapter.Id);
                readings.Add(new Sensor{id=id+"/throughput/0",hardwareId=id,hardware=adapter.Name,hardwareType="Network",name="Download Speed",type="Throughput",value=valid?(double?)down:null});
                readings.Add(new Sensor{id=id+"/throughput/1",hardwareId=id,hardware=adapter.Name,hardwareType="Network",name="Upload Speed",type="Throughput",value=valid?(double?)up:null});
            }catch(NetworkInformationException){intervals.Remove(adapter.Id);}catch(NotImplementedException){intervals.Remove(adapter.Id);}}
            catch(NetworkInformationException){intervals.Clear();}
            foreach(string id in new List<string>(intervals.Keys))if(!seen.Contains(id))intervals.Remove(id);
            return readings.ToArray();
        }
        public NetworkLink[] Read(){
            if(DateTime.UtcNow>=nextAdapterDiscovery){
                nextAdapterDiscovery=DateTime.UtcNow.AddSeconds(30);
                try{var found=new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase);
                    using(var query=new ManagementObjectSearcher("SELECT GUID, PhysicalAdapter FROM Win32_NetworkAdapter"))using(var rows=query.Get())
                        foreach(ManagementObject row in rows)using(row){var id=row["GUID"] as string;if(id!=null&&row["PhysicalAdapter"] is bool)found[id]=(bool)row["PhysicalAdapter"];}
                    physicalAdapters=found;
                }catch(ManagementException){}catch(UnauthorizedAccessException){}
            }
            var links=new List<NetworkLink>();try{foreach(var adapter in NetworkInterface.GetAllNetworkInterfaces())try{
                bool connected=adapter.OperationalStatus==OperationalStatus.Up;long speed=connected?adapter.Speed:0;
                bool wifi=adapter.NetworkInterfaceType==NetworkInterfaceType.Wireless80211;
                string kind=wifi?"Wi-Fi":adapter.NetworkInterfaceType==NetworkInterfaceType.Ethernet||adapter.NetworkInterfaceType==NetworkInterfaceType.GigabitEthernet||adapter.NetworkInterfaceType==NetworkInterfaceType.FastEthernetFx||adapter.NetworkInterfaceType==NetworkInterfaceType.FastEthernetT?"Ethernet":"Network";
                bool physical;bool? isPhysical=physicalAdapters.TryGetValue(adapter.Id,out physical)?(bool?)physical:null;
                links.Add(new NetworkLink{hardwareId=identifier(adapter.Id),connected=connected,bitsPerSecond=speed>0?(long?)speed:null,connectionType=kind,signalPercent=wifi&&connected?WifiSignal.Read(adapter.Id):null,physical=isPhysical});
            }catch(NetworkInformationException){}catch(NotImplementedException){}}catch(NetworkInformationException){}
            return links.ToArray();
        }
    }
}
