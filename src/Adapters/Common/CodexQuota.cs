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
            catch(QuotaFailure e){return new QuotaReading {Provider="Codex",Status=e.Status,Observed=DateTimeOffset.UtcNow,RetryAt=e.RetryAt,HttpStatus=e.HttpStatus,FailureKind=e.FailureKind};}
            catch{cancel.ThrowIfCancellationRequested();return new QuotaReading {Provider="Codex",Status="Quota unavailable",Observed=DateTimeOffset.UtcNow};}
        }
    }
    public sealed class QuotaFailure:Exception {
        public readonly string Status;
        public QuotaFailure(string status){Status=status;}
        public readonly DateTimeOffset? RetryAt;
        public readonly int HttpStatus;
        public readonly string FailureKind;
        public QuotaFailure(string status,DateTimeOffset? retryAt):this(status){RetryAt=retryAt;}
        public QuotaFailure(string status,DateTimeOffset? retryAt,int httpStatus):this(status,retryAt){HttpStatus=httpStatus;}
        public QuotaFailure(string status,DateTimeOffset? retryAt,int httpStatus,string failureKind):this(status,retryAt,httpStatus){FailureKind=failureKind;}
        public static DateTimeOffset? ParseRetryAfter(string value,DateTimeOffset now){
            if(string.IsNullOrWhiteSpace(value)||value.Length>128)return null;
            value=value.Trim();long seconds;
            if(long.TryParse(value,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out seconds)){
                if(seconds<0||seconds>(DateTimeOffset.MaxValue-now).TotalSeconds)return null;
                try{return now.AddSeconds(seconds);}catch(ArgumentOutOfRangeException){return null;}
            }
            DateTimeOffset date;
            if(DateTimeOffset.TryParseExact(value,"r",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.AssumeUniversal,out date))return date;
            return null;
        }
    }
}
