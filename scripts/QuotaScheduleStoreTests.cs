using System;
using System.IO;
using HardwarePulse;

static class QuotaScheduleStoreTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception("Quota schedule store: "+message);}
    static QuotaSchedule Schedule(string provider,DateTimeOffset deadline){
        return new QuotaSchedule{Provider=provider,Status="Refresh rate limited",Observed=new DateTimeOffset(2026,9,27,8,26,11,TimeSpan.FromHours(8)),NextAttempt=deadline,NotBefore=deadline,RecoveryFailures=3,RateLimitFailures=4};
    }
    static void Write(string directory,string content){File.WriteAllText(Path.Combine(directory,"quota-schedule.json"),content,new System.Text.UTF8Encoding(false));}
    static QuotaSchedule[] Read(string directory){return new QuotaScheduleStore(directory).Load();}
    internal static void Run(string directory){
        string root=Path.Combine(directory,"quota-schedule-store-tests-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try{
            var store=new QuotaScheduleStore(root);
            DateTimeOffset deadline=new DateTimeOffset(2056,9,27,8,26,11,TimeSpan.FromHours(8));
            store.Save(new[]{Schedule("Antigravity",deadline),Schedule("Claude",deadline.AddDays(1))});
            string path=Path.Combine(root,"quota-schedule.json");
            Check(File.Exists(path)&&new FileInfo(path).Length<=QuotaScheduleStore.MaximumBytes,"bounded schedule file written");
            string json=File.ReadAllText(path);
            Check(json.Contains("2056-09-27T08:26:11.0000000+08:00"),"long future deadline preserved");
            Check(!json.Contains("token")&&!json.Contains("window")&&!json.Contains("reading")&&!json.Contains("accountId")&&!json.Contains("cacheScope"),"no credential, window, reading, or account metadata serialized");
            QuotaSchedule[] loaded=store.Load();
            Check(loaded.Length==2&&loaded[0].Provider=="Antigravity"&&loaded[0].Status=="Refresh rate limited"&&loaded[0].NextAttempt==deadline&&loaded[0].NotBefore==deadline&&loaded[0].RecoveryFailures==3&&loaded[0].RateLimitFailures==4,"schedule roundtrip");
            Check(loaded[1].Provider=="Claude"&&loaded[1].NextAttempt==deadline.AddDays(1),"multiple provider roundtrip");
            foreach(ClaudeRecoveryStage stage in Enum.GetValues(typeof(ClaudeRecoveryStage))){
                var routed=Schedule("Claude",deadline);routed.ClaudeRecovery=stage;store.Save(new[]{routed});loaded=store.Load();
                Check(loaded.Length==1&&loaded[0].ClaudeRecovery==stage&&loaded[0].NotBefore==deadline&&loaded[0].RateLimitFailures==4,"Auto route/stop and shared cooldown survive disk roundtrip");
            }

            using(var session=new QuotaSession((provider,cancel)=>new QuotaReading{Provider=provider})){
                store.Save(session.CaptureSchedules());
                Check(store.Load().Length==QuotaSession.Providers.Length,"every registered provider schedule persists even when disabled");
            }
            foreach(string status in new[]{"API key required","API key rejected","Credential unavailable","Refresh rate limited"}){
                var deepSeek=Schedule("DeepSeek",deadline);deepSeek.Status=status;
                store.Save(new[]{Schedule("Claude",deadline),deepSeek});
                loaded=store.Load();
                Check(loaded.Length==2&&loaded[1].Status==status&&loaded[1].NotBefore==deadline&&loaded[1].RateLimitFailures==4,"DeepSeek auth states preserve server cooldown and other provider schedules");
            }

            Write(root,"{");Check(Read(root).Length==0,"malformed JSON ignored");
            Write(root,"{\"schema\":99,\"schedules\":[]}");Check(Read(root).Length==0,"unknown schema ignored");
            string legacy="{\"schema\":1,\"schedules\":[{\"provider\":\"Claude\",\"status\":\"Refresh rate limited\",\"observed\":\"2026-09-27T08:26:11.0000000+08:00\",\"nextAttempt\":\"2056-09-27T08:26:11.0000000+08:00\",\"notBefore\":\"2056-09-27T08:26:11.0000000+08:00\",\"recoveryFailures\":3,\"rateLimitFailures\":4}]}";
            Write(root,legacy);loaded=Read(root);Check(loaded.Length==1&&loaded[0].NotBefore==deadline&&loaded[0].ClaudeRecovery==ClaudeRecoveryStage.Api,"Legacy schedule migrates without clearing cooldown");
            foreach(string value in new[]{"-1","3","true","\"Cli\""}){Write(root,legacy.Replace("\"schema\":1","\"schema\":2").Replace("\"rateLimitFailures\":4","\"rateLimitFailures\":4,\"claudeRecovery\":"+value));Check(Read(root).Length==0,"Malformed Auto route rejected");}
            Write(root,"{\"schema\":1,\"schedules\":[{\"provider\":\"Unknown\",\"status\":\"Live\",\"observed\":\"2026-09-27T08:26:11.0000000+08:00\",\"nextAttempt\":\"2056-09-27T08:26:11.0000000+08:00\",\"notBefore\":\"2056-09-27T08:26:11.0000000+08:00\",\"recoveryFailures\":0,\"rateLimitFailures\":0}]}");
            Check(Read(root).Length==0,"unknown provider ignored");
            Write(root,"{\"schema\":1,\"schedules\":[{\"provider\":\"Claude\",\"status\":\"Live\",\"observed\":\"not-a-roundtrip-timestamp\",\"nextAttempt\":\"2056-09-27T08:26:11.0000000+08:00\",\"notBefore\":\"2056-09-27T08:26:11.0000000+08:00\",\"recoveryFailures\":0,\"rateLimitFailures\":0}]}");
            Check(Read(root).Length==0,"invalid timestamp ignored");
            Write(root,"{\"schema\":1,\"schedules\":[{\"provider\":\"Claude\",\"status\":\"Live\",\"observed\":\"2026-09-27T08:26:11.0000000+08:00\",\"nextAttempt\":\"2056-09-27T08:26:11.0000000+08:00\",\"notBefore\":\"2056-09-27T08:26:11.0000000+08:00\",\"recoveryFailures\":0,\"rateLimitFailures\":0,\"window\":\"secret-window\"}]}");
            Check(Read(root).Length==0,"unknown window/credential fields rejected");
            File.WriteAllText(path,new string('x',QuotaScheduleStore.MaximumBytes+1));Check(Read(root).Length==0,"oversized schedule ignored");

            store.Save(new[]{Schedule("Antigravity",deadline)});
            string valid=File.ReadAllText(path);
            store.Save(new[]{new QuotaSchedule{Provider="Unknown",Status="Live"}});
            Check(File.ReadAllText(path)==valid,"invalid save does not replace valid schedule");

            string blocker=Path.Combine(root,"not-a-directory");File.WriteAllText(blocker,"keep");
            var blockedStore=new QuotaScheduleStore(blocker);
            blockedStore.Save(new[]{Schedule("Antigravity",deadline)});
            Check(File.ReadAllText(blocker)=="keep"&&blockedStore.Load().Length==0,"unwritable state path is safe");
            Console.WriteLine("PASS quota schedule store: bounded allowlisted persistence; synthetic only");
        }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
