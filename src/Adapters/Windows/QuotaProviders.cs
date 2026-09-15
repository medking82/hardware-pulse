using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;

namespace HardwarePulse {
    public static class QuotaProviders {
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct Credential {public uint Flags,Type;public string TargetName,Comment;public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;public uint BlobSize;public IntPtr Blob;public uint Persist,AttributeCount;public IntPtr Attributes;public string TargetAlias,UserName;}
        [DllImport("advapi32.dll",EntryPoint="CredReadW",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CredRead(string target,uint type,int flags,out IntPtr credential);
        [DllImport("advapi32.dll")]static extern void CredFree(IntPtr credential);
        static object ClaudeLogin(){
            string path=LoginFile("CLAUDE_CONFIG_DIR",".claude",".credentials.json");if(File.Exists(path))return ReadLogin(path);
            foreach(string target in new[]{"Claude Code-credentials","Claude Code-credentials:"+Environment.UserName,"Claude Code-credentials/"+Environment.UserName}){
                IntPtr ptr;if(!CredRead(target,1,0,out ptr))continue;
                try{var credential=(Credential)Marshal.PtrToStructure(ptr,typeof(Credential));if(credential.BlobSize==0||credential.BlobSize>1048576)continue;byte[] bytes=new byte[credential.BlobSize];Marshal.Copy(credential.Blob,bytes,0,bytes.Length);
                    try{return QuotaData.Parse(Encoding.UTF8.GetString(bytes).TrimEnd('\0'));}catch{try{return QuotaData.Parse(Encoding.Unicode.GetString(bytes).TrimEnd('\0'));}catch{}}finally{Array.Clear(bytes,0,bytes.Length);}
                }finally{CredFree(ptr);}
            }
            throw new QuotaFailure("Login required");
        }
        static string LoginFile(string variable,string directory,string file){string root=Environment.GetEnvironmentVariable(variable);if(string.IsNullOrWhiteSpace(root))root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),directory);return Path.Combine(root,file);}
        static object ReadLogin(string path){var info=new FileInfo(path);if(!info.Exists)throw new QuotaFailure("Login required");if(info.Length>1048576)throw new QuotaFailure("Login unavailable");return QuotaData.Parse(File.ReadAllText(path));}
        public static QuotaReading Read(string provider,CancellationToken cancel){
            try{
                object body;
                if(provider=="Antigravity")body=AntigravityQuota.Read(cancel);
                else if(provider=="Codex"){
                    var login=ReadLogin(LoginFile("CODEX_HOME",".codex","auth.json"));var tokens=QuotaData.Get(login,"tokens");string token=QuotaData.Text(QuotaData.Get(tokens,"access_token"));
                    if(token=="")throw new QuotaFailure("Login required");
                    var headers=new Dictionary<string,string>{{"Authorization","Bearer "+token}};string account=QuotaData.Text(QuotaData.Get(tokens,"account_id"));if(account!="")headers["ChatGPT-Account-Id"]=account;
                    body=Request("https://chatgpt.com/backend-api/wham/usage",headers,null,cancel,false);
                }else if(provider=="Claude"){
                    string token=Environment.GetEnvironmentVariable("CLAUDE_CODE_OAUTH_TOKEN");
                    if(string.IsNullOrEmpty(token)){var login=ClaudeLogin();token=QuotaData.Text(QuotaData.Get(QuotaData.Get(login,"claudeAiOauth")??login,"accessToken"));}
                    if(string.IsNullOrEmpty(token))throw new QuotaFailure("Login required");
                    body=Request("https://api.anthropic.com/api/oauth/usage",new Dictionary<string,string>{{"Authorization","Bearer "+token},{"anthropic-beta","oauth-2025-04-20"}},null,cancel,false);
                }else throw new QuotaFailure("Quota unavailable");
                cancel.ThrowIfCancellationRequested();return QuotaData.Decode(provider,body,DateTimeOffset.UtcNow);
            }catch(OperationCanceledException){throw;}
            catch(QuotaFailure e){return new QuotaReading{Provider=provider,Status=e.Status,Observed=DateTimeOffset.UtcNow};}
            catch{cancel.ThrowIfCancellationRequested();return new QuotaReading{Provider=provider,Status="Quota unavailable",Observed=DateTimeOffset.UtcNow};}
        }
        // All callers supply fixed endpoints or a verified local process endpoint. Never follow redirects with login headers.
        internal static object Request(string url,Dictionary<string,string> headers,string body,CancellationToken cancel,bool local){
            cancel.ThrowIfCancellationRequested();var uri=new Uri(url);
            if(local?uri.Host!="127.0.0.1":url!="https://chatgpt.com/backend-api/wham/usage"&&url!="https://api.anthropic.com/api/oauth/usage")throw new QuotaFailure("Quota unavailable");
            // Enable TLS 1.2 and TLS 1.3 (numeric value for the older compiler).
            // Pinning TLS 1.2 fails the Codex endpoint on current Windows configurations.
            ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12 | (SecurityProtocolType)12288;
            var request=(HttpWebRequest)WebRequest.Create(uri);request.AllowAutoRedirect=false;request.Timeout=10000;request.ReadWriteTimeout=10000;request.Accept="application/json";request.UserAgent="HardwarePulse/"+typeof(QuotaProviders).Assembly.GetName().Version.ToString(3);request.KeepAlive=false;
            if(local){request.Proxy=null;if(uri.Scheme=="https")request.ServerCertificateValidationCallback=(sender,cert,chain,errors)=>true;}
            foreach(var pair in headers)request.Headers[pair.Key]=pair.Value;
            using(cancel.Register(request.Abort)){
                try{
                    if(body!=null){request.Method="POST";request.ContentType="application/json";byte[] bytes=Encoding.UTF8.GetBytes(body);request.ContentLength=bytes.Length;using(var output=request.GetRequestStream())output.Write(bytes,0,bytes.Length);}
                    using(var response=(HttpWebResponse)request.GetResponse()){
                        if((int)response.StatusCode<200||(int)response.StatusCode>=300)throw new QuotaFailure("Quota unavailable");
                        using(var stream=response.GetResponseStream())using(var memory=new MemoryStream()){
                            byte[] buffer=new byte[8192];int count;while((count=stream.Read(buffer,0,buffer.Length))>0){cancel.ThrowIfCancellationRequested();if(memory.Length+count>1048576)throw new QuotaFailure("Quota unavailable");memory.Write(buffer,0,count);}
                            return QuotaData.Parse(Encoding.UTF8.GetString(memory.ToArray()));
                        }
                    }
                }catch(WebException e){cancel.ThrowIfCancellationRequested();using(var response=e.Response as HttpWebResponse){int status=response==null?0:(int)response.StatusCode;throw new QuotaFailure(status==401||status==403?"Login required":status==429?"Refresh rate limited":"Quota unavailable");}}
            }
        }
    }
    internal sealed class QuotaFailure:Exception {internal readonly string Status;internal QuotaFailure(string status){Status=status;}}
}
