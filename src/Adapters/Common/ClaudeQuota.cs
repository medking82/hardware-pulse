using System;
using System.Threading;

namespace HardwarePulse {
    public static class ClaudeQuota {
        static string Access(object login){return QuotaDecoder.Text(QuotaDecoder.Get(QuotaDecoder.Get(login,"claudeAiOauth")??login,"accessToken"));}
        static DateTimeOffset? Expiry(object login){return QuotaDecoder.Time(QuotaDecoder.Get(QuotaDecoder.Get(login,"claudeAiOauth")??login,"expiresAt"));}
        static string Credential(object login){var value=Access(login);if(value.Length>16384||Array.Exists(value.ToCharArray(),char.IsControl))throw new QuotaFailure("Login unavailable");return value;}
        static string Fresh(object login){var expires=Expiry(login);if(expires.HasValue&&expires<=DateTimeOffset.UtcNow)throw new QuotaFailure("Login expired");return Credential(login);}
        public static object Read(Func<object> login,Func<string> environmentToken,Func<string,CancellationToken,object> request,CancellationToken cancel){
            cancel.ThrowIfCancellationRequested();
            string token=environmentToken();bool fromFile=string.IsNullOrWhiteSpace(token);
            if(fromFile)token=Fresh(login());
            else if(token.Length>16384||Array.Exists(token.ToCharArray(),char.IsControl))throw new QuotaFailure("Login unavailable");
            if(string.IsNullOrWhiteSpace(token))throw new QuotaFailure("Login required");
            try{return request(token,cancel);}
            catch(QuotaFailure failure){
                if(failure.Status!="Login required"||!fromFile)throw;
                // Claude Code can rotate its file while a quota request is in flight.
                // Re-read once; do not refresh/write the owning application's credentials.
                cancel.ThrowIfCancellationRequested();
                string current;
                try{current=Fresh(login());}
                catch(QuotaFailure){throw;}
                catch{throw new QuotaFailure(failure.Status);} // Re-read trouble keeps the actionable original status.
                if(current==token||string.IsNullOrWhiteSpace(current))throw new QuotaFailure(failure.Status);
                return request(current,cancel);
            }
        }
    }
}
