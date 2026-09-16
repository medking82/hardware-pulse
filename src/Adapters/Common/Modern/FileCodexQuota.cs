using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HardwarePulse {
    // Read-only file login and fixed-endpoint transport. Host owns refresh cadence.
    public class FileCodexQuota:IDisposable {
        const int Limit=1048576;
        const string Endpoint="https://chatgpt.com/backend-api/wham/usage";
        readonly string path;
        readonly HttpClient client;
        public FileCodexQuota(string loginPath):this(loginPath,NewHandler()){}
        public FileCodexQuota(string loginPath,HttpMessageHandler handler){
            path=loginPath??throw new ArgumentNullException("loginPath");
            client=new HttpClient(handler??throw new ArgumentNullException("handler"),true){Timeout=Timeout.InfiniteTimeSpan};
        }
        static HttpClientHandler NewHandler(){return new HttpClientHandler {AllowAutoRedirect=false,UseCookies=false};}
        public static string DefaultLoginPath(){
            string root=Environment.GetEnvironmentVariable("CODEX_HOME");
            if(string.IsNullOrWhiteSpace(root))root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex");
            return Path.Combine(root,"auth.json");
        }
        public QuotaReading Read(CancellationToken cancel){return CodexQuota.Read(()=>ReadLogin(cancel),Request,cancel);}
        object ReadLogin(CancellationToken cancel){
            try{
                using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete,8192,true)){
                    if(input.Length>Limit)throw new QuotaFailure("Login unavailable");
                    return QuotaJson.ReadGraph(input,cancel).GetAwaiter().GetResult();
                }
            }catch(FileNotFoundException){throw new QuotaFailure("Login required");}
            catch(DirectoryNotFoundException){throw new QuotaFailure("Login required");}
        }
        object Request(string token,string account,CancellationToken cancel){
            using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancel)){
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                try{return RequestAsync(token,account,timeout.Token).GetAwaiter().GetResult();}
                catch(OperationCanceledException) when(!cancel.IsCancellationRequested){throw new QuotaFailure("Quota unavailable");}
            }
        }
        async Task<object> RequestAsync(string token,string account,CancellationToken cancel){
            using(var request=new HttpRequestMessage(HttpMethod.Get,Endpoint)){
                request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
                if(account!="")request.Headers.Add("ChatGPT-Account-Id",account);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.UserAgent.ParseAdd("HardwarePulse/"+typeof(FileCodexQuota).Assembly.GetName().Version.ToString(3));
                using(var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancel).ConfigureAwait(false)){
                    int status=(int)response.StatusCode;
                    if(status<200||status>=300)throw new QuotaFailure(status==401||status==403?"Login required":status==429?"Refresh rate limited":"Quota unavailable");
                    if(response.Content.Headers.ContentLength>Limit)throw new QuotaFailure("Quota unavailable");
                    using(var stream=await response.Content.ReadAsStreamAsync(cancel).ConfigureAwait(false))
                        return await QuotaJson.ReadGraph(stream,cancel).ConfigureAwait(false);
                }
            }
        }
        public void Dispose(){client.Dispose();}
    }
}
