using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HardwarePulse;

internal static class ClaudeStatusLineTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static string Payload(string session,int used,DateTimeOffset now){
        long reset=(long)(now.AddHours(1)-new DateTimeOffset(1970,1,1,0,0,0,TimeSpan.Zero)).TotalSeconds;
        return "{\"session_id\":\""+session+"\",\"transcript_path\":\"PRIVATE_TRANSCRIPT\",\"workspace\":{\"current_dir\":\"PRIVATE_WORKSPACE\"},\"rate_limits\":{\"five_hour\":{\"used_percentage\":"+used+",\"resets_at\":"+reset+"}}}";
    }
    static string Receive(string path,string payload,DateTimeOffset now){return ClaudeStatusLineReceiver.Receive(path,new StringReader(payload),now);}
    static void Child(string path,bool idle){
        var start=new ProcessStartInfo{FileName=Assembly.GetExecutingAssembly().Location,Arguments="--receive \""+path+"\"",UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        using(var process=Process.Start(start)){
            try{
                if(!idle){process.StandardInput.Write(Payload("child-session",25,DateTimeOffset.UtcNow));process.StandardInput.Close();}
                Check(process.WaitForExit(5000),"receiver child exceeded bounded lifetime");
                Check(process.ExitCode==(idle?1:0),"receiver child exit status");
                string output=process.StandardOutput.ReadToEnd(),errors=process.StandardError.ReadToEnd();
                Check(errors==""&&!output.Contains("PRIVATE_"),"receiver leaked input or diagnostics");
                if(!idle)Check(output.Contains("CLI snapshot"),"receiver child did not publish snapshot");
            }finally{if(!process.HasExited){process.Kill();process.WaitForExit();}}
        }
    }
    static int Main(string[] args){
        if(args.Length==2&&args[0]=="--receive")return ClaudeStatusLineReceiver.Run(args[1]);
        string root=args[0];Directory.CreateDirectory(root);
        var now=new DateTimeOffset(2026,9,20,0,0,0,TimeSpan.Zero);
        string state=Path.Combine(root,"state"),data=Payload("PRIVATE_SESSION",10,now);
        Check(Receive(state,data,now)=="CLI snapshot","initial receive");
        string path=Path.Combine(state,"claude-statusline","snapshot.json");
        string saved=File.ReadAllText(path);
        Check(saved.Length<4096&&!saved.Contains("PRIVATE_")&&!saved.Contains("session_id")&&!saved.Contains("transcript"),"persistence privacy whitelist");
        Check(ClaudeStatusLineReceiver.Read(state,now).Windows.Single().Remaining==90,"snapshot round trip");
        Check(Receive(state,data,now.AddMinutes(9))=="CLI snapshot","repeat receive");
        Check(ClaudeStatusLineReceiver.Read(state,now.AddMinutes(10)).Status=="Quota stale","cross-process persistence must preserve age");
        Check(Receive(state,data,now.AddMinutes(-1))=="Quota stale","clock rollback cannot renew persisted data");
        Check(Receive(state,Payload("OTHER_SESSION",90,now),now)=="Different CLI session","cross-session replacement must fail closed");
        Check(ClaudeStatusLineReceiver.Read(state,now).Windows.Single().Remaining==90,"foreign session changed selected quota");
        saved=File.ReadAllText(path);
        foreach(string invalid in new[]{"{PRIVATE_MALFORMED",new string('x',65537),"{}","{\"session_id\":\"x\",\"a\":"+new string('[',20)+"0"+new string(']',20)+"}"}){
            Check(Receive(state,invalid,now)=="Invalid snapshot","invalid or excessive input rejection");
            Check(File.ReadAllText(path)==saved,"invalid input changed prior file");
        }
        using(var gate=new FileStream(Path.Combine(state,"claude-statusline","write.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))
            Check(Receive(state,data,now)=="Snapshot unavailable","concurrent writer lock is bounded");
        string race=Path.Combine(root,"race");
        var a=Task.Run(()=>Receive(race,Payload("session-a",10,now),now));
        var b=Task.Run(()=>Receive(race,Payload("session-b",90,now),now));Task.WaitAll(a,b);
        Check(new[]{a.Result,b.Result}.Count(v=>v=="CLI snapshot")==1,"exactly one first session wins atomic binding");
        Check(ClaudeStatusLineReceiver.Read(race,now).Windows.Count==1,"concurrent publication is complete");
        Check(Directory.GetFiles(Path.Combine(state,"claude-statusline"),"*.tmp").Length==0,"temporary publication files leaked");
        File.WriteAllText(path,"PRIVATE_CORRUPTED");Check(ClaudeStatusLineReceiver.Read(state,now).Status=="Quota unavailable","corrupt file read fails closed");
        Check(Receive(state,data,now)=="Invalid snapshot"&&File.ReadAllText(path)=="PRIVATE_CORRUPTED","corrupt owner cannot silently rebind");
        Child(Path.Combine(root,"child"),false);Child(Path.Combine(root,"idle"),true);
        Console.WriteLine("PASS Claude status-line receiver: bounded input/lifetime, whitelist, persistent age, clock rollback, session isolation, atomic concurrency and corruption");
        return 0;
    }
}
