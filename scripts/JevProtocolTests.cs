using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using HardwarePulse;
internal static class JevProtocolTests {
    static void Check(bool value,string message){if(!value)throw new Exception("Jev protocol: "+message);}
    public static void Run(){foreach(int status in new[]{200,307,429})Conversation(status);}
    static void Conversation(int status){
        var type=typeof(JevQuota).GetNestedType("Conversation",BindingFlags.NonPublic);var frames=new List<object>();
        frames.Add(new{id=1,result=new{browserContextId="fixture-context"}});frames.Add(new{id=2,result=new{targetId="fixture-target"}});frames.Add(new{id=3,result=new{sessionId="fixture-session"}});
        foreach(int id in new[]{4,5,6,7,8})frames.Add(new{id=id,result=new{}});
        frames.Add(new{sessionId="fixture-session",method="Fetch.requestPaused",@params=new{requestId="fixture-document",resourceType="Document",request=new{url=JevQuota.Endpoint,method="GET"}}});
        frames.Add(new{sessionId="fixture-session",method="Page.loadEventFired",@params=new{}});frames.Add(new{id=9,result=new{}});
        frames.Add(new{id=11,result=new{result=new{value=JevQuota.Origin}}});
        frames.Add(new{sessionId="fixture-session",method="Fetch.requestPaused",@params=new{requestId="fixture-foreign",resourceType="Fetch",request=new{url="https://example.invalid/collect",method="POST"}}});
        frames.Add(new{sessionId="fixture-session",method="Network.requestWillBeSent",@params=new{requestId="fixture-network",request=new{url=JevQuota.Endpoint,method="POST"}}});
        frames.Add(new{sessionId="fixture-session",method="Fetch.requestPaused",@params=new{requestId="fixture-read",resourceType="Fetch",request=new{url=JevQuota.Endpoint,method="POST",headers=new Dictionary<string,object>{{"Cookie","fixture-discarded"},{"Accept","text/x-component"}}}}});
        frames.Add(new{sessionId="fixture-session",method="Fetch.requestPaused",@params=new{requestId="fixture-repeat",resourceType="Fetch",request=new{url=JevQuota.Endpoint,method="POST"}}});
        frames.Add(new{sessionId="fixture-session",method="Network.responseReceivedExtraInfo",@params=new{requestId="fixture-network",statusCode=status,headers=new Dictionary<string,object>{{"location","/login"},{"retry-after","7200"}}}});
        var wire=new Dictionary<string,object>{{"http",status==200?200:0},{"contentType","text/x-component"},{"text","0:{\"ok\":true,\"data\":{\"balanceCents\":1000}}\n"}};
        frames.Add(new{id=12,result=new{result=new{value=wire}}});
        string transcript=string.Join("\0",frames.Select(value=>Json.Serializer().Serialize(value)))+"\0";
        using(var input=new MemoryStream(Encoding.UTF8.GetBytes(transcript)))using(var output=new MemoryStream()){
            var fixture=(IDisposable)Activator.CreateInstance(type,BindingFlags.NonPublic|BindingFlags.Instance,null,new object[]{output,input,new JevSession{Cookie="session=fixture-private",UserAgent="fixture-browser"},CancellationToken.None},null);
            using(fixture){
                try{var result=type.GetMethod("ReadBalance",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(fixture,null);Check(status==200&&JevBalance.Decode(result,DateTimeOffset.UtcNow).Status=="Live","valid result reaches the money model");}
                catch(TargetInvocationException e){var failure=e.InnerException as QuotaFailure;Check(failure!=null&&failure.HttpStatus==status&&(status==307?failure.Status=="Session rejected":status==429&&failure.RetryAt>DateTimeOffset.UtcNow.AddMinutes(119)),"opaque redirects and 429 retain correlated safe metadata");}
            }
            var sent=Encoding.UTF8.GetString(output.ToArray()).Split(new[]{'\0'},StringSplitOptions.RemoveEmptyEntries).Select(QuotaData.Parse).ToArray();
            var expression=sent.Where(row=>QuotaDecoder.Text(QuotaDecoder.Get(row,"method"))=="Runtime.evaluate").Select(row=>QuotaDecoder.Text(QuotaDecoder.Get(QuotaDecoder.Get(row,"params"),"expression"))).Single(value=>value!=null&&value.Contains("fetch("));
            // Assert the emitted request, independently of the production pin and fixture request headers.
            Check(expression.Contains("'Next-Action':'00b163e3f5850493c1a7aea4324b5defb38da0f21a'"),"billing balance fetch must use the confirmed getBillingBalanceResult action ID");
            Check(expression.Contains("body:'[]'"),"billing balance action must retain its zero-argument body");
            Check(expression.Contains("fetch('/usage',{")&&expression.Contains("method:'POST'")&&expression.Contains("credentials:'include'")&&expression.Contains("redirect:'manual'"),"read-only balance request must keep its fixed path, POST, browser credentials and manual redirect contract");
            Check(expression.Contains("'Accept':'text/x-component'")&&expression.Contains("'Content-Type':'text/plain;charset=UTF-8'"),"billing balance request must retain its Flight action headers");
            Check(sent.Count(row=>QuotaDecoder.Text(QuotaDecoder.Get(row,"method"))=="Fetch.continueRequest")==1,"only one POST is admitted");
            var admitted=sent.Single(row=>QuotaDecoder.Text(QuotaDecoder.Get(row,"method"))=="Fetch.continueRequest");
            Check(QuotaDecoder.Text(QuotaDecoder.Get(QuotaDecoder.Get(admitted,"params"),"requestId"))=="fixture-read","credential is attached only to the fixed-origin request");
            Check(sent.Count(row=>Json.Serializer().Serialize(row).Contains("fixture-private"))==1&&!Encoding.UTF8.GetString(output.ToArray()).Contains("fixture-discarded"),"no credential in script, foreign request, or duplicate Cookie header");
            Check(sent.Count(row=>QuotaDecoder.Text(QuotaDecoder.Get(row,"method"))=="Fetch.failRequest")==2,"foreign destination and second POST are blocked");
        }
    }
}
