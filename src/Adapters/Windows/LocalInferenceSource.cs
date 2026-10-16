using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace HardwarePulse {
    public static class LocalInferenceSource {
        const int Limit=1048576;
        public static bool ValidEndpoint(string endpoint){
            Uri uri;
            return Uri.TryCreate(endpoint,UriKind.Absolute,out uri)&&(uri.Scheme=="http"||uri.Scheme=="https")&&
                (uri.Host=="127.0.0.1"||uri.Host=="[::1]"||uri.Host=="::1")&&uri.UserInfo==""&&uri.Query==""&&uri.Fragment==""&&
                (uri.AbsolutePath=="/"||uri.AbsolutePath=="/metrics");
        }
        public static LocalInferenceReading Read(string provider,string endpoint,CancellationToken cancellation){
            var missing=new LocalInferenceReading{Provider=provider,Observed=DateTimeOffset.UtcNow};
            if(provider!="Strata"||!ValidEndpoint(endpoint)||cancellation.IsCancellationRequested)return missing;
            // This endpoint is local, unauthenticated metadata. Never forward credentials,
            // follow redirects, discover ports, load models or issue completion requests.
            var request=(HttpWebRequest)WebRequest.Create(new Uri(new Uri(endpoint),"/metrics"));
            request.Method="GET";request.Proxy=null;request.AllowAutoRedirect=false;request.UseDefaultCredentials=false;
            request.Credentials=null;request.CookieContainer=null;request.Timeout=2000;request.ReadWriteTimeout=2000;
            using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation)){
                deadline.CancelAfter(2500);
                using(deadline.Token.Register(request.Abort))try{
                    using(var response=(HttpWebResponse)request.GetResponse()){
                        if(response.StatusCode!=HttpStatusCode.OK||response.ContentLength>Limit)return missing;
                        using(var input=response.GetResponseStream())using(var bytes=new MemoryStream()){
                            var buffer=new byte[8192];int count;
                            while((count=input.Read(buffer,0,buffer.Length))>0){deadline.Token.ThrowIfCancellationRequested();if(bytes.Length+count>Limit)return missing;bytes.Write(buffer,0,count);}
                            string text=new UTF8Encoding(false,true).GetString(bytes.ToArray());
#if NET
                            object body=QuotaData.Parse(text);
#else
                            object body=Json.Serializer().DeserializeObject(text);
#endif
                            deadline.Token.ThrowIfCancellationRequested();
                            return LocalInferenceMetrics.Decode(provider,body,DateTimeOffset.UtcNow);
                        }
                    }
                }catch{return missing;}
            }
        }
    }
}
