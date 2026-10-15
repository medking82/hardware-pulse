using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HardwarePulse;

public static class QuotaSecurityTests {
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static int expiryCalls;
    static object Proceed(string token,CancellationToken ct){expiryCalls++;return token;}
    public static void Run(){
        var request=typeof(QuotaProviders).GetMethod("Request",BindingFlags.Static|BindingFlags.NonPublic);
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
        try{
            int port=((IPEndPoint)listener.LocalEndpoint).Port;
            var server=Task.Run(()=>{using(var client=listener.AcceptTcpClient())using(var stream=client.GetStream()){
                client.ReceiveTimeout=3000;var reader=new System.IO.StreamReader(stream);while(!string.IsNullOrEmpty(reader.ReadLine())){}
                var bytes=Encoding.ASCII.GetBytes("HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");stream.Write(bytes,0,bytes.Length);
            }});
            try{request.Invoke(null,new object[]{"http://127.0.0.1:"+port,new Dictionary<string,string>(),null,CancellationToken.None,true});throw new Exception("Forbidden admitted");}
            catch(TargetInvocationException e){Check(((QuotaFailure)e.InnerException).Status=="Quota access denied","403 access denial must not falsely demand another login");}
            Check(server.Wait(4000),"fixture stalled");
        }finally{listener.Stop();}
        Console.WriteLine("PASS quota HTTP forbidden is distinct from missing/expired authentication");
        string root=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"pulse-quota-test-"+Guid.NewGuid().ToString("N"));
        try{
            var store=new ProtectedQuotaCredentials(root);Check(store.Read("DeepSeek")=="","unset credential");
            const string synthetic="fixture-key-not-a-real-key";store.Save("DeepSeek",synthetic);
            Check(store.Read("DeepSeek")==synthetic,"user-bound round trip");
            var bytes=System.IO.File.ReadAllBytes(System.IO.Path.Combine(root,"credentials","DeepSeek.dpapi"));
            Check(!Encoding.UTF8.GetString(bytes).Contains(synthetic),"plaintext key was persisted");
            try{store.Save("DeepSeek","bad\r\nheader");throw new Exception("invalid key accepted");}catch(ArgumentException){}
            Check(store.Read("DeepSeek")==synthetic,"invalid replacement changed prior key");
            store.Save("DeepSeek",synthetic+"-replaced");Check(store.Read("DeepSeek")==synthetic+"-replaced","atomic replacement");
            bytes[bytes.Length/2]^=1;System.IO.File.WriteAllBytes(System.IO.Path.Combine(root,"credentials","DeepSeek.dpapi"),bytes);
            try{store.Read("DeepSeek");throw new Exception("corrupt key accepted");}catch(QuotaFailure failure){Check(failure.Status=="Credential unavailable","corrupt failure leaks diagnostics");}
            store.Clear("DeepSeek");Check(!store.Exists("DeepSeek")&&store.Read("DeepSeek")=="","explicit removal");
        }finally{if(System.IO.Directory.Exists(root))System.IO.Directory.Delete(root,true);}
        int calls=0,reads=0;
        object rotated=ClaudeQuota.Read(()=>new Dictionary<string,object>{{"accessToken",++reads==1?"synthetic-old":"synthetic-new"}},()=>null,(token,ct)=>{calls++;if(token=="synthetic-old")throw new QuotaFailure("Login required");return token;},CancellationToken.None);
        Check(calls==2&&reads==2&&(string)rotated=="synthetic-new","rotation race must re-read once without refreshing login");
        calls=0;try{ClaudeQuota.Read(()=>new Dictionary<string,object>{{"accessToken","synthetic-expired"},{"expiresAt",1}},()=>null,(token,ct)=>{calls++;return null;},CancellationToken.None);throw new Exception("expired admitted");}catch(QuotaFailure failure){Check(failure.Status=="Login expired"&&calls==0,"expired login must have explicit state");}
        calls=0;try{ClaudeQuota.Read(()=>new Dictionary<string,object>{{"accessToken","synthetic-same"}},()=>null,(token,ct)=>{calls++;throw new QuotaFailure("Login required");},CancellationToken.None);throw new Exception("invalid admitted");}catch(QuotaFailure){Check(calls==1,"same rejected token must not be retried");}
        var map=typeof(QuotaProviders).GetMethod("MapStatus",BindingFlags.Static|BindingFlags.NonPublic);
        Check((string)map.Invoke(null,new object[]{"Codex","Quota access denied"})=="Login required","Codex 403 keeps login guidance");
        Check((string)map.Invoke(null,new object[]{"Antigravity","Quota access denied"})=="Login required","Antigravity 403 keeps login guidance");
        Check((string)map.Invoke(null,new object[]{"Claude","Quota access denied"})=="Quota access denied","Claude 403 is access denial");
        Check((string)map.Invoke(null,new object[]{"DeepSeek","Quota access denied"})=="Quota access denied","DeepSeek 403 is access denial");
        Check((string)map.Invoke(null,new object[]{"DeepSeek","Login required"})=="API key rejected","DeepSeek 401 is a rejected key");
        expiryCalls=0;
        foreach(object stamp in new object[]{4102444800000L,2000000000L,"2027-01-01T00:00:00Z"})Check((string)ClaudeQuota.Read(()=>new Dictionary<string,object>{{"accessToken","fixture-future"},{"expiresAt",stamp}},()=>null,Proceed,CancellationToken.None)=="fixture-future"&&expiryCalls>0,"future expiry stamp must proceed: "+stamp);
        int before=expiryCalls;foreach(object stamp in new object[]{1500000000000L,1500000000L,"2026-01-01T00:00:00Z"}){try{ClaudeQuota.Read(()=>new Dictionary<string,object>{{"accessToken","fixture-expired"},{"expiresAt",stamp}},()=>null,Proceed,CancellationToken.None);throw new Exception("expired admitted: "+stamp);}catch(QuotaFailure failure){Check(failure.Status=="Login expired"&&expiryCalls==before,"expired stamp must fail before any request: "+stamp);}}
        Console.WriteLine("PASS DPAPI encrypted save/replace/corruption/removal and Claude expiry/bounded rotation recovery; synthetic credentials only");
    }
}
