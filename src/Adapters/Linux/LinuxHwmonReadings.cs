using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace HardwarePulse {
    public sealed record LinuxHwmonChannel(string Id,string Label,string Unit);

    // Raw kernel ABI channels, not board-specific CPU/GPU/fan assignments.
    // One host owns serial Read/Refresh calls. No timer, process or sysfs writes.
    public sealed class LinuxHwmonReadings {
        sealed record Entry(LinuxHwmonChannel Channel,string Prefix,bool Temperature,bool Enable,bool Fault,bool Type);
        readonly string root,source=Guid.NewGuid().ToString("N");
        Entry[] entries=Array.Empty<Entry>();
        long sequence;
        public IReadOnlyList<LinuxHwmonChannel> Channels {get;private set;}=Array.Empty<LinuxHwmonChannel>();

        public LinuxHwmonReadings():this(NativeRoot()){}
        // Explicit root supports isolated filesystem fixtures; the host uses NativeRoot only.
        public LinuxHwmonReadings(string root) {
            this.root=Path.GetFullPath(root??throw new ArgumentNullException(nameof(root)));
            Refresh();
        }
        static string NativeRoot() {
            if(!OperatingSystem.IsLinux())throw new PlatformNotSupportedException("Linux hwmon is required");
            return "/sys/class/hwmon";
        }
        public void Refresh() {
            var found=new List<Entry>();
            try {
                foreach(var directory in Directory.EnumerateDirectories(root).Take(128).OrderBy(x=>x,StringComparer.Ordinal)) {
                    string device=Path.GetFileName(directory);
                    if(!Regex.IsMatch(device,@"^hwmon[0-9]+$"))continue;
                    try {
                        string chip=Text(Path.Combine(directory,"name"));
                        if(string.IsNullOrWhiteSpace(chip))continue;
                        foreach(var file in Directory.EnumerateFiles(directory,"*_input").Take(512).OrderBy(x=>x,StringComparer.Ordinal)) {
                            var match=Regex.Match(Path.GetFileName(file),@"^(temp|fan)([1-9][0-9]*)_input$");
                            if(!match.Success)continue;
                            string channel=match.Groups[1].Value+match.Groups[2].Value;
                            string prefix=Path.Combine(directory,channel);
                            string label=Optional(prefix+"_label");
                            var descriptor=new LinuxHwmonChannel(device+"/"+channel,chip+" · "+(string.IsNullOrWhiteSpace(label)?channel:label),match.Groups[1].Value=="temp"?"°C":"RPM");
                            found.Add(new Entry(descriptor,prefix,descriptor.Unit=="°C",File.Exists(prefix+"_enable"),File.Exists(prefix+"_fault"),File.Exists(prefix+"_type")));
                        }
                    }catch(IOException){}catch(UnauthorizedAccessException){}
                }
            }catch(IOException){}catch(UnauthorizedAccessException){}
            entries=found.ToArray();
            Channels=Array.AsReadOnly(entries.Select(x=>x.Channel).ToArray());
        }
        public Reading Read(DateTimeOffset now) {
            var result=new Reading {time=now,identity=source+":"+(++sequence).ToString(CultureInfo.InvariantCulture),available=new Dictionary<string,bool>()};
            foreach(var entry in entries) {
                string id=entry.Channel.Id;
                result.names[id]=entry.Channel.Label;result.available[id]=false;
                try {
                    string enabled=entry.Enable?Text(entry.Prefix+"_enable"):null,fault=entry.Fault?Text(entry.Prefix+"_fault"):null;
                    if((enabled!=null&&enabled!="1")||(fault!=null&&fault!="0"))continue;
                    if(entry.Temperature) {
                        string type=entry.Type?Text(entry.Prefix+"_type"):null;
                        // Thermistor channels may report millivolts requiring board calibration.
                        if(type!=null&&type!="1"&&type!="2"&&type!="3"&&type!="5"&&type!="6")continue;
                    }
                    if(!long.TryParse(Text(entry.Prefix+"_input"),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out long raw))continue;
                    if(!entry.Temperature&&raw<0)continue;
                    double value=entry.Temperature?raw/1000.0:raw;
                    if(entry.Temperature&&value< -273.15)continue;
                    result.values[id]=value;result.available[id]=true;
                }catch(IOException){}catch(UnauthorizedAccessException){}
            }
            if(result.values.Count>0)result.state="LIVE";
            return result;
        }
        static string Optional(string path) {
            try{return Text(path);}catch(FileNotFoundException){return null;}catch(DirectoryNotFoundException){return null;}
        }
        static string Text(string path) {
            using(var reader=File.OpenText(path)) {
                var buffer=new char[257];int count=reader.ReadBlock(buffer,0,buffer.Length);
                if(count>256)throw new IOException("Oversized hwmon attribute");
                string value=new string(buffer,0,count).Trim();
                if(value.Any(char.IsControl))throw new IOException("Invalid hwmon attribute");
                return value;
            }
        }
    }
}
