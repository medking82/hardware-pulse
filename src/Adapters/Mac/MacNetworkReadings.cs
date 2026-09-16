using System;

namespace HardwarePulse {
    public struct MacNetworkCounters {public long Received,Sent;}
    public sealed class MacNetworkReadings {
        readonly BclNetworkReadings inner;
        public MacNetworkReadings(string name) {
            if(!OperatingSystem.IsMacOS())throw new PlatformNotSupportedException("macOS network statistics are required");
            inner=new BclNetworkReadings(Validate(name));
        }
        public MacNetworkReadings(string name,Func<MacNetworkCounters> read,Func<double> seconds) {
            if(read==null)throw new ArgumentNullException("read");
            inner=new BclNetworkReadings(Validate(name),()=>Convert(read()),seconds);
        }
        public MacNetworkReadings(string name,Func<Func<MacNetworkCounters>> resolve,Func<double> seconds) {
            if(resolve==null)throw new ArgumentNullException("resolve");
            inner=new BclNetworkReadings(Validate(name),()=>{
                var selected=resolve();return selected==null?null:new Func<BclNetworkCounters>(()=>Convert(selected()));
            },seconds);
        }
        static string Validate(string name) {
            if(string.IsNullOrWhiteSpace(name)||name.IndexOfAny(new[]{':','\r','\n',' ','\t'})>=0)
                throw new ArgumentException("An exact network interface name is required","interfaceName");
            return name;
        }
        static BclNetworkCounters Convert(MacNetworkCounters value)=>new BclNetworkCounters {Received=value.Received,Sent=value.Sent};
        public Reading Read(DateTimeOffset now)=>inner.Read(now);
    }
}
