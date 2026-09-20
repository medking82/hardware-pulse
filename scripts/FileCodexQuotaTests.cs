using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HardwarePulse;

static class FileCodexQuotaTests {
    sealed class UnknownLengthStream:MemoryStream {
        public UnknownLengthStream(byte[] bytes):base(bytes){}
        public override bool CanSeek {get{return false;}}
    }
    sealed class Handler:HttpMessageHandler {
        public Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> Reply;
        public int Calls;public bool Disposed;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel){Calls++;return Reply(request,cancel);}
        protected override void Dispose(bool disposing){Disposed=true;base.Dispose(disposing);}
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static HttpResponseMessage Response(int status,string body){return new HttpResponseMessage((HttpStatusCode)status){Content=new StringContent(body)};}
    public static void Run(Func<string,HttpMessageHandler,FileCodexQuota> create){
        // Only this generated synthetic fixture is read or removed. Never resolve real login.
        string path=Path.Combine(Path.GetTempPath(),"pulse-codex-fixture-"+Guid.NewGuid().ToString("N")+".json");
        const string login="{\"tokens\":{\"access_token\":\"synthetic-token\",\"account_id\":\"synthetic-account\"}}";
        var handler=new Handler();
        try{
            File.WriteAllText(path,login);
            using(var adapter=create(path,handler)){
                handler.Reply=(request,cancel)=>{
                    Check(request.Method==HttpMethod.Get&&request.RequestUri.AbsoluteUri=="https://chatgpt.com/backend-api/wham/usage","Fixed HTTPS GET endpoint");
                    Check(request.Headers.Authorization.ToString()=="Bearer synthetic-token","Bearer credential");
                    Check(string.Join("",request.Headers.GetValues("ChatGPT-Account-Id"))=="synthetic-account","Account header");
                    return Task.FromResult(Response(200,"{\"rate_limit\":{\"secondary_window\":{\"used_percent\":36,\"limit_window_seconds\":604800}}}"));
                };
                var reading=adapter.Read(CancellationToken.None);
                Check(reading.Status=="Live"&&reading.Windows[0].Remaining==64,"File to HTTP to shared decoder");
                File.WriteAllText(path,login,new System.Text.UTF8Encoding(true));
                Check(adapter.Read(CancellationToken.None).Status=="Live","UTF-8 BOM login accepted");
                handler.Reply=(r,c)=>{var response=Response(429,"{}");response.Headers.RetryAfter=new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMinutes(10));return Task.FromResult(response);};
                reading=adapter.Read(CancellationToken.None);
                Check(reading.Status=="Refresh rate limited"&&reading.RetryAt>DateTimeOffset.UtcNow.AddMinutes(9)&&reading.RetryAt<DateTimeOffset.UtcNow.AddMinutes(11),"Codex Retry-After reaches scheduler metadata");
                foreach(int status in new[]{301,302,307,308,401,403,429,500}){
                    handler.Reply=(r,c)=>{var response=Response(status,"{}");response.Headers.Location=new Uri("https://example.invalid/no-credentials");return Task.FromResult(response);};
                    int calls=handler.Calls;
                    Check(adapter.Read(CancellationToken.None).Status==(status==401?"Login required":status==403?"Quota access denied":status==429?"Refresh rate limited":"Quota unavailable"),"HTTP safe status mapping");
                    Check(handler.Calls==calls+1,"No additional request on redirect");
                }
                foreach(string body in new[]{"invalid","[]",new string(' ',1048577),"{\"x\":"+new string('[',40)+"0"+new string(']',40)+"}"}){
                    handler.Reply=(r,c)=>Task.FromResult(Response(200,body));
                    Check(adapter.Read(CancellationToken.None).Status=="Quota unavailable","Bounded response parsing");
                }
                handler.Reply=(r,c)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StreamContent(new UnknownLengthStream(new byte[1048577]))});
                Check(adapter.Read(CancellationToken.None).Status=="Quota unavailable","Streaming limit without Content-Length");
                handler.Reply=async(r,c)=>{await Task.Delay(Timeout.Infinite,c);return Response(200,"{}");};
                var elapsed=System.Diagnostics.Stopwatch.StartNew();
                Check(adapter.Read(CancellationToken.None).Status=="Quota unavailable"&&elapsed.Elapsed.TotalSeconds<20,"Deadline maps to safe status without caller cancellation");
                int before=handler.Calls;
                File.WriteAllText(path,new string(' ',1048577));Check(adapter.Read(CancellationToken.None).Status=="Login unavailable","Bounded login file");
                File.WriteAllText(path,"{}");Check(adapter.Read(CancellationToken.None).Status=="Login required","Missing token");
                File.Delete(path);Check(adapter.Read(CancellationToken.None).Status=="Login required","Missing file");
                Check(handler.Calls==before,"Bad login never sends credentials");
                File.WriteAllText(path,login);
                using(var cancelled=new CancellationTokenSource()){
                    cancelled.Cancel();bool propagated=false;try{adapter.Read(cancelled.Token);}catch(OperationCanceledException){propagated=true;}
                    Check(propagated&&handler.Calls==before,"Pre-cancelled request");
                }
                using(var cancelled=new CancellationTokenSource()){
                    handler.Reply=async(r,c)=>{cancelled.Cancel();await Task.Delay(Timeout.Infinite,c);return Response(200,"{}");};
                    bool propagated=false;try{adapter.Read(cancelled.Token);}catch(OperationCanceledException){propagated=true;}Check(propagated,"In-flight cancellation");
                }
                handler.Reply=(r,c)=>throw new HttpRequestException("synthetic-secret");
                Check(adapter.Read(CancellationToken.None).Status=="Quota unavailable","Exception details not exposed");
            }
            Check(handler.Disposed,"Transport disposed with adapter");
            using(var native=(HttpClientHandler)typeof(FileCodexQuota).GetMethod("NewHandler",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null))
                Check(!native.AllowAutoRedirect&&!native.UseCookies,"Actual production handler rejects redirects and ambient cookies");
        }finally{if(File.Exists(path))File.Delete(path);}
        Console.WriteLine("PASS shared Codex file/HTTP adapter: synthetic credentials, bounds, safe errors, redirects, cancellation and disposal");
    }
}
