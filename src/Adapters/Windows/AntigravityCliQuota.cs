using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace HardwarePulse {
    internal static class AntigravityCliQuota {
        internal static string InstalledExecutable(){
            string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"agy","bin","agy.exe");
            return File.Exists(path)?path:null;
        }
        internal static QuotaReading Read(string executable,CancellationToken cancel){
            var body=QuotaChildProcess.Read(executable,"--print /usage --print-timeout 15s --log-file NUL",ReadReport,cancel,true);
            var reading=QuotaDecoder.Decode("Antigravity",body,DateTimeOffset.UtcNow);
            reading.Source="CLI";
            return reading;
        }
        internal static object ReadReport(TextReader input,TextWriter output,CancellationToken cancel){
            var text=new StringBuilder();int value;
            while((value=input.Read())>=0){cancel.ThrowIfCancellationRequested();if(text.Length>=65536)throw new QuotaFailure("Quota unavailable");text.Append((char)value);}
            cancel.ThrowIfCancellationRequested();
            var groups=new Dictionary<string,Dictionary<string,object>>(StringComparer.Ordinal);
            foreach(string line in text.ToString().Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries)){
                string[] fields=line.TrimEnd('\r').Split('\t');
                if(fields.Length!=4||fields[0].Length==0||fields[0].Length>80||fields[0].Any(char.IsControl))throw new QuotaFailure("Quota unavailable");
                string window=fields[1]=="Weekly Limit Remaining"?"weekly":fields[1]=="Five Hour Limit Remaining"?"5-hour":null;
                double remaining;DateTimeOffset reset;
                if(window==null||!fields[2].EndsWith("%",StringComparison.Ordinal)||fields[2].IndexOf('%')!=fields[2].Length-1||
                    !double.TryParse(fields[2].TrimEnd('%'),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out remaining)||
                    double.IsNaN(remaining)||double.IsInfinity(remaining)||remaining<0||remaining>100||
                    fields[3].IndexOf('T')<0||!System.Text.RegularExpressions.Regex.IsMatch(fields[3],@"(?:Z|[+-]\d{2}:\d{2})$")||!DateTimeOffset.TryParse(fields[3],CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out reset))throw new QuotaFailure("Quota unavailable");
                Dictionary<string,object> buckets;
                if(!groups.TryGetValue(fields[0],out buckets)){if(groups.Count>=32)throw new QuotaFailure("Quota unavailable");groups.Add(fields[0],buckets=new Dictionary<string,object>());}
                if(buckets.ContainsKey(window))throw new QuotaFailure("Quota unavailable");
                buckets.Add(window,new Dictionary<string,object>{{"window",window},{"remainingFraction",remaining/100},{"resetTime",reset.ToString("o")}});
            }
            if(!groups.ContainsKey("Gemini Models")||groups.Any(g=>g.Value.Count!=2))throw new QuotaFailure("Quota unavailable");
            return new Dictionary<string,object>{{"groups",groups.Select(g=>(object)new Dictionary<string,object>{{"displayName",g.Key},{"buckets",g.Value.Values.ToArray()}}).ToArray()}};
        }
    }
}
