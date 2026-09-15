using System;
using System.Threading;

namespace HardwarePulse {
    // Platform IO supplies bounded login/response graphs. No credential persistence,
    // endpoint selection, HTTP implementation or scheduler belongs in this flow.
    public static class CodexQuota {
        public static QuotaReading Read(Func<object> login,Func<string,string,CancellationToken,object> request,CancellationToken cancel){
            if(login==null)throw new ArgumentNullException("login");
            if(request==null)throw new ArgumentNullException("request");
            try{
                cancel.ThrowIfCancellationRequested();
                var tokens=QuotaDecoder.Get(login(),"tokens");
                string token=QuotaDecoder.Text(QuotaDecoder.Get(tokens,"access_token"));
                if(token=="")throw new QuotaFailure("Login required");
                string account=QuotaDecoder.Text(QuotaDecoder.Get(tokens,"account_id"));
                cancel.ThrowIfCancellationRequested();
                object body=request(token,account,cancel);
                cancel.ThrowIfCancellationRequested();
                return QuotaDecoder.Decode("Codex",body,DateTimeOffset.UtcNow);
            }catch(OperationCanceledException){throw;}
            catch(QuotaFailure e){return new QuotaReading {Provider="Codex",Status=e.Status,Observed=DateTimeOffset.UtcNow};}
            catch{cancel.ThrowIfCancellationRequested();return new QuotaReading {Provider="Codex",Status="Quota unavailable",Observed=DateTimeOffset.UtcNow};}
        }
    }
    public sealed class QuotaFailure:Exception {
        public readonly string Status;
        public QuotaFailure(string status){Status=status;}
    }
}
