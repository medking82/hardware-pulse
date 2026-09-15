using System;
using System.Collections.Generic;
using System.Management;
using System.Net.NetworkInformation;

namespace HardwarePulse {
    // Called serially by the collector; no independent sampling loop.
    public sealed class WindowsNetwork {
        readonly Func<string,string> identifier;
        public WindowsNetwork(Func<string,string> identifier){if(identifier==null)throw new ArgumentNullException("identifier");this.identifier=identifier;}
        DateTime nextAdapterDiscovery;
        Dictionary<string,bool> physicalAdapters=new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase);
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
