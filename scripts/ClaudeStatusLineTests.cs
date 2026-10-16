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
        if(args.Length==1&&args[0]=="--claude-statusline")return ClaudeStatusLineReceiver.Run(Environment.GetEnvironmentVariable("PULSE_STATUSLINE_TEST_STATE"));
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
        Check(ClaudeStatusLineReceiver.Reset(state)&&!File.Exists(path),"explicit rebind removes only the selected snapshot");
        Check(Receive(state,Payload("OTHER_SESSION",90,now),now)=="CLI snapshot","explicit rebind allows the next session");
        Child(Path.Combine(root,"child"),false);Child(Path.Combine(root,"idle"),true);
        ShellCommands(root);
        Console.WriteLine("PASS Claude status-line receiver: bounded input/lifetime, whitelist, persistent age, clock rollback, session isolation, atomic concurrency and corruption");
        return 0;
    }
    static void ShellCommands(string root){
        string fixture=Path.Combine(root,"shell path $literal's"),original=Assembly.GetExecutingAssembly().Location;
        Directory.CreateDirectory(fixture);string executable=Path.Combine(fixture,"Receiver.exe");File.Copy(original,executable);
        File.Copy(Path.Combine(Path.GetDirectoryName(original),"Pulse.Core.dll"),Path.Combine(fixture,"Pulse.Core.dll"));
        if(File.Exists(original+".config"))File.Copy(original+".config",executable+".config");
        string powershell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");
        foreach(bool legacy in new[]{true,false}){
            string state=Path.Combine(root,legacy?"legacy-shell":"powershell-shell");
            string command=legacy?"\""+executable.Replace('\\','/')+"\" --claude-statusline":ClaudeStatusLineCommand.Create(executable,true);
            RunShell(powershell,root,state,command,true,!legacy);
        }
        string bash=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Git","bin","bash.exe");
        if(File.Exists(bash))RunShell(bash,root,Path.Combine(root,"bash-shell"),ClaudeStatusLineCommand.Create(executable,false),false,true);
        else Console.WriteLine("SKIP Git Bash invocation: Git Bash unavailable");
        foreach(string invalid in new[]{"relative.exe","C:relative.exe","\\relative.exe","bad\npath",null}){
            bool rejected=false;try{ClaudeStatusLineCommand.Create(invalid,true);}catch(ArgumentException){rejected=true;}
            Check(rejected,"invalid command path rejected");
        }
        Console.WriteLine("PASS status-line shell command: old PowerShell command fails; quoted paths and literal metacharacters survive shell invocation");
    }
    static void RunShell(string shell,string root,string state,string command,bool powershell,bool expected){
        string script=Path.Combine(root,Path.GetFileName(state)+(powershell?".ps1":".sh"));
        File.WriteAllText(script,command+Environment.NewLine,new System.Text.UTF8Encoding(powershell));
        var start=new ProcessStartInfo{FileName=shell,Arguments=(powershell?"-NoProfile -NonInteractive -File ":"--noprofile --norc ")+"\""+script.Replace('\\','/')+"\"",UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        start.EnvironmentVariables["PULSE_STATUSLINE_TEST_STATE"]=state;
        using(var process=Process.Start(start)){
            try{
                var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
                process.StandardInput.Write(Payload("shell-session",25,DateTimeOffset.UtcNow));process.StandardInput.Close();
                Check(process.WaitForExit(10000),"shell invocation exceeded deadline");
                Check(Task.WaitAll(new Task[]{output,errors},2000),"shell output readers did not finish");
                bool success=process.ExitCode==0&&output.Result.Contains("CLI snapshot")&&ClaudeStatusLineReceiver.Read(state,DateTimeOffset.UtcNow).Windows.Count==1;
                Check(success==expected,"shell command result mismatch: "+Path.GetFileName(shell)+" expected="+expected+" exit="+process.ExitCode+" snapshot="+File.Exists(Path.Combine(state,"claude-statusline","snapshot.json")));
                if(expected)Check(errors.Result==""&&!output.Result.Contains("PRIVATE_"),"shell leaks diagnostics/input");
            }finally{if(!process.HasExited){process.Kill();process.WaitForExit();}}
        }
    }
}
