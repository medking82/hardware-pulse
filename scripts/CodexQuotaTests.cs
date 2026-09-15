using System;
using System.Collections.Generic;
using System.Threading;
using HardwarePulse;

static class CodexQuotaTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static object Login(object token,object account){return new Dictionary<string,object>{{"tokens",new Dictionary<string,object>{{"access_token",token},{"account_id",account}}}};}
    public static void Run(){
        int logins=0,requests=0;object account="synthetic-account";
        Func<object> login=()=>{logins++;return Login("synthetic-token",account);};
        Func<string,string,CancellationToken,object> request=(token,id,cancel)=>{
            requests++;Check(token=="synthetic-token"&&id==(account as string??""),"Forward exact synthetic credentials and optional account");
            return new Dictionary<string,object>{{"rate_limit",new Dictionary<string,object>{{"secondary_window",new Dictionary<string,object>{{"used_percent",36},{"limit_window_seconds",604800}}}}}};
        };
        var reading=CodexQuota.Read(login,request,CancellationToken.None);
        Check(reading.Provider=="Codex"&&reading.Status=="Live"&&reading.Windows[0].Remaining==64,"Shared response decoder");
        account=null;Check(CodexQuota.Read(login,request,CancellationToken.None).Status=="Live","Missing account remains optional");
        int before=requests;
        foreach(object token in new object[]{null,"",42})Check(CodexQuota.Read(()=>Login(token,null),request,CancellationToken.None).Status=="Login required","Missing or non-string token");
        Check(requests==before,"Invalid login never reaches request");
        Check(CodexQuota.Read(()=>null,request,CancellationToken.None).Status=="Login required","Missing login graph");
        foreach(string status in new[]{"Login required","Login unavailable","Refresh rate limited","Quota unavailable"}){
            Check(CodexQuota.Read(()=>{throw new QuotaFailure(status);},request,CancellationToken.None).Status==status,"Preserve safe source status");
            Check(CodexQuota.Read(login,(t,a,c)=>{throw new QuotaFailure(status);},CancellationToken.None).Status==status,"Preserve safe request status");
        }
        Check(CodexQuota.Read(()=>{throw new Exception("synthetic secret");},request,CancellationToken.None).Status=="Quota unavailable","Do not expose source exception details");
        Check(CodexQuota.Read(login,(t,a,c)=>{throw new Exception("synthetic secret");},CancellationToken.None).Status=="Quota unavailable","Do not expose request exception details");
        Check(CodexQuota.Read(login,(t,a,c)=>new Dictionary<string,object>(),CancellationToken.None).Status!="Live","Empty response never fabricates quota");
        using(var cancel=new CancellationTokenSource()){
            cancel.Cancel();before=logins;
            ExpectCancelled(()=>CodexQuota.Read(login,request,cancel.Token));
            Check(logins==before,"Pre-cancelled read does not access credentials");
        }
        using(var cancel=new CancellationTokenSource()){
            before=requests;
            ExpectCancelled(()=>CodexQuota.Read(()=>{cancel.Cancel();return Login("synthetic-token",null);},request,cancel.Token));
            Check(requests==before,"Cancellation after login prevents request");
        }
        using(var cancel=new CancellationTokenSource()){
            ExpectCancelled(()=>CodexQuota.Read(login,(t,a,c)=>{cancel.Cancel();return new Dictionary<string,object>();},cancel.Token));
        }
        using(var cancel=new CancellationTokenSource()){
            ExpectCancelled(()=>CodexQuota.Read(login,(t,a,c)=>{cancel.Cancel();throw new Exception();},cancel.Token));
        }
        Console.WriteLine("PASS shared Codex flow: credentials, account, decode, safe errors and cancellation; no real IO");
    }
    static void ExpectCancelled(Action action){bool cancelled=false;try{action();}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"Cancellation must propagate");}
}
