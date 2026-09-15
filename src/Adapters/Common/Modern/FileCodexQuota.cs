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
                    return ReadGraph(input,cancel).GetAwaiter().GetResult();
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
                        return await ReadGraph(stream,cancel).ConfigureAwait(false);
                }
            }
        }
        static async Task<object> ReadGraph(Stream stream,CancellationToken cancel){
            using(var memory=new MemoryStream()){
                var buffer=new byte[8192];int count;
                while((count=await stream.ReadAsync(buffer,0,buffer.Length,cancel).ConfigureAwait(false))>0){
                    if(memory.Length+count>Limit)throw new QuotaFailure("Quota unavailable");
                    memory.Write(buffer,0,count);
                }
                cancel.ThrowIfCancellationRequested();
                var bytes=memory.GetBuffer();int offset=memory.Length>=3&&bytes[0]==239&&bytes[1]==187&&bytes[2]==191?3:0;
                using(var document=JsonDocument.Parse(bytes.AsMemory(offset,(int)memory.Length-offset),new JsonDocumentOptions {MaxDepth=32})){
                    if(document.RootElement.ValueKind!=JsonValueKind.Object)throw new QuotaFailure("Quota unavailable");
                    return Graph(document.RootElement);
                }
            }
        }
        static object Graph(JsonElement value){
            switch(value.ValueKind){
                case JsonValueKind.Object:
                    var map=new Dictionary<string,object>();foreach(var property in value.EnumerateObject())map[property.Name]=Graph(property.Value);return map;
                case JsonValueKind.Array:
                    var list=new List<object>();foreach(var item in value.EnumerateArray())list.Add(Graph(item));return list;
                case JsonValueKind.String:return value.GetString();
                case JsonValueKind.Number:if(value.TryGetInt64(out long integer))return integer;return value.GetDouble();
                case JsonValueKind.True:return true;
                case JsonValueKind.False:return false;
                default:return null;
            }
        }
        public void Dispose(){client.Dispose();}
    }
}
