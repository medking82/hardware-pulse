using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using HardwarePulse;

static class QuotaDiagnosticLogTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception("Diagnostic log: "+message);}
    static void ParseJsonLines(string path){
        var parser=new JavaScriptSerializer();foreach(string line in File.ReadAllLines(path,Encoding.UTF8)){Check(line.Trim().Length>1&&line.TrimStart().StartsWith("{")&&line.TrimEnd().EndsWith("}"),"rotated line is complete JSONL");parser.DeserializeObject(line);}
    }
    internal static void Run(string stateDirectory){
        var type=typeof(QuotaDiagnosticLog);var project=type.GetMethod("Project",BindingFlags.NonPublic|BindingFlags.Static);var rotate=type.GetMethod("RotateBounded",BindingFlags.NonPublic|BindingFlags.Static);
        var attempt=new QuotaAttempt{Provider="Antigravity",Status="Quota unavailable",Source="Desktop",FailureKind="HTTP response",HttpStatus=500,DurationMilliseconds=12.4};
        byte[] line=(byte[])project.Invoke(null,new object[]{attempt});Check(line!=null&&line.Length<=QuotaDiagnosticLog.MaximumBytes,"bounded projection");string text=Encoding.UTF8.GetString(line);Check(text.Contains("\"reason\":\"HTTP response\"")&&!text.Contains("127.0.0.1"),"safe projection fields");
        string sentinel="secret-provider secret-source secret-status secret-reason";var unsafeAttempt=new QuotaAttempt{Provider=sentinel,Source=sentinel,Status=sentinel,FailureKind=sentinel};Check(project.Invoke(null,new object[]{unsafeAttempt})==null,"unknown provider rejected");var safeAttempt=new QuotaAttempt{Provider="Antigravity",Source=sentinel,Status=sentinel,FailureKind=sentinel};string safeText=Encoding.UTF8.GetString((byte[])project.Invoke(null,new object[]{safeAttempt}));Check(!safeText.Contains("secret-")&&!safeText.Contains("secret_"),"unknown source/status/reason dropped");
        Directory.CreateDirectory(stateDirectory);string active=Path.Combine(stateDirectory,"quota-diagnostics.jsonl"),rotated=active+".1",source=Path.Combine(stateDirectory,"quota-diagnostic-oversized.jsonl"),sourceRotated=source+".1";
        try{
            var log=new QuotaDiagnosticLog(stateDirectory);for(int i=0;i<400;i++){attempt.Started=DateTimeOffset.UtcNow;attempt.Completed=attempt.Started;attempt.DurationMilliseconds=i;log.Record(attempt);}
            Check(File.Exists(active)&&new FileInfo(active).Length<=QuotaDiagnosticLog.MaximumBytes,"active log remains bounded");Check(File.Exists(rotated)&&new FileInfo(rotated).Length<=QuotaDiagnosticLog.MaximumBytes,"rotated log remains bounded");ParseJsonLines(active);ParseJsonLines(rotated);
            string valid=Encoding.UTF8.GetString(line);var oversized=new StringBuilder(new string('x',137)+"\n");for(int i=0;i<160;i++)oversized.Append(valid);File.WriteAllText(source,oversized.ToString(),Encoding.UTF8);rotate.Invoke(null,new object[]{source,sourceRotated});Check(new FileInfo(sourceRotated).Length<=QuotaDiagnosticLog.MaximumBytes,"oversized preexisting rotation bounded");ParseJsonLines(sourceRotated);
        }finally{foreach(string file in new[]{active,rotated,source,sourceRotated})if(File.Exists(file))File.Delete(file);}
        Console.WriteLine("PASS diagnostic log: bounded projection and rotation; synthetic only");
    }
}
