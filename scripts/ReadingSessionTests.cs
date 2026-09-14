using System;
using System.IO;
using HardwarePulse;

static class ReadingSessionTests {
    static void Check(bool pass,string reason){if(!pass)throw new Exception(reason);}
    static RawSnapshot Snapshot(DateTimeOffset now,int pid,long sequence,double temperature) {
        return new RawSnapshot {schema=2,pid=pid,sequence=sequence,time=now.ToString("o"),
            ramUsage=new RamUsage {usedGb=8,totalGb=16},sensors=new[]{
                new Sensor {id="/cpu/0/temp/0",hardwareId="/cpu/0",hardwareType="Cpu",name="CPU Package",type="Temperature",value=temperature}
            }};
    }
    static int Main(string[] args) {
        try {
            string path=Path.Combine(args[0],"snapshot.json");Directory.CreateDirectory(args[0]);
            var now=new DateTimeOffset(2026,9,14,12,0,0,TimeSpan.Zero);
            var session=new ReadingSession(path);session.Poll(now);
            Check(session.Latest.state=="OFFLINE"&&session.Peaks.Count==0,"Missing initial snapshot");
            Json.WriteAtomic(path,Snapshot(now,1,1,50));session.Poll(now);
            Check(session.Latest.values["cpu"]==50&&session.Peaks["cpu"]==50&&session.HasUsage("ram"),"First live reading");
            Json.WriteAtomic(path,Snapshot(now,1,1,90));session.Poll(now);
            Check(session.Peaks["cpu"]==50&&session.Latest.values["cpu"]==90,"Duplicate identity must not update session peak");
            Json.WriteAtomic(path,Snapshot(now,1,2,70));session.Poll(now);
            Check(session.Peaks["cpu"]==70,"New sequence peak");
            session.Poll(now.AddSeconds(16));
            Check(session.Latest.state=="STALE"&&session.Latest.values.Count==0&&session.Latest.available["cpu"]&&session.HasUsage("ram"),"Stale readings must retain capabilities, not values");
            File.WriteAllText(path,"{broken");session.Poll(now);
            Check(session.Latest.state=="OFFLINE"&&session.Latest.available["cpu"]&&session.Peaks["cpu"]==70,"Malformed snapshot must preserve history");
            var changed=Snapshot(now,2,1,65);changed.ramUsage=null;Json.WriteAtomic(path,changed);session.Poll(now);
            Check(session.Latest.state=="LIVE"&&!session.HasUsage("ram")&&session.Peaks["cpu"]==70,"Restart must retain session peak and replace capabilities");
            Json.WriteAtomic(path,Snapshot(now,2,2,85));session.Poll(now);
            Check(session.Peaks["cpu"]==85,"Restarted collector sequence must advance peaks");
            var freshSession=new ReadingSession(path);freshSession.Poll(now);
            Check(freshSession.Peaks["cpu"]==85,"Independent session initial peak");
            Json.WriteAtomic(path,Snapshot(now,2,3,95));freshSession.Poll(now);
            Check(session.Peaks["cpu"]==85&&freshSession.Peaks["cpu"]==95,"Session histories must be independent");
            Console.WriteLine("PASS headless reading session: offline/live/stale recovery, duplicate identity, peaks, capability replacement and independent histories");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
