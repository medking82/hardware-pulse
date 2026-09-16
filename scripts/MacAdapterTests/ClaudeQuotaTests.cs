using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HardwarePulse;

static class ClaudeQuotaTests {
    sealed class Handler:HttpMessageHandler {
        public Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> Reply;
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel){Calls++;return Reply(request,cancel);}
    }
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static HttpResponseMessage Response(int status,string body)=>new((HttpStatusCode)status){Content=new StringContent(body)};
    public static void Run() {
        string path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"pulse-claude-fixture-"+Guid.NewGuid().ToString("N")+".json");
        try {
            string external=null;
            var login=new ClaudeFileLogin(path,()=>external);
            using var cancel=new CancellationTokenSource();cancel.Cancel();
            try{login.Read(cancel.Token);throw new Exception("Expected cancelled login read");}catch(OperationCanceledException){}
            try{login.Read(CancellationToken.None);throw new Exception("Expected missing login");}catch(QuotaFailure e){Check(e.Status=="Login required","Missing file status");}
            const string json="{\"claudeAiOauth\":{\"accessToken\":\"synthetic-file\"}}";
            System.IO.File.WriteAllText(path,json,new System.Text.UTF8Encoding(true));
            var before=System.IO.File.ReadAllBytes(path);
            Check(login.Read(CancellationToken.None)=="synthetic-file","BOM credential file");
            Check(Convert.ToBase64String(before)==Convert.ToBase64String(System.IO.File.ReadAllBytes(path)),"Credential source remains byte-identical");
            external="synthetic-env";System.IO.File.Delete(path);
            Check(login.Read(CancellationToken.None)==external,"Environment token precedes file access");
            external=null;System.IO.File.WriteAllText(path,"{\"accessToken\":\"synthetic-flat\"}");
            Check(login.Read(CancellationToken.None)=="synthetic-flat","Existing flat credential shape");
            System.IO.File.WriteAllText(path,new string('x',1048577));
            try{login.Read(CancellationToken.None);throw new Exception("Expected size rejection");}catch(QuotaFailure e){Check(e.Status=="Login unavailable","Login size limit");}
        } finally {if(System.IO.File.Exists(path))System.IO.File.Delete(path);}
        var handler=new Handler();string credential="synthetic-first";int reads=0;
        using var client=new ClaudeQuotaClient(cancel=>{reads++;cancel.ThrowIfCancellationRequested();return credential;},handler);
        handler.Reply=(request,cancel)=>{
            Check(request.Method==HttpMethod.Get&&request.RequestUri.AbsoluteUri=="https://api.anthropic.com/api/oauth/usage","Fixed Claude HTTPS GET");
            Check(request.Headers.Authorization.ToString()=="Bearer "+credential,"Current token supplied per refresh");
            Check(string.Join("",request.Headers.GetValues("anthropic-beta"))=="oauth-2025-04-20","Claude OAuth header");
            Check(!request.Headers.Contains("ChatGPT-Account-Id"),"No Codex account header leaks into Claude");
            return Task.FromResult(Response(200,"{\"five_hour\":{\"utilization\":25},\"seven_day\":{\"utilization\":0},\"seven_day_sonnet\":{\"utilization\":100}}"));
        };
        var reading=client.Read(CancellationToken.None);
        Check(reading.Provider=="Claude"&&reading.Status=="Live"&&reading.AllWindows.Count==3,"All Claude windows decoded");
        Check(reading.AllWindows[0].Remaining==75&&reading.AllWindows[1].Remaining==100&&reading.AllWindows[2].Remaining==0,"Real zero and full usage preserved");
        credential="synthetic-second";client.Read(CancellationToken.None);Check(reads==2,"No cached credential");
        int calls=handler.Calls;
        foreach(var token in new[]{"", "  ","injected\r\nheader",new string('a',16385)}) {
            credential=token;reading=client.Read(CancellationToken.None);
            Check(reading.Windows.Count==0&&reading.Status!="Live"&&handler.Calls==calls,"Invalid credentials cannot cause HTTP requests");
        }
        credential="synthetic-third";
        foreach(int status in new[]{301,302,401,403,429,500}) {
            handler.Reply=(_,_)=>Task.FromResult(Response(status,"private response body"));
            reading=client.Read(CancellationToken.None);
            Check(reading.Status==(status==401||status==403?"Login required":status==429?"Refresh rate limited":"Quota unavailable")&&reading.AllWindows.Count==0,"Sanitized status with no stale quota");
        }
        foreach(string body in new[]{"not json","[]",new string('x',1048577)}) {
            handler.Reply=(_,_)=>Task.FromResult(Response(200,body));
            Check(client.Read(CancellationToken.None).Status=="Quota unavailable","Malformed/oversize response rejected");
        }
        using(var cancelled=new CancellationTokenSource()) {
            cancelled.Cancel();int before=reads;
            try{client.Read(cancelled.Token);throw new Exception("Expected cancellation");}catch(OperationCanceledException){}
            Check(reads==before,"Cancellation checked before credential lookup");
        }
        handler.Reply=(_,cancel)=>{var task=new TaskCompletionSource<HttpResponseMessage>();cancel.Register(()=>task.TrySetCanceled(cancel));return task.Task;};
        using(var cancellation=new CancellationTokenSource(TimeSpan.FromMilliseconds(30))) {
            try{client.Read(cancellation.Token);throw new Exception("Expected in-flight cancellation");}catch(OperationCanceledException){}
        }
        using var failure=new ClaudeQuotaClient(_=>throw new Exception("synthetic private credential details"),new Handler());
        Check(failure.Read(CancellationToken.None).Status=="Quota unavailable","Credential exception details are not exposed");
        Console.WriteLine("PASS Claude quota transport: fixed endpoint, all windows, current credentials, errors, bounds and cancellation; synthetic only");
    }
}
