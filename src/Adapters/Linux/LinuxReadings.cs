using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace HardwarePulse {
    // A host polls one instance serially. No process, timer or privileged access.
    // procfs reports host memory, not a container's cgroup memory allowance.
    public sealed class LinuxReadings {
        readonly Func<string,string> read;
        readonly string source=Guid.NewGuid().ToString("N");
        ulong[] previous;
        long sequence;

        public LinuxReadings() {
            if(!OperatingSystem.IsLinux())throw new PlatformNotSupportedException("Linux procfs is required");
            read=ReadProc;
        }
        public LinuxReadings(Func<string,string> read) {
            this.read=read??throw new ArgumentNullException("read");
        }

        static string ReadProc(string path) {
            if(path!="/proc/stat")return File.ReadAllText(path);
            // Only the aggregate is consumed; per-core and interrupt rows can be large.
            using(var reader=File.OpenText(path))return reader.ReadLine();
        }

        public Reading Read(DateTimeOffset now) {
            var result=new Reading {time=now,identity=source+":"+(++sequence).ToString(CultureInfo.InvariantCulture),
                available=new Dictionary<string,bool>{{"cpuLoad",false}}};
            var errors=new List<string>();
            try {
                var current=CpuCounters(read("/proc/stat"));
                if(previous!=null){
                    double total=0,idle=0;bool reset=false;
                    for(int i=0;i<8;i++){
                        if(current[i]<previous[i]){reset=true;break;}
                        double delta=current[i]-previous[i];total+=delta;
                        if(i==3||i==4)idle+=delta;
                    }
                    if(!reset&&total>0){
                        result.values["cpuLoad"]=100*(total-idle)/total;
                        result.available["cpuLoad"]=true;
                    }
                }
                previous=current;
            }catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is FormatException){
                previous=null;errors.Add("CPU: "+e.Message);
            }
            try {result.usage["ram"]=Memory(read("/proc/meminfo"));}
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is FormatException){errors.Add("RAM: "+e.Message);}
            if(result.values.Count>0||result.usage.Count>0)result.state="LIVE";
            if(errors.Count>0)result.error=string.Join("; ",errors);
            return result;
        }

        static ulong[] CpuCounters(string text) {
            using(var reader=new StringReader(text??"")){
                string line=reader.ReadLine();
                var fields=(line??"").Split((char[])null,StringSplitOptions.RemoveEmptyEntries);
                if(fields.Length<9||fields[0]!="cpu")throw new FormatException("Missing aggregate CPU counters");
                // guest / guest_nice are already included in user / nice; do not add twice.
                var counters=new ulong[8];
                for(int i=0;i<8;i++)if(!ulong.TryParse(fields[i+1],NumberStyles.None,CultureInfo.InvariantCulture,out counters[i]))
                    throw new FormatException("Invalid CPU counter");
                return counters;
            }
        }

        static Usage Memory(string text) {
            ulong? total=null,available=null;
            using(var reader=new StringReader(text??"")){
                string line;
                while((line=reader.ReadLine())!=null){
                    if(!line.StartsWith("MemTotal:",StringComparison.Ordinal)&&!line.StartsWith("MemAvailable:",StringComparison.Ordinal))continue;
                    var fields=line.Split((char[])null,StringSplitOptions.RemoveEmptyEntries);ulong value;
                    if(fields.Length!=3||fields[2]!="kB"||!ulong.TryParse(fields[1],NumberStyles.None,CultureInfo.InvariantCulture,out value))
                        throw new FormatException("Invalid memory counter");
                    if(fields[0]=="MemTotal:"){
                        if(total.HasValue)throw new FormatException("Duplicate MemTotal");total=value;
                    }else{
                        if(available.HasValue)throw new FormatException("Duplicate MemAvailable");available=value;
                    }
                }
            }
            if(!total.HasValue||!available.HasValue||total.Value==0||available.Value>total.Value)
                throw new FormatException("Missing or inconsistent available memory");
            double used=total.Value-available.Value;
            return new Usage {used=used/1048576.0,total=total.Value/1048576.0,percent=100*used/total.Value};
        }
    }
}
