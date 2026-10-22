#if NET
#pragma warning disable CA1416
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace HardwarePulse {
    // Private inherited pipes, not a TCP debugging server. Never attaches to a user's browser.
    // A local empty document establishes the fixed origin; every other network request is blocked
    // except one explicit read-only balance action. The cookie is injected only for that POST.
    public static class JevQuota {
        public const string Origin="https://console.typesafe.ai";
        public const string Endpoint=Origin+"/usage";
        // Pinned zero-argument getBillingBalanceResult from the 2026-10-22 console client.
        const string ActionId="00b163e3f5850493c1a7aea4324b5defb38da0f21a";
        public static string InstalledBrowser(){
            if(Environment.OSVersion.Platform!=PlatformID.Win32NT)return null;
            foreach(string relative in new[]{"Google/Chrome/Application/chrome.exe","Microsoft/Edge/Application/msedge.exe"})
                foreach(var folder in new[]{Environment.SpecialFolder.ProgramFiles,Environment.SpecialFolder.ProgramFilesX86,Environment.SpecialFolder.LocalApplicationData}){
                    string root=Environment.GetFolderPath(folder);if(string.IsNullOrEmpty(root))continue;
                    string path=Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar));
                    try{if(File.Exists(path)&&FileVersionInfo.GetVersionInfo(path).FileMajorPart>=112)return path;}catch{}
                }
            return null;
        }
        public static string DefaultUserAgent(){
            string browser=InstalledBrowser();if(browser==null)return "";
            int major=FileVersionInfo.GetVersionInfo(browser).FileMajorPart;
            return "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/"+major+".0.0.0 Safari/537.36";
        }
        public static bool OpenConsole(){try{string browser=InstalledBrowser();if(browser==null)return false;using(var process=Process.Start(new ProcessStartInfo{FileName=browser,Arguments=Endpoint,UseShellExecute=false})){}return true;}catch{return false;}}
        public static object Read(string encoded,CancellationToken cancel){
            cancel.ThrowIfCancellationRequested();
#if NET
            // The shared preview has no session editor and does not advertise this source.
            throw new QuotaFailure("Quota unavailable",null,0,"Unsupported platform");
#else
            var session=JevSession.Decode(encoded);if(session==null)throw new QuotaFailure("Session required");
            string browser=InstalledBrowser();if(browser==null)throw new QuotaFailure("Chrome or Edge required");
            string profile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"HardwarePulse","browser-runtime",Guid.NewGuid().ToString("N"));
            var security=new DirectorySecurity();security.SetAccessRuleProtection(true,false);
            security.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
            Directory.CreateDirectory(profile,security);
            try{
                using(var send=new AnonymousPipeServerStream(PipeDirection.Out,HandleInheritability.Inheritable))
                using(var receive=new AnonymousPipeServerStream(PipeDirection.In,HandleInheritability.Inheritable)){
                    string args="--headless=new --disable-gpu --disable-background-networking --disable-component-update --disable-sync --disable-extensions --no-first-run --no-default-browser-check --remote-debugging-pipe --remote-debugging-io-pipes="+send.GetClientHandleAsString()+","+receive.GetClientHandleAsString()+" --user-data-dir=\""+profile+"\" about:blank";
                    using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancel)){
                        deadline.CancelAfter(TimeSpan.FromSeconds(25));
                        using(var child=JevBrowserProcess.Start(browser,args,send.ClientSafePipeHandle.DangerousGetHandle(),receive.ClientSafePipeHandle.DangerousGetHandle()))
                        using(deadline.Token.Register(child.Stop)){
                        send.DisposeLocalCopyOfClientHandle();receive.DisposeLocalCopyOfClientHandle();
                        try{
                            using(var input=new QuotaPipeStream(receive,deadline.Token))using(var pipe=new Conversation(send,input,session,deadline.Token)){
                                try{return pipe.ReadBalance();}finally{pipe.CloseBrowser();}
                            }
                        }catch{cancel.ThrowIfCancellationRequested();if(deadline.IsCancellationRequested)throw new QuotaFailure("Quota unavailable",null,0,"Transport timeout");throw;}
                        }
                    }
                }
            }finally{
                session.Cookie=null;session.UserAgent=null;
                for(int attempt=0;attempt<8;attempt++){try{if(Directory.Exists(profile))Directory.Delete(profile,true);break;}catch(IOException){Thread.Sleep(50);}catch(UnauthorizedAccessException){break;}}
            }
