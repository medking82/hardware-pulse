using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.NetworkInformation;

namespace HardwarePulse {
    public struct BclNetworkCounters {
        public long Received,Sent;
    }

    // Exact interface selection; no aggregation, timer, shell or invented link speed.
    public sealed class BclNetworkReadings {
        readonly string name,source=Guid.NewGuid().ToString("N");
        readonly Func<BclNetworkCounters> read;
        readonly Func<double> seconds;
        readonly NetworkInterval interval=new NetworkInterval();
        long sequence;
        public BclNetworkReadings(string interfaceName)
            :this(interfaceName,()=>ResolveNative(interfaceName),()=>Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency){

        }
        // Resolve once; the returned reader must acquire fresh counters on every call.
        public BclNetworkReadings(string interfaceName,Func<Func<BclNetworkCounters>> resolve,Func<double> monotonicSeconds)
            :this(interfaceName,CachedReader(resolve),monotonicSeconds){}
        static Func<BclNetworkCounters> CachedReader(Func<Func<BclNetworkCounters>> resolve){
            if(resolve==null)throw new ArgumentNullException("resolve");
            Func<BclNetworkCounters> selected=null;
            return ()=>{
                try{
                    if(selected==null)selected=resolve()??throw new IOException("Network interface unavailable");
                    return selected();
                }catch(Exception e) when(e is IOException||e is NetworkInformationException||e is UnauthorizedAccessException||e is FormatException){
                    selected=null;throw;
                }
            };
        }
        public BclNetworkReadings(string interfaceName,Func<BclNetworkCounters> read,Func<double> monotonicSeconds){
            if(string.IsNullOrWhiteSpace(interfaceName)||interfaceName.Length>256||Array.Exists(interfaceName.ToCharArray(),char.IsControl))
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
        static Func<BclNetworkCounters> ResolveNative(string name){
            foreach(var network in NetworkInterface.GetAllNetworkInterfaces()){
                if(network.Name!=name)continue;
                // Acquire fresh OS counters each call. Do not reuse the stats
                // object or cached interface metadata (speed/status/address properties).
                return ()=>{
                    var stats=network.GetIPStatistics();
                    return new BclNetworkCounters {Received=stats.BytesReceived,Sent=stats.BytesSent};
                };
            }
            throw new IOException("Network interface unavailable");
        }
    }
}
