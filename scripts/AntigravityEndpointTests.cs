using System;
using System.Reflection;
using HardwarePulse;

static class AntigravityEndpointTests {
    static readonly MethodInfo Probe=typeof(QuotaProviders).Assembly.GetType("HardwarePulse.AntigravityQuota").GetMethod("TryReadEndpoint",BindingFlags.NonPublic|BindingFlags.Static);
    static readonly MethodInfo DiagnosticProbe=typeof(QuotaProviders).Assembly.GetType("HardwarePulse.AntigravityQuota").GetMethod("TryReadEndpointDiagnostic",BindingFlags.NonPublic|BindingFlags.Static);
    static void Check(bool ok,string message){if(!ok)throw new Exception("Antigravity endpoint: "+message);}
    static object Read(Func<object> request,ref string failure){
        object[] arguments={request,failure};
        try{return Probe.Invoke(null,arguments);}catch(TargetInvocationException error){throw error.InnerException;}
        finally{failure=(string)arguments[1];}
    }
    static object ReadDiagnostic(Func<object> request,ref string failure,ref QuotaFailure endpointFailure){
        object[] arguments={request,failure,endpointFailure};
        try{return DiagnosticProbe.Invoke(null,arguments);}
        catch(TargetInvocationException error){throw error.InnerException;}
        finally{failure=(string)arguments[1];endpointFailure=(QuotaFailure)arguments[2];}
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
        foreach(string kind in new[]{"HTTP response","Transport timeout","Transport failure"}){
            string diagnosticState="Quota unavailable";QuotaFailure endpointFailure=null;
            ReadDiagnostic(()=>{throw new QuotaFailure(kind=="HTTP response"?"Login required":"Quota unavailable",null,kind=="HTTP response"?401:0,kind);},ref diagnosticState,ref endpointFailure);
            Check(endpointFailure!=null&&endpointFailure.FailureKind==kind,"failure kind preserved");
            Check(diagnosticState==(kind=="HTTP response"?"Login required":"Quota unavailable"),"failure status preserved");
        }
        string invalidState="Quota unavailable";QuotaFailure invalidFailure=null;
        ReadDiagnostic(()=>QuotaData.Parse("{}"),ref invalidState,ref invalidFailure);
        Check(invalidFailure!=null&&invalidFailure.FailureKind=="Invalid response"&&invalidFailure.Status=="Quota unavailable","invalid response classified safely");
        string retainedState="Quota unavailable";QuotaFailure retainedFailure=null;
        ReadDiagnostic(()=>{throw new QuotaFailure("Login required",null,401,"HTTP response");},ref retainedState,ref retainedFailure);
        ReadDiagnostic(()=>{throw new QuotaFailure("Quota unavailable",null,500,"HTTP response");},ref retainedState,ref retainedFailure);
        Check(retainedState=="Login required"&&retainedFailure.HttpStatus==401&&retainedFailure.FailureKind=="HTTP response","prior 401 metadata survives later non-auth failure");
        retainedFailure=null;retainedState="Quota unavailable";
        ReadDiagnostic(()=>{throw new QuotaFailure("Login required",null,401,"HTTP response");},ref retainedState,ref retainedFailure);
        ReadDiagnostic(()=>QuotaData.Parse("{}"),ref retainedState,ref retainedFailure);
        Check(retainedState=="Login required"&&retainedFailure.HttpStatus==401&&retainedFailure.FailureKind=="HTTP response","prior 401 metadata survives invalid response");
        Console.WriteLine("PASS Antigravity endpoint: rate-limit deadline/access denial propagation, transport/auth probes, missing/live body and cancellation; synthetic only");
    }
}
