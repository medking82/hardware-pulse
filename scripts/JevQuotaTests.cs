using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Threading;
using HardwarePulse;
internal static class JevQuotaTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception("Jev: "+message);}
    static Type Adapter(){var t=typeof(QuotaProviders).Assembly.GetType("HardwarePulse.JevQuota");Check(t!=null,"fixed-origin Chromium reader is required");return t;}
    static object Decode(Dictionary<string,object> wire){return Adapter().GetMethod("Decode").Invoke(null,new object[]{wire,DateTimeOffset.UtcNow});}
    static Dictionary<string,object> Wire(int http,string type,string text){return new Dictionary<string,object>{{"http",http},{"contentType",type},{"text",text}};}
    static QuotaFailure Failure(Dictionary<string,object> wire){try{Decode(wire);throw new Exception("Invalid response accepted");}catch(TargetInvocationException e){var failure=e.InnerException as QuotaFailure;Check(failure!=null,"only sanitized failures may cross transport");return failure;}}
    static void PublishPid(string file,int pid,Action writing){
        string pending=file+".tmp";
        try{
            using(var writer=new StreamWriter(pending)){writer.Write(pid);if(writing!=null)writing();}
            File.Move(pending,file);
        }finally{if(File.Exists(pending))File.Delete(pending);}
    }
    static void PidPublication(){
        string file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"jev-ready-"+Guid.NewGuid().ToString("N")+".txt");
        try{
            bool checkedWriting=false;PublishPid(file,12345,delegate{checkedWriting=true;Check(!File.Exists(file),"ready PID path must stay absent while its writer is open");});
            Check(checkedWriting&&int.Parse(File.ReadAllText(file))==12345,"published PID is complete and readable immediately");
        }finally{if(File.Exists(file))File.Delete(file);}
    }
    public static int JobChild(string[] args){
        if(args[0]=="--jev-job-leaf"){Thread.Sleep(30000);return 0;}
        using(var leaf=Process.Start(new ProcessStartInfo{FileName=Assembly.GetExecutingAssembly().Location,Arguments="--jev-job-leaf",UseShellExecute=false,CreateNoWindow=true})){
            PublishPid(args[1],leaf.Id,null);Thread.Sleep(30000);
        }
        return 0;
    }
    static void OwnedTree(){
        var owner=typeof(QuotaProviders).Assembly.GetType("HardwarePulse.JevBrowserProcess");Check(owner!=null,"browser root and all descendants need an owned job before execution");
        string file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"jev-job-"+Guid.NewGuid().ToString("N")+".txt");
        try{
            using(var send=new AnonymousPipeServerStream(PipeDirection.Out,HandleInheritability.Inheritable))using(var receive=new AnonymousPipeServerStream(PipeDirection.In,HandleInheritability.Inheritable)){
                Process leaf=null;
                using(var child=(IDisposable)owner.GetMethod("Start",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{Assembly.GetExecutingAssembly().Location,"--jev-job-root \""+file+"\"",send.ClientSafePipeHandle.DangerousGetHandle(),receive.ClientSafePipeHandle.DangerousGetHandle()})){
                    for(int i=0;i<250&&!File.Exists(file);i++)Thread.Sleep(20);
                    Check(File.Exists(file),"suspended child resumed inside the job and created its fixture descendant");
                    leaf=Process.GetProcessById(int.Parse(File.ReadAllText(file)));Check(!leaf.HasExited,"fixture descendant is live before disposal");
                }
                using(leaf)Check(leaf.WaitForExit(3000),"disposing the owner kills descendants, not just the browser root");
            }
        }finally{if(File.Exists(file))File.Delete(file);}
    }
    public static void Run(){
        Adapter();
        const string payload="0:{\"ok\":true,\"data\":{\"balanceCents\":1000}}\n";
        var body=Decode(Wire(200,"text/x-component; charset=utf-8",payload));
        Check(JevBalance.Decode(body,DateTimeOffset.UtcNow).Balances[0].Amount=="10.00","Flight action body reaches strict money decoder");
        foreach(string invalid in new[]{payload+payload,payload+"1:{\"ok\":true,\"data\":{\"balanceCents\":\"bad\"}}","z:{\"ok\":true,\"data\":{\"balanceCents\":1000}}","<html>not a response</html>"})Check(Failure(Wire(200,"text/x-component",invalid)).Status=="Quota unavailable","malformed or ambiguous Flight is not Live");
        Check(Failure(Wire(200,"text/html",payload)).Status=="Quota unavailable","HTML cannot supply a fake amount");
        var redirected=Wire(307,"","");redirected["loginRedirect"]=true;Check(Failure(redirected).Status=="Session rejected","only a known login redirect identifies rejected session");
        Check(Failure(Wire(403,"text/html","blocked")).Status=="Quota access denied","403 alone is not expired session");
        var challenged=Wire(403,"text/html","");challenged["challenge"]=true;Check(Failure(challenged).Status=="Browser challenge required","challenge remains distinguishable from expired session");
        var limited=Wire(429,"","");limited["retryAfter"]="7200";Check(Failure(limited).RetryAt> DateTimeOffset.UtcNow.AddMinutes(119),"preserve server Retry-After");
        using(var canceled=new CancellationTokenSource()){
            canceled.Cancel();bool rejected=false;try{Adapter().GetMethod("Read").Invoke(null,new object[]{"not-a-session",canceled.Token});}catch(TargetInvocationException e){rejected=e.InnerException is OperationCanceledException;}
            Check(rejected,"pre-cancel must run before credential decoding or browser startup");
        }
        JevProtocolTests.Run();
        PidPublication();
        OwnedTree();
        Console.WriteLine("PASS Jev: fixed response contract, strict Flight, money, redirects, status distinctions, Retry-After, cancellation and private protocol firewall");
    }
    public static int LiveInvalid(){
        try{
            string agent=(string)Adapter().GetMethod("DefaultUserAgent").Invoke(null,null);
            string packed=JevSession.Encode("session=fixture-not-a-real-session",agent);
            try{Adapter().GetMethod("Read").Invoke(null,new object[]{packed,CancellationToken.None});throw new Exception("Invalid session returned data");}
            catch(TargetInvocationException e){var failure=e.InnerException as QuotaFailure;if(failure!=null)Console.WriteLine("Synthetic control only: "+failure.Status+" / HTTP"+failure.HttpStatus+" / "+failure.FailureKind);Check(failure!=null&&failure.HttpStatus==307&&failure.Status=="Session rejected","installed-browser transport must reach normal login rejection, not native403");Console.WriteLine("PASS live invalid-session control: HTTP307 / Session rejected; no real credentials used");}
            return 0;
        }catch(Exception error){Console.Error.WriteLine(error.Message);return 1;}
    }
}
