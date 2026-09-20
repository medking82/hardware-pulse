#if NET
#pragma warning disable SYSLIB0014, SYSLIB0039, CA1416 // Reuse the established Windows transport and credential boundary.
#endif
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
        static object ReadLogin(string path){
            try{
                using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
                using(var memory=new MemoryStream()){
                    if(input.Length>1048576)throw new QuotaFailure("Login unavailable");
                    byte[] buffer=new byte[8192];int count;
                    try{while((count=input.Read(buffer,0,buffer.Length))>0){if(memory.Length+count>1048576)throw new QuotaFailure("Login unavailable");memory.Write(buffer,0,count);}
                        memory.Position=0;using(var reader=new StreamReader(memory,Encoding.UTF8,true,1024,true))return QuotaData.Parse(reader.ReadToEnd());
                    }finally{Array.Clear(buffer,0,buffer.Length);Array.Clear(memory.GetBuffer(),0,(int)memory.Length);}
                }
            }catch(FileNotFoundException){throw new QuotaFailure("Login required");}
            catch(DirectoryNotFoundException){throw new QuotaFailure("Login required");}
        }
        public static QuotaReading Read(string provider,CancellationToken cancel){
            cancel.ThrowIfCancellationRequested();
            if(provider=="Codex"){
                var reading=CodexQuota.Read(()=>ReadLogin(LoginFile("CODEX_HOME",".codex","auth.json")),RequestCodex,cancel);
                // Avoid spawning a CLI for transient network errors or server throttling.
                if(reading.Status!="Login required")return reading;
                string executable=CodexAppServerQuota.InstalledExecutable();if(executable==null)return reading;
                try{var recovered=CodexAppServerQuota.Read(executable,cancel);return recovered.Status=="Live"?recovered:reading;}
                catch{cancel.ThrowIfCancellationRequested();return reading;}
            }
            try{
                object body;
                if(provider=="Antigravity"){
                    try{body=AntigravityQuota.Read(cancel);}
                    catch(QuotaFailure failure){
                        if(failure.Status!="Open Antigravity to read quota")throw;
                        string executable=AntigravityCliQuota.InstalledExecutable();if(executable==null)throw;
                        return AntigravityCliQuota.Read(executable,cancel);
                    }
                }
                else if(provider=="Claude"){
                    string token=Environment.GetEnvironmentVariable("CLAUDE_CODE_OAUTH_TOKEN");
                    if(string.IsNullOrEmpty(token)){var login=ClaudeLogin();token=QuotaDecoder.Text(QuotaDecoder.Get(QuotaDecoder.Get(login,"claudeAiOauth")??login,"accessToken"));}
                    if(string.IsNullOrEmpty(token))throw new QuotaFailure("Login required");
                    body=Request("https://api.anthropic.com/api/oauth/usage",new Dictionary<string,string>{{"Authorization","Bearer "+token},{"anthropic-beta","oauth-2025-04-20"}},null,cancel,false);
                }else throw new QuotaFailure("Quota unavailable");
                cancel.ThrowIfCancellationRequested();return QuotaDecoder.Decode(provider,body,DateTimeOffset.UtcNow);
            }catch(OperationCanceledException){throw;}
            catch(QuotaFailure e){return new QuotaReading{Provider=provider,Status=e.Status,Observed=DateTimeOffset.UtcNow,RetryAt=e.RetryAt};}
            catch{cancel.ThrowIfCancellationRequested();return new QuotaReading{Provider=provider,Status="Quota unavailable",Observed=DateTimeOffset.UtcNow};}
        }
        static object RequestCodex(string token,string account,CancellationToken cancel){
            var headers=new Dictionary<string,string>{{"Authorization","Bearer "+token}};
            if(account!="")headers["ChatGPT-Account-Id"]=account;
            return Request("https://chatgpt.com/backend-api/wham/usage",headers,null,cancel,false);
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
                }catch(WebException e){cancel.ThrowIfCancellationRequested();using(var response=e.Response as HttpWebResponse){int status=response==null?0:(int)response.StatusCode;throw new QuotaFailure(status==401?"Login required":status==403?"Quota access denied":status==429?"Refresh rate limited":"Quota unavailable",status==429?QuotaFailure.ParseRetryAfter(response.Headers["Retry-After"],DateTimeOffset.UtcNow):null);}}
            }
        }
    }
}
