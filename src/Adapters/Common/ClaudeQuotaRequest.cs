using System;
using System.Threading;

namespace HardwarePulse {
    // Re-read the selected login only after a rejected request. Never renew or persist tokens.
    public static class ClaudeQuotaRequest {
        public static object Read(Func<CancellationToken,string> login,Func<string,CancellationToken,object> request,CancellationToken cancel){
            string token=ReadToken(login,cancel);
            try{return Request(request,token,cancel);}
            catch(QuotaFailure failure){
                if(failure.Status!="Login required")throw;
                string current=ReadToken(login,cancel);
                if(string.Equals(token,current,StringComparison.Ordinal))throw;
                return Request(request,current,cancel);
            }
        }
        static string ReadToken(Func<CancellationToken,string> login,CancellationToken cancel){
            cancel.ThrowIfCancellationRequested();string token=login(cancel);cancel.ThrowIfCancellationRequested();
            if(string.IsNullOrWhiteSpace(token))throw new QuotaFailure("Login required");
            if(token.Length>16384||Array.Exists(token.ToCharArray(),char.IsControl))throw new QuotaFailure("Login unavailable");
            return token;
        }
        static object Request(Func<string,CancellationToken,object> request,string token,CancellationToken cancel){
            cancel.ThrowIfCancellationRequested();var body=request(token,cancel);cancel.ThrowIfCancellationRequested();return body;
        }
    }
}
