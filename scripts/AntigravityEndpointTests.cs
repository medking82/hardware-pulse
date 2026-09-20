using System;
using System.Reflection;
using HardwarePulse;

static class AntigravityEndpointTests {
    static readonly MethodInfo Probe=typeof(QuotaProviders).Assembly.GetType("HardwarePulse.AntigravityQuota").GetMethod("TryReadEndpoint",BindingFlags.NonPublic|BindingFlags.Static);
    static void Check(bool ok,string message){if(!ok)throw new Exception("Antigravity endpoint: "+message);}
    static object Read(Func<object> request,ref string failure){
        object[] arguments={request,failure};
        try{return Probe.Invoke(null,arguments);}catch(TargetInvocationException error){throw error.InnerException;}
        finally{failure=(string)arguments[1];}
    }
    public static void Run(){
        foreach(string status in new[]{"Refresh rate limited","Quota access denied"}){
            var expected=new QuotaFailure(status,status=="Refresh rate limited"?(DateTimeOffset?)DateTimeOffset.UtcNow.AddMinutes(12):null);
            string failure="Quota unavailable";bool rejected=false;int calls=0;
            try{Read(()=>{calls++;throw expected;},ref failure);}catch(QuotaFailure error){rejected=object.ReferenceEquals(error,expected);}
            Check(rejected&&calls==1,status+" must reach provider unchanged, including RetryAt");
        }
        foreach(string status in new[]{"Quota unavailable","Login required"}){
            string failure="Quota unavailable";
            Check(Read(()=>{throw new QuotaFailure(status);},ref failure)==null,"probe failure should allow next endpoint");
            Check(failure==(status=="Login required"?status:"Quota unavailable"),"existing probe status changed");
        }
        string state="Quota unavailable";
        Check(Read(()=>QuotaData.Parse("{}"),ref state)==null,"empty response cannot become Live");
        var body=QuotaData.Parse("{\"groups\":[{\"displayName\":\"Gemini Models\",\"buckets\":[{\"window\":\"weekly\",\"remainingFraction\":0.4}]}]}");
        Check(object.ReferenceEquals(body,Read(()=>body,ref state)),"valid quota body must survive");
        bool canceled=false;try{Read(()=>{throw new OperationCanceledException();},ref state);}catch(OperationCanceledException){canceled=true;}
        Check(canceled,"cancellation swallowed");
        Console.WriteLine("PASS Antigravity endpoint: rate-limit deadline/access denial propagation, transport/auth probes, missing/live body and cancellation; synthetic only");
    }
}
