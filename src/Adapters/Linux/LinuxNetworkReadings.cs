using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace HardwarePulse {
    // The host selects one interface and polls serially; never aggregate stacked links.
    public sealed class LinuxNetworkReadings {
        readonly string name,source=Guid.NewGuid().ToString("N");
        readonly Func<string> read;
        readonly Func<double> seconds;
        ulong received,sent;
        double timestamp;
        bool baseline;
        long sequence;

        public LinuxNetworkReadings(string interfaceName)
            :this(interfaceName,()=>File.ReadAllText("/proc/net/dev"),()=>Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency){
            if(!OperatingSystem.IsLinux())throw new PlatformNotSupportedException("Linux procfs is required");
        }
        public LinuxNetworkReadings(string interfaceName,Func<string> read,Func<double> monotonicSeconds){
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
                ulong rx=0,tx=0;bool found=false;
                using(var reader=new StringReader(read()??"")){
                    string line;
                    while((line=reader.ReadLine())!=null){
                        int colon=line.IndexOf(':');
                        if(colon<0||line.Substring(0,colon).Trim()!=name)continue;
                        if(found)throw new FormatException("Duplicate network interface");
                        var fields=line.Substring(colon+1).Split((char[])null,StringSplitOptions.RemoveEmptyEntries);
                        if(fields.Length!=16||!ulong.TryParse(fields[0],NumberStyles.None,CultureInfo.InvariantCulture,out rx)
                            ||!ulong.TryParse(fields[8],NumberStyles.None,CultureInfo.InvariantCulture,out tx))
                            throw new FormatException("Invalid network byte counters");
                        found=true;
                    }
                }
                if(!found)throw new FormatException("Network interface unavailable");
                double current=seconds();
                if(!double.IsFinite(current))throw new FormatException("Invalid monotonic clock");
                double elapsed=current-timestamp;
                if(baseline&&elapsed>0&&double.IsFinite(elapsed)&&rx>=received&&tx>=sent){
                    double down=(rx-received)/elapsed,up=(tx-sent)/elapsed;
                    if(double.IsFinite(down)&&double.IsFinite(up)){
                        result.values["netDown"]=down;result.values["netUp"]=up;
                        result.available["netDown"]=result.available["netUp"]=true;result.state="LIVE";
                    }
                }
                received=rx;sent=tx;timestamp=current;baseline=true;
            }catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is FormatException){
                baseline=false;result.error=e.Message;
            }
            return result;
        }
    }
}