#endif
        }
        public static object Decode(Dictionary<string,object> wire,DateTimeOffset now){
            int http=(int)(QuotaDecoder.Number(QuotaDecoder.Get(wire,"http"))??0);
            if(QuotaDecoder.Text(QuotaDecoder.Get(wire,"error"))=="Response too large")throw new QuotaFailure("Quota unavailable",null,http,"Response too large");
            if(object.Equals(QuotaDecoder.Get(wire,"actionNotFound"),true))throw new QuotaFailure("Console changed",null,http,"Invalid response");
            if(http==429)throw new QuotaFailure("Refresh rate limited",QuotaFailure.ParseRetryAfter(QuotaDecoder.Text(QuotaDecoder.Get(wire,"retryAfter")),now),http,"HTTP response");
            if(http==401||http>=300&&http<400&&object.Equals(QuotaDecoder.Get(wire,"loginRedirect"),true))throw new QuotaFailure("Session rejected",null,http,"HTTP response");
            if(object.Equals(QuotaDecoder.Get(wire,"challenge"),true))throw new QuotaFailure("Browser challenge required",null,http,"HTTP response");
            if(http==403)throw new QuotaFailure("Quota access denied",null,http,"HTTP response");
            string type=QuotaDecoder.Text(QuotaDecoder.Get(wire,"contentType")).Split(';')[0].Trim();
            string text=QuotaDecoder.Text(QuotaDecoder.Get(wire,"text"));
            if(http!=200||type!="text/x-component"||text.Length>65536)throw new QuotaFailure("Quota unavailable",null,http,"Invalid response");
            object selected=null;int candidates=0;
            foreach(string line in text.Split('\n')){
                int split=line.IndexOf(':');if(split<1||split>32)continue;
                bool hex=true;for(int i=0;i<split;i++)if(!Uri.IsHexDigit(line[i]))hex=false;if(!hex)continue;
                object row;try{row=QuotaData.Parse(line.Substring(split+1));}catch{continue;}
                if(!object.Equals(QuotaDecoder.Get(row,"ok"),true))continue;
                candidates++;selected=row;
            }
            if(candidates!=1||JevBalance.Decode(selected,now).Status!="Live")throw new QuotaFailure("Quota unavailable",null,http,"Invalid response");
            return selected;
        }
        static string Json(object value){
#if NET
            return System.Text.Json.JsonSerializer.Serialize(value);
#else
            return new System.Web.Script.Serialization.JavaScriptSerializer{MaxJsonLength=1048576,RecursionLimit=32}.Serialize(value);
#endif
        }
        sealed class Conversation:IDisposable {
            readonly Stream send;
            readonly BufferedStream input;
            readonly JevSession session;
            readonly CancellationToken cancel;
            int nextId,http;
            string targetSession,retryAfter,networkRequest;
            bool document,loaded,post,loginRedirect;
            internal Conversation(Stream send,Stream input,JevSession session,CancellationToken cancel){this.send=send;this.input=new BufferedStream(input,8192);this.session=session;this.cancel=cancel;}
            int Send(string method,object parameters,string target=null){
                cancel.ThrowIfCancellationRequested();int id=++nextId;
                var data=new Dictionary<string,object>{{"id",id},{"method",method},{"params",parameters??new{}}};if(target!=null)data["sessionId"]=target;
                byte[] bytes=Encoding.UTF8.GetBytes(Json(data));
                try{send.Write(bytes,0,bytes.Length);send.WriteByte(0);send.Flush();}finally{Array.Clear(bytes,0,bytes.Length);}
                return id;
            }
            Dictionary<string,object> Frame(){
                using(var buffer=new MemoryStream()){
                    try{
                        int value;while((value=input.ReadByte())>=0){cancel.ThrowIfCancellationRequested();if(value==0)return QuotaData.Parse(Encoding.UTF8.GetString(buffer.GetBuffer(),0,(int)buffer.Length)) as Dictionary<string,object>;if(buffer.Length>=524288)throw new QuotaFailure("Quota unavailable",null,0,"Response too large");buffer.WriteByte((byte)value);}
                        throw new QuotaFailure("Quota unavailable",null,0,"Transport failure");
                    }finally{Array.Clear(buffer.GetBuffer(),0,(int)buffer.Length);}
                }
            }
            object Call(string method,object parameters=null,string target=null){
                int id=Send(method,parameters,target);
                for(int count=0;count<512;count++){
                    var frame=Frame();if(frame==null)throw new QuotaFailure("Quota unavailable");Handle(frame);
                    if(QuotaDecoder.Number(QuotaDecoder.Get(frame,"id"))!=id)continue;
                    if(QuotaDecoder.Get(frame,"error")!=null)throw new QuotaFailure("Quota unavailable",null,0,"Transport failure");
                    return QuotaDecoder.Get(frame,"result");
                }
                throw new QuotaFailure("Quota unavailable",null,0,"Response too large");
            }
            void Handle(Dictionary<string,object> frame){
                if(QuotaDecoder.Text(QuotaDecoder.Get(frame,"sessionId"))!=targetSession)return;
                string method=QuotaDecoder.Text(QuotaDecoder.Get(frame,"method"));var parameters=QuotaDecoder.Get(frame,"params");
                if(method=="Page.loadEventFired"&&document)loaded=true;
                if(method=="Network.requestWillBeSent"){
                    var outgoing=QuotaDecoder.Get(parameters,"request");
                    if(QuotaDecoder.Text(QuotaDecoder.Get(outgoing,"url"))==Endpoint&&QuotaDecoder.Text(QuotaDecoder.Get(outgoing,"method"))=="POST")networkRequest=QuotaDecoder.Text(QuotaDecoder.Get(parameters,"requestId"));
                }
                // Manual redirects are opaque to fetch and may omit responseReceived. The
                // correlated network extra-info event still carries the real HTTP status.
                if(method=="Network.responseReceivedExtraInfo"&&post&&networkRequest!=null&&QuotaDecoder.Text(QuotaDecoder.Get(parameters,"requestId"))==networkRequest){
                    http=(int)(QuotaDecoder.Number(QuotaDecoder.Get(parameters,"statusCode"))??0);
                    RememberHeaders(QuotaDecoder.Get(parameters,"headers") as Dictionary<string,object>);
                }
                if(method=="Network.responseReceived"&&post&&QuotaDecoder.Text(QuotaDecoder.Get(parameters,"type"))=="Fetch"){
                    var response=QuotaDecoder.Get(parameters,"response");if(QuotaDecoder.Text(QuotaDecoder.Get(response,"url"))!=Endpoint)return;
                    http=(int)(QuotaDecoder.Number(QuotaDecoder.Get(response,"status"))??0);
                    RememberHeaders(QuotaDecoder.Get(response,"headers") as Dictionary<string,object>);
                }
                if(method!="Fetch.requestPaused")return;
                string requestId=QuotaDecoder.Text(QuotaDecoder.Get(parameters,"requestId"));var request=QuotaDecoder.Get(parameters,"request");
                bool fixedUrl=QuotaDecoder.Text(QuotaDecoder.Get(request,"url"))==Endpoint;
                if(!document&&fixedUrl&&QuotaDecoder.Text(QuotaDecoder.Get(parameters,"resourceType"))=="Document"){
                    document=true;
                    string html=Convert.ToBase64String(Encoding.UTF8.GetBytes("<!doctype html><meta charset=utf-8><title>Pulse Read-Only Balance</title>"));
                    Send("Fetch.fulfillRequest",new{requestId=requestId,responseCode=200,responseHeaders=new[]{new{name="Content-Type",value="text/html; charset=utf-8"},new{name="Content-Security-Policy",value="default-src 'none'; connect-src https://console.typesafe.ai"}},body=html},targetSession);
                }else if(!post&&loaded&&fixedUrl&&QuotaDecoder.Text(QuotaDecoder.Get(request,"method"))=="POST"){
                    post=true;var headers=new List<object>();var existing=QuotaDecoder.Get(request,"headers") as Dictionary<string,object>;
                    if(existing!=null)foreach(var header in existing)if(!header.Key.Equals("cookie",StringComparison.OrdinalIgnoreCase))headers.Add(new{name=header.Key,value=QuotaDecoder.Text(header.Value)});
                    headers.Add(new{name="Cookie",value=session.Cookie});
                    Send("Fetch.continueRequest",new{requestId=requestId,headers=headers},targetSession);
                }else Send("Fetch.failRequest",new{requestId=requestId,errorReason="BlockedByClient"},targetSession);
            }
            void RememberHeaders(Dictionary<string,object> headers){
                if(headers==null)return;
                foreach(var header in headers){
                    if(header.Key.Equals("retry-after",StringComparison.OrdinalIgnoreCase))retryAfter=header.Value as string;
                    if(header.Key.Equals("location",StringComparison.OrdinalIgnoreCase)){
                        Uri location;if(Uri.TryCreate(new Uri(Endpoint),header.Value as string,out location))loginRedirect=location.GetLeftPart(UriPartial.Authority)==Origin&&(location.AbsolutePath=="/login"||location.AbsolutePath=="/sign-in"||location.AbsolutePath=="/signin");
                    }
                }
            }
            internal object ReadBalance(){
                string context=QuotaDecoder.Text(QuotaDecoder.Get(Call("Target.createBrowserContext",new{disposeOnDetach=true}),"browserContextId"));
                string target=QuotaDecoder.Text(QuotaDecoder.Get(Call("Target.createTarget",new{url="about:blank",browserContextId=context}),"targetId"));
                targetSession=QuotaDecoder.Text(QuotaDecoder.Get(Call("Target.attachToTarget",new{targetId=target,flatten=true}),"sessionId"));
                if(string.IsNullOrEmpty(context)||string.IsNullOrEmpty(targetSession))throw new QuotaFailure("Quota unavailable");
                Call("Page.enable",null,targetSession);Call("Network.enable",null,targetSession);
                Call("Network.setCacheDisabled",new{cacheDisabled=true},targetSession);
                Call("Network.setUserAgentOverride",new{userAgent=session.UserAgent},targetSession);
                Call("Fetch.enable",new{patterns=new[]{new{urlPattern="*",requestStage="Request"}}},targetSession);
                Call("Page.navigate",new{url=Endpoint},targetSession);
                for(int count=0;!loaded&&count<256;count++){var frame=Frame();if(frame!=null)Handle(frame);}
                var origin=Call("Runtime.evaluate",new{expression="location.origin",returnByValue=true},targetSession);
                if(!loaded||QuotaDecoder.Text(QuotaDecoder.Get(QuotaDecoder.Get(origin,"result"),"value"))!=Origin)throw new QuotaFailure("Quota unavailable");
                string expression=@"(async()=>{const r=await fetch('/usage',{method:'POST',credentials:'include',redirect:'manual',headers:{'Next-Action':'"+ActionId+@"','Accept':'text/x-component','Content-Type':'text/plain;charset=UTF-8'},body:'[]',signal:AbortSignal.timeout(10000)});const type=r.headers.get('content-type')||'';const result={http:r.status,contentType:type,retryAfter:r.headers.get('retry-after')||'',actionNotFound:r.headers.get('x-nextjs-action-not-found')==='1'};if(!r.body)return result;const reader=r.body.getReader(),decoder=new TextDecoder();let bytes=0,text='';for(;;){const chunk=await reader.read();if(chunk.done)break;bytes+=chunk.value.length;if(bytes>65536){await reader.cancel();return {http:r.status,error:'Response too large'};}text+=decoder.decode(chunk.value,{stream:true});}text+=decoder.decode();result.challenge=r.headers.get('cf-mitigated')==='challenge'||(type.startsWith('text/html')&&/Attention Required!|Sorry, you have been blocked|Just a moment/.test(text));result.text=r.ok&&type.startsWith('text/x-component')?text:'';return result;})()";
                var evaluated=Call("Runtime.evaluate",new{expression=expression,awaitPromise=true,returnByValue=true},targetSession);
                var wire=QuotaDecoder.Get(QuotaDecoder.Get(evaluated,"result"),"value") as Dictionary<string,object>;
                if(wire==null)throw new QuotaFailure("Quota unavailable",null,http,"Transport failure");
                if((QuotaDecoder.Number(QuotaDecoder.Get(wire,"http"))??0)==0&&http!=0){wire["http"]=http;wire["loginRedirect"]=loginRedirect;}
                if(string.IsNullOrEmpty(QuotaDecoder.Text(QuotaDecoder.Get(wire,"retryAfter"))))wire["retryAfter"]=retryAfter;
                return Decode(wire,DateTimeOffset.UtcNow);
            }
            internal void CloseBrowser(){if(!cancel.IsCancellationRequested)try{Send("Browser.close",null);}catch{}}
            public void Dispose(){input.Dispose();}
        }
    }
}
