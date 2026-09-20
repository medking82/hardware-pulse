using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace HardwarePulse {
    // Platform credential adapters supply a token on demand. This client never persists it.
    public sealed class ClaudeQuotaClient:IDisposable {
        const string Endpoint="https://api.anthropic.com/api/oauth/usage";
        readonly Func<CancellationToken,string> token;
        readonly HttpClient client;
        public ClaudeQuotaClient(Func<CancellationToken,string> token):this(token,new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false}){}
        public ClaudeQuotaClient(Func<CancellationToken,string> token,HttpMessageHandler handler) {
            this.token=token??throw new ArgumentNullException(nameof(token));
            client=new HttpClient(handler??throw new ArgumentNullException(nameof(handler)),true){Timeout=Timeout.InfiniteTimeSpan};
        }
        public QuotaReading Read(CancellationToken cancel) {
            try {
                cancel.ThrowIfCancellationRequested();
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancel);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                object body;
                try {body=ClaudeQuotaRequest.Read(token,(accessToken,requestCancel)=>Request(accessToken,requestCancel).GetAwaiter().GetResult(),timeout.Token);}
                catch(OperationCanceledException) when(!cancel.IsCancellationRequested){throw new QuotaFailure("Quota unavailable");}
                cancel.ThrowIfCancellationRequested();
                return QuotaDecoder.Decode("Claude",body,DateTimeOffset.UtcNow);
            }catch(OperationCanceledException){throw;}
            catch(QuotaFailure failure){var reading=Unavailable(failure.Status);reading.RetryAt=failure.RetryAt;return reading;}
            catch {cancel.ThrowIfCancellationRequested();return Unavailable("Quota unavailable");}
        }
        static QuotaReading Unavailable(string status)=>new QuotaReading{Provider="Claude",Status=status,Observed=DateTimeOffset.UtcNow};
        async Task<object> Request(string accessToken,CancellationToken cancel) {
            using var request=new HttpRequestMessage(HttpMethod.Get,Endpoint);
            request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",accessToken);
            request.Headers.Add("anthropic-beta","oauth-2025-04-20");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.UserAgent.ParseAdd("HardwarePulse/"+typeof(ClaudeQuotaClient).Assembly.GetName().Version.ToString(3));
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancel).ConfigureAwait(false);
            int status=(int)response.StatusCode;
            if(status<200||status>=300)throw new QuotaFailure(status==401?"Login required":status==403?"Quota access denied":status==429?"Refresh rate limited":"Quota unavailable",status==429?QuotaFailure.ParseRetryAfter(response.Headers.RetryAfter?.ToString(),DateTimeOffset.UtcNow):null);
            if(response.Content.Headers.ContentLength>1048576)throw new QuotaFailure("Quota unavailable");
            using var stream=await response.Content.ReadAsStreamAsync(cancel).ConfigureAwait(false);
            return await QuotaJson.ReadGraph(stream,cancel).ConfigureAwait(false);
        }
        public void Dispose()=>client.Dispose();
    }
}
