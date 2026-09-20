using System;
using System.Threading;
using HardwarePulse;

static class ClaudeQuotaRequestTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception("Claude recovery: "+message);}
    public static void Run(){
        foreach(string outcome in new[]{"success","Login required","Quota access denied","Refresh rate limited","Quota unavailable"}){
            int reads=0,calls=0;object result=new object();
            try{
                object received=ClaudeQuotaRequest.Read(_=>++reads==1?"old":"new",(token,cancel)=>{
                    calls++;Check(token==(calls==1?"old":"new"),"exact current token");
                    if(outcome!="success"&&(calls==1||outcome!="Login required"))throw new QuotaFailure(outcome);
                    return result;
                },CancellationToken.None);
                Check((outcome=="success"||outcome=="Login required")&&ReferenceEquals(received,result),"unexpected recovery result");
            }catch(QuotaFailure error){Check(outcome!="Login required"&&error.Status==outcome,"preserve failure");}
            Check(reads==(outcome=="Login required"?2:1)&&calls==reads,"only rejected token may cause another read and request");
        }
        foreach(string replacement in new[]{"old","new","","injected\r\nheader",new string('x',16385)}){
            int reads=0,calls=0;bool failed=false;
            try{ClaudeQuotaRequest.Read(_=>++reads==1?"old":replacement,(token,cancel)=>{calls++;throw new QuotaFailure("Login required");},CancellationToken.None);}
            catch(QuotaFailure){failed=true;}
            Check(failed&&reads==2&&calls==(replacement=="new"?2:1),"unchanged/invalid token must not retry; second rejection must stop");
        }
        using(var cancel=new CancellationTokenSource()){
            int reads=0,calls=0;bool cancelled=false;
            try{ClaudeQuotaRequest.Read(_=>{reads++;return "old";},(token,ct)=>{calls++;cancel.Cancel();throw new QuotaFailure("Login required");},cancel.Token);}
            catch(OperationCanceledException){cancelled=true;}
            Check(cancelled&&reads==1&&calls==1,"cancellation must prevent recovery credential access");
        }
        Console.WriteLine("PASS Claude recovery: changed token retries once, unchanged/invalid rejected, 403/429/transport no retry, cancellation; synthetic only");
    }
}
