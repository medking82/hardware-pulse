using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.NetworkInformation;

namespace HardwarePulse {
    public struct MacNetworkCounters {
        public long Received,Sent;
    }

    // Exact interface selection; no aggregation, timer, shell or invented link speed.
    public sealed class MacNetworkReadings {
        readonly string name,source=Guid.NewGuid().ToString("N");
        readonly Func<MacNetworkCounters> read;
        readonly Func<double> seconds;
        readonly NetworkInterval interval=new NetworkInterval();
        long sequence;
        public MacNetworkReadings(string interfaceName)
            :this(interfaceName,()=>ResolveNative(interfaceName),()=>Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency){
            if(!OperatingSystem.IsMacOS())throw new PlatformNotSupportedException("macOS network statistics are required");
        }
        // Resolve once; the returned reader must acquire fresh counters on every call.
        public MacNetworkReadings(string interfaceName,Func<Func<MacNetworkCounters>> resolve,Func<double> monotonicSeconds)
            :this(interfaceName,CachedReader(resolve),monotonicSeconds){}
        static Func<MacNetworkCounters> CachedReader(Func<Func<MacNetworkCounters>> resolve){
            if(resolve==null)throw new ArgumentNullException("resolve");
            Func<MacNetworkCounters> selected=null;
            return ()=>{
                try{
                    if(selected==null)selected=resolve()??throw new IOException("Network interface unavailable");
                    return selected();
                }catch(Exception e) when(e is IOException||e is NetworkInformationException||e is UnauthorizedAccessException||e is FormatException){
                    selected=null;throw;
                }
            };
        }
        public MacNetworkReadings(string interfaceName,Func<MacNetworkCounters> read,Func<double> monotonicSeconds){
            if(string.IsNullOrWhiteSpace(interfaceName)||interfaceName.IndexOfAny(new[]{':','\r','\n',' ','\t'})>=0)
                throw new ArgumentException("An exact network interface name is required","interfaceName");
            name=interfaceName;this.read=read??throw new ArgumentNullException("read");
            seconds=monotonicSeconds??throw new ArgumentNullException("monotonicSeconds");
        }
        public Reading Read(DateTimeOffset now){
            var result=new Reading {time=now,identity=source+":"+(++sequence).ToString(CultureInfo.InvariantCulture),
                available=new Dictionary<string,bool>{{"netDown",false},{"netUp",false}}};
            result.names["Network"]=name;
            try{
                var counters=read();double current=seconds();
                if(counters.Received<0||counters.Sent<0)throw new FormatException("Invalid network byte counters");
                if(!double.IsFinite(current))throw new FormatException("Invalid monotonic clock");
                if(interval.Update((ulong)counters.Received,(ulong)counters.Sent,current,out double down,out double up)){
                    result.values["netDown"]=down;result.values["netUp"]=up;
                    result.available["netDown"]=result.available["netUp"]=true;result.state="LIVE";
                }
            }catch(Exception e) when(e is IOException||e is NetworkInformationException||e is UnauthorizedAccessException||e is FormatException){
                interval.Reset();result.error=e.Message;
            }
            return result;
        }
        static Func<MacNetworkCounters> ResolveNative(string name){
            foreach(var network in NetworkInterface.GetAllNetworkInterfaces()){
                if(network.Name!=name)continue;
                // BSD GetIPStatistics reads by Name each time. Do not reuse the stats
                // object or cached interface metadata (speed/status/address properties).
                return ()=>{
                    var stats=network.GetIPStatistics();
                    return new MacNetworkCounters {Received=stats.BytesReceived,Sent=stats.BytesSent};
                };
            }
            throw new IOException("Network interface unavailable");
        }
    }
}
