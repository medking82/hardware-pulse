using System;
using System.IO;
using System.Text;

namespace HardwarePulse {
    // Record transitions only. Never copy arbitrary exception text or raw sensor data.
    public sealed class HardwareDiagnosticLog {
        readonly string path;
        string previous;
        public HardwareDiagnosticLog(string state){path=Path.Combine(state,"hardware-diagnostics.jsonl");}
        public static string Reason(Reading reading){
            switch(reading.readErrorCode){
                case 32:case 33:return "Snapshot busy";
                case 2:case 3:return "Snapshot missing";
                case 5:return "Snapshot access denied";
            }
            return reading.state=="STALE"?"Snapshot stale":reading.state=="LIVE"?"Live":"Snapshot unreadable";
        }
        public void Record(Reading reading,DateTimeOffset now){
            string state=reading.state=="LIVE"?"LIVE":reading.state=="STALE"?"STALE":"OFFLINE",reason=Reason(reading);
            string signature=state+"|"+reason;if(previous==signature)return;
            try{
                var entry=new {schema=1,observed=now.ToString("o"),state=state,reason=reason,snapshotTime=reading.time==default(DateTimeOffset)?null:reading.time.ToString("o")};
                byte[] line=Encoding.UTF8.GetBytes(Json.Serializer().Serialize(entry)+"\n");
                if(File.Exists(path)&&new FileInfo(path).Length+line.Length>QuotaDiagnosticLog.MaximumBytes){QuotaDiagnosticLog.RotateBounded(path,path+".1");using(var clear=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.Read)){} }
                using(var output=new FileStream(path,FileMode.Append,FileAccess.Write,FileShare.Read))output.Write(line,0,line.Length);
                previous=signature;
            }catch{/* A busy diagnostic file must not interrupt sampling. */}
        }
    }
}
