using System;
using System.IO;
using System.Text;

namespace HardwarePulse {
    // Local, bounded, explicitly whitelisted metadata. Never serialize a reading or exception.
    public sealed class QuotaDiagnosticLog {
        public const int MaximumBytes=32768;
        readonly string path;
        public QuotaDiagnosticLog(string stateDirectory){path=Path.Combine(stateDirectory,"quota-diagnostics.jsonl");}
        static string Allowed(string value,string[] choices){return Array.IndexOf(choices,value)>=0?value:null;}
        internal static byte[] Project(QuotaAttempt attempt){
            if(attempt==null||Array.IndexOf(QuotaSession.Providers,attempt.Provider)<0)return null;
            string status=Allowed(attempt.Status,new[]{"Live","CLI snapshot","Quota unavailable","Refresh rate limited","Login required","Login unavailable","Quota access denied","Refresh timed out","Open Antigravity to read quota"})??"Quota unavailable";
            string source=Allowed(attempt.Source,new[]{"Desktop","CLI","CLI snapshot"});
            string reason=Allowed(attempt.FailureKind,new[]{"HTTP response","Transport timeout","Transport failure","Invalid response","Response too large","CLI failure","Local discovery","Local port unavailable","Request timeout"});
            var entry=new {schema=1,provider=attempt.Provider,source=source,status=status,httpStatus=attempt.HttpStatus>=100&&attempt.HttpStatus<=599?(int?)attempt.HttpStatus:null,reason=reason,
                started=attempt.Started.ToString("o"),completed=attempt.Completed.ToString("o"),durationMs=double.IsNaN(attempt.DurationMilliseconds)||double.IsInfinity(attempt.DurationMilliseconds)?(double?)null:Math.Max(0,Math.Round(attempt.DurationMilliseconds)),
                retryAfter=attempt.RetryAt.HasValue?attempt.RetryAt.Value.ToString("o"):null,nextAttempt=attempt.NextAttempt.HasValue?attempt.NextAttempt.Value.ToString("o"):null,rateLimitFailures=Math.Max(0,Math.Min(4,attempt.RateLimitFailures))};
            byte[] line=Encoding.UTF8.GetBytes(Json.Serializer().Serialize(entry)+"\n");
            return line.Length>MaximumBytes?null:line;
        }
        internal static void RotateBounded(string source,string rotated){
            using(var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
            using(var output=new FileStream(rotated,FileMode.Create,FileAccess.Write,FileShare.Read)){
                long count=Math.Min(MaximumBytes,input.Length);long start=input.Length-count;input.Seek(start,SeekOrigin.Begin);
                // An externally oversized file may start the retained tail mid-record.
                if(start>0){input.Seek(-1,SeekOrigin.Current);int preceding=input.ReadByte();if(preceding!='\n')while(input.Position<input.Length&&input.ReadByte()!='\n'){} }
                byte[] buffer=new byte[8192];long remaining=input.Length-input.Position;int read;
                while(remaining>0&&(read=input.Read(buffer,0,(int)Math.Min(buffer.Length,remaining)))>0){output.Write(buffer,0,read);remaining-=read;}
            }
        }
        public void Record(QuotaAttempt attempt){
            try{
                byte[] line=Project(attempt);if(line==null)return;
                if(File.Exists(path)&&new FileInfo(path).Length+line.Length>MaximumBytes){RotateBounded(path,path+".1");using(var clear=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.Read)){} }
                using(var output=new FileStream(path,FileMode.Append,FileAccess.Write,FileShare.Read))output.Write(line,0,line.Length);
            }catch{/* A busy/unwritable diagnostic file must not stop quota refresh or the UI. */}
        }
    }
}
