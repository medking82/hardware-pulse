using System;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using HardwarePulse;

static class ReadingSessionTests {
    static void Check(bool pass,string reason){if(!pass)throw new Exception(reason);}
    sealed class FileConflict : IOException {
        public FileConflict(int code):base("Snapshot sharing conflict") {HResult=unchecked((int)0x80070000)|code;}
    }
    static Reading ReadWithFault(string path,DateTimeOffset now,Func<string,RawSnapshot> read) {
        var method=typeof(SensorProfile).GetMethod("ReadWithRetry",BindingFlags.Static|BindingFlags.NonPublic);
        return (Reading)method.Invoke(null,new object[]{path,now,read});
    }
    static void CheckReadConflicts(string path,DateTimeOffset now) {
        var raw=Snapshot(now,3,1,60);
        foreach(int code in new[]{32,33}) {
            int attempts=0;
            var recovered=ReadWithFault(path,now,p=>{if(++attempts==1)throw new FileConflict(code);return raw;});
            Check(recovered.state=="LIVE"&&recovered.values["cpu"]==60&&recovered.usage.ContainsKey("ram")&&attempts==2,"A transient sharing/lock conflict must recover the complete fresh reading");
            attempts=0;var timer=Stopwatch.StartNew();
            var blocked=ReadWithFault(path,now,p=>{attempts++;throw new FileConflict(code);});
            Check(blocked.state=="OFFLINE"&&blocked.values.Count==0&&attempts==3&&timer.Elapsed<TimeSpan.FromSeconds(2),"Persistent conflicts must stop after three attempts without live values");
        }
        foreach(Exception failure in new Exception[]{new FileNotFoundException(),new UnauthorizedAccessException(),new IOException("Other I/O failure"),new FormatException("Invalid snapshot")}) {
            int attempts=0;var invalid=ReadWithFault(path,now,p=>{attempts++;throw failure;});
            Check(invalid.state=="OFFLINE"&&invalid.values.Count==0&&attempts==1,"Unrelated failures must not be retried");
        }
        int nearExpiryAttempts=0;var nearExpiry=Snapshot(now.AddSeconds(-15),3,2,61);
        var expired=ReadWithFault(path,now,p=>{if(++nearExpiryAttempts==1)throw new FileConflict(32);return nearExpiry;});
        Check(expired.state=="STALE"&&expired.values.Count==0&&nearExpiryAttempts==2,"Time spent retrying must count toward snapshot freshness");
        Json.WriteAtomic(path,raw);
        Check(SensorProfile.Read(path,now).state=="LIVE","Exclusive-lock fixture starts with a fresh, valid snapshot");
        using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None)) {
            var blocked=SensorProfile.Read(path,now);
            Check(blocked.state=="OFFLINE"&&blocked.values.Count==0,"A real persistent exclusive lock remains unavailable");
        }
        Check(SensorProfile.Read(path,now).state=="LIVE","Real file reads recover after the lock is released");
    }
    static RawSnapshot Snapshot(DateTimeOffset now,int pid,long sequence,double temperature) {
        return new RawSnapshot {schema=2,pid=pid,sequence=sequence,time=now.ToString("o"),
            ramUsage=new RamUsage {usedGb=8,totalGb=16},sensors=new[]{
                new Sensor {id="/cpu/0/temp/0",hardwareId="/cpu/0",hardwareType="Cpu",hardware="Demo Processor",name="CPU Package",type="Temperature",value=temperature}
            }};
    }
    static int Main(string[] args) {
        try {
            string path=Path.Combine(args[0],"snapshot.json");Directory.CreateDirectory(args[0]);
            var now=new DateTimeOffset(2026,9,14,12,0,0,TimeSpan.Zero);
            var session=new ReadingSession(time=>SensorProfile.Read(path,time));session.Poll(now);
            Check(session.Latest.state=="OFFLINE"&&session.Peaks.Count==0,"Missing initial snapshot");
            Json.WriteAtomic(path,Snapshot(now,1,1,50));session.Poll(now);
            Check(session.Latest.values["cpu"]==50&&session.Peaks["cpu"]==50&&session.HasUsage("ram"),"First live reading");
            Json.WriteAtomic(path,Snapshot(now,1,1,90));session.Poll(now);
            Check(session.Peaks["cpu"]==50&&session.Latest.values["cpu"]==90,"Duplicate identity must not update session peak");
            Json.WriteAtomic(path,Snapshot(now,1,2,70));session.Poll(now);
            Check(session.Peaks["cpu"]==70,"New sequence peak");
            session.Poll(now.AddSeconds(16));
            Check(session.Latest.state=="STALE"&&session.Latest.values.Count==0&&session.Latest.available["cpu"]&&session.HasUsage("ram"),"Stale readings must retain capabilities, not values");
            Check(session.Latest.names.ContainsKey("CPU")&&session.Latest.names["CPU"]=="Demo Processor","Stale snapshot lost hardware identity");
            File.WriteAllText(path,"{broken");session.Poll(now);
            Check(session.Latest.state=="OFFLINE"&&session.Latest.available["cpu"]&&session.Peaks["cpu"]==70,"Malformed snapshot must preserve history");
            var changed=Snapshot(now,2,1,65);changed.ramUsage=null;changed.sensors[0].hardware="Replacement Processor";Json.WriteAtomic(path,changed);session.Poll(now);
            Check(session.Latest.names["CPU"]=="Replacement Processor","Live recovery retained obsolete device identity");
            Check(session.Latest.state=="LIVE"&&!session.HasUsage("ram")&&session.Peaks["cpu"]==70,"Restart must retain session peak and replace capabilities");
            Json.WriteAtomic(path,Snapshot(now,2,2,85));session.Poll(now);
            Check(session.Peaks["cpu"]==85,"Restarted collector sequence must advance peaks");
            var freshSession=new ReadingSession(time=>SensorProfile.Read(path,time));freshSession.Poll(now);
            Check(freshSession.Peaks["cpu"]==85,"Independent session initial peak");
            Json.WriteAtomic(path,Snapshot(now,2,3,95));freshSession.Poll(now);
            Check(session.Peaks["cpu"]==85&&freshSession.Peaks["cpu"]==95,"Session histories must be independent");
            CheckReadConflicts(path,now);
            Console.WriteLine("PASS headless reading session: offline/live/stale recovery, bounded sharing retries, expiry during retry, duplicate identity, peaks, capability replacement and independent histories");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
