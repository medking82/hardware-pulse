using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace HardwarePulse {
    // Small, bounded persistence for quota scheduling only. Never persist readings, credentials,
    // window state, cache scope, or account identifiers here.
    public sealed class QuotaScheduleStore {
        public const int MaximumBytes=16*1024;
        static readonly string[] Providers={"Codex","Antigravity","Claude"};
        static readonly string[] Statuses={"Live","Quota unavailable","Login required","Login unavailable","Quota access denied","Refresh rate limited","Open Antigravity to read quota","Refresh pending"};
        static readonly string[] RootKeys={"schema","schedules"};
        static readonly string[] ScheduleKeys={"provider","status","observed","nextAttempt","notBefore","recoveryFailures","rateLimitFailures"};
        readonly string path;

        sealed class StoredSchedule {
            public string provider;
            public string status;
            public string observed;
            public string nextAttempt;
            public string notBefore;
            public int recoveryFailures;
            public int rateLimitFailures;
        }

        public QuotaScheduleStore(string stateDirectory){path=Path.Combine(stateDirectory,"quota-schedule.json");}

        static QuotaSchedule[] Empty(){return new QuotaSchedule[0];}
        static bool HasOnlyKeys(Dictionary<string,object> value,string[] keys){
            if(value==null||value.Count!=keys.Length)return false;
            foreach(string key in value.Keys)if(Array.IndexOf(keys,key)<0)return false;
            return true;
        }
        static bool TryInteger(object value,out int result){
            if(value is int){result=(int)value;return true;}
            if(value is long&&((long)value)>=int.MinValue&&((long)value)<=int.MaxValue){result=(int)(long)value;return true;}
            result=0;return false;
        }
        static bool TryTimestamp(object value,out DateTimeOffset result){
            result=default(DateTimeOffset);
            string text=value as string;
            if(text==null)return false;
            if(!DateTimeOffset.TryParseExact(text,"o",CultureInfo.InvariantCulture,DateTimeStyles.None,out result))return false;
            return string.Equals(result.ToString("o",CultureInfo.InvariantCulture),text,StringComparison.Ordinal);
        }
        static bool Valid(QuotaSchedule item){
            return item!=null&&Array.IndexOf(Providers,item.Provider)>=0&&Array.IndexOf(Statuses,item.Status)>=0&&
                item.RecoveryFailures>=0&&item.RecoveryFailures<=4&&item.RateLimitFailures>=0&&item.RateLimitFailures<=4;
        }
        static StoredSchedule Project(QuotaSchedule item){
            return new StoredSchedule{
                provider=item.Provider,
                status=item.Status,
                observed=item.Observed.ToString("o",CultureInfo.InvariantCulture),
                nextAttempt=item.NextAttempt.ToString("o",CultureInfo.InvariantCulture),
                notBefore=item.NotBefore.ToString("o",CultureInfo.InvariantCulture),
                recoveryFailures=item.RecoveryFailures,
                rateLimitFailures=item.RateLimitFailures
            };
        }

        public QuotaSchedule[] Load(){
            try{
                if(!File.Exists(path))return Empty();
                string json;
                using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){
                    if(stream.Length>MaximumBytes)return Empty();
                    byte[] bytes=new byte[MaximumBytes+1];int total=0,read;
                    while(total<bytes.Length&&(read=stream.Read(bytes,total,bytes.Length-total))>0)total+=read;
                    if(total>MaximumBytes)return Empty();
                    json=new UTF8Encoding(false,true).GetString(bytes,0,total);
                }
                var root=Json.Serializer().DeserializeObject(json) as Dictionary<string,object>;
                if(!HasOnlyKeys(root,RootKeys))return Empty();
                int schema;
                if(!TryInteger(root["schema"],out schema)||schema!=1)return Empty();
                var rows=root["schedules"] as object[];
                if(rows==null||rows.Length>Providers.Length)return Empty();
                var result=new List<QuotaSchedule>(rows.Length);
                var seen=new HashSet<string>(StringComparer.Ordinal);
                foreach(object row in rows){
                    var value=row as Dictionary<string,object>;
                    if(!HasOnlyKeys(value,ScheduleKeys))return Empty();
                    string provider=value["provider"] as string,status=value["status"] as string;
                    if(Array.IndexOf(Providers,provider)<0||Array.IndexOf(Statuses,status)<0||!seen.Add(provider))return Empty();
                    DateTimeOffset observed,nextAttempt,notBefore;
                    int recoveryFailures,rateLimitFailures;
                    if(!TryTimestamp(value["observed"],out observed)||!TryTimestamp(value["nextAttempt"],out nextAttempt)||!TryTimestamp(value["notBefore"],out notBefore)||
                        !TryInteger(value["recoveryFailures"],out recoveryFailures)||recoveryFailures<0||recoveryFailures>4||
                        !TryInteger(value["rateLimitFailures"],out rateLimitFailures)||rateLimitFailures<0||rateLimitFailures>4)return Empty();
                    result.Add(new QuotaSchedule{Provider=provider,Status=status,Observed=observed,NextAttempt=nextAttempt,NotBefore=notBefore,RecoveryFailures=recoveryFailures,RateLimitFailures=rateLimitFailures});
                }
                return result.ToArray();
            }catch{return Empty();}
        }

        public void Save(QuotaSchedule[] schedules){
            try{
                if(schedules==null||schedules.Length>Providers.Length)return;
                var seen=new HashSet<string>(StringComparer.Ordinal);
                var rows=new StoredSchedule[schedules.Length];
                for(int i=0;i<schedules.Length;i++){
                    QuotaSchedule item=schedules[i];
                    if(!Valid(item)||!seen.Add(item.Provider))return;
                    rows[i]=Project(item);
                }
                var document=new{schema=1,schedules=rows};
                string json=Json.Serializer().Serialize(document);
                if(new UTF8Encoding(false).GetByteCount(json)>MaximumBytes)return;
                Json.WriteAtomic(path,document);
            }catch{/* Storage failures must not interrupt quota refresh or the UI. */}
        }
    }
}
