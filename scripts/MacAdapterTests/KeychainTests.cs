using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using HardwarePulse;

static class KeychainTests {
    sealed class Api:IMacClaudeKeychainApi {
        public byte Allowed=1;
        public int Result,Calls,Freed,Changes;
        public bool Throw,FailRestore;
        public uint Length=3;
        public byte[] Payload=new byte[]{1,2,3};
        public CancellationTokenSource Cancel;
        public int GetInteraction(out byte allowed){allowed=Allowed;return 0;}
        public int SetInteraction(byte allowed){Changes++;if(FailRestore&&Changes==2)return -1;Allowed=allowed;return 0;}
        public int Find(byte[] service,byte[] account,out uint length,out IntPtr data) {
            Calls++;Check(Allowed==0,"Interactive UI disabled before lookup");
            Check(Encoding.UTF8.GetString(service)=="Claude Code-credentials"&&Encoding.UTF8.GetString(account)=="synthetic-user","Exact service and account match");
            length=Length;data=Marshal.AllocHGlobal(Payload.Length);Marshal.Copy(Payload,0,data,Payload.Length);
            Cancel?.Cancel();if(Throw)throw new InvalidOperationException("Synthetic native failure");return Result;
        }
        public void Free(IntPtr data){Freed++;Marshal.FreeHGlobal(data);}
    }
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
    public static void Run() {
        foreach(byte original in new byte[]{0,1}) {
            var api=new Api{Allowed=original};var reader=new MacClaudeKeychain(api);
            var data=reader.Read("Claude Code-credentials","synthetic-user",CancellationToken.None);
            Check(data.Length==3&&data[2]==3&&api.Freed==1&&api.Allowed==original&&api.Changes==2,"Bounded copy and interaction state restored");
        }
        foreach(int status in new[]{-25300,-25308,-25293}) {
            var api=new Api{Result=status};var reader=new MacClaudeKeychain(api);
            try {Check(reader.Read("Claude Code-credentials","synthetic-user",CancellationToken.None)==null&&status==-25300,"Only missing items permit fallback");}
            catch(QuotaFailure e){Check(status!=-25300&&e.Status=="Login unavailable","Denied/locked item does not masquerade as absent");}
            Check(api.Allowed==1&&api.Freed==1,"Error cleanup restores process state");
        }
        foreach(uint size in new uint[]{0,1048577}) {
            var api=new Api{Length=size};
            try{new MacClaudeKeychain(api).Read("Claude Code-credentials","synthetic-user",CancellationToken.None);throw new Exception("Expected size rejection");}catch(QuotaFailure){}
            Check(api.Allowed==1&&api.Freed==1,"Oversize/empty native buffer freed");
        }
        var throwing=new Api{Throw=true};
        try{new MacClaudeKeychain(throwing).Read("Claude Code-credentials","synthetic-user",CancellationToken.None);throw new Exception("Expected native failure");}catch(InvalidOperationException){}
        Check(throwing.Allowed==1&&throwing.Freed==1,"Native exception restores guard");
        using(var stop=new CancellationTokenSource()) {
            var api=new Api{Cancel=stop};
            try{new MacClaudeKeychain(api).Read("Claude Code-credentials","synthetic-user",stop.Token);throw new Exception("Expected cancellation");}catch(OperationCanceledException){}
            Check(api.Allowed==1&&api.Freed==1,"In-call cancellation restores guard and frees result");
        }
        var invalid=new Api();
        using(var stop=new CancellationTokenSource()) {
            var api=new Api{Cancel=stop,FailRestore=true};
            try{new MacClaudeKeychain(api).Read("Claude Code-credentials","synthetic-user",stop.Token);throw new Exception("Expected cancellation despite restore failure");}catch(OperationCanceledException){}
            Check(api.Freed==1&&api.Changes==2,"Restore failure must not mask cancellation or skip native cleanup");
        }
        var failedRestore=new Api{FailRestore=true};
        try{new MacClaudeKeychain(failedRestore).Read("Claude Code-credentials","synthetic-user",CancellationToken.None);throw new Exception("Expected restore failure");}catch(QuotaFailure){}
        Check(failedRestore.Freed==1,"Restore failure never returns credentials and frees native result");
        try{new MacClaudeKeychain(invalid).Read("Other-service","synthetic-user",CancellationToken.None);throw new Exception("Expected service rejection");}catch(ArgumentException){}
        Check(invalid.Calls==0&&invalid.Changes==0,"Unrelated Keychain entries never queried");
        Check(MacClaudeLogin.ServiceName(null,"/Users/test")=="Claude Code-credentials","Default service identity");
        Check(MacClaudeLogin.ServiceName("~/profile","/Users/test")==MacClaudeLogin.ServiceName("/Users/test/profile","/Users/test"),"Home expansion retains profile identity");
        Check(MacClaudeLogin.ServiceName("/profile-a","/Users/test")!=MacClaudeLogin.ServiceName("/profile-b","/Users/test"),"Distinct profiles never fall through to default service");
        Check(MacClaudeLogin.ServiceName("/caf\u00e9","/Users/test")==MacClaudeLogin.ServiceName("/cafe\u0301","/Users/test"),"Canonical Unicode service hash");
        var json=Encoding.UTF8.GetBytes("{\"claudeAiOauth\":{\"accessToken\":\"synthetic-keychain\"}}");
        var source=new Api{Payload=json,Length=(uint)json.Length};int fileReads=0;string supplied="synthetic-env";
        var login=new MacClaudeLogin(new MacClaudeKeychain(source),()=>supplied,_=>{fileReads++;return "synthetic-file";},null,"/Users/test","synthetic-user");
        Check(login.Read(CancellationToken.None)==supplied&&source.Calls==0&&fileReads==0,"Explicit environment token avoids Keychain and file");
        supplied=null;Check(login.Read(CancellationToken.None)=="synthetic-keychain"&&fileReads==0,"Existing Keychain takes precedence over fallback file");
        source.Result=-25300;Check(login.Read(CancellationToken.None)=="synthetic-file"&&fileReads==1,"Missing Keychain uses owning profile file");
        source.Result=-25308;
        try{login.Read(CancellationToken.None);throw new Exception("Expected interaction denial");}catch(QuotaFailure){}
        Check(fileReads==1,"Denied Keychain does not silently switch account source");
        Console.WriteLine("PASS Claude Keychain boundary: exact query, noninteractive guard, copy bounds, cancellation and cleanup; synthetic only");
    }
}
