using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HardwarePulse;

static class WindowsQuotaTests {
    sealed class Credentials: IWindowsCredentialReader {
        public readonly List<string> Targets=[];
        public readonly Dictionary<string,byte[]> Values=[];
        public byte[]? Last;
        public byte[]? Read(string target,CancellationToken cancel){cancel.ThrowIfCancellationRequested();Targets.Add(target);return Values.TryGetValue(target,out var value)?Last=(byte[])value.Clone():null;}
    }
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static void Run() {
        if(OperatingSystem.IsWindows()) {
            var ownership=typeof(QuotaProviders).Assembly.GetType("HardwarePulse.AntigravityQuota")!.GetMethod("IsOwnedProcess",BindingFlags.NonPublic|BindingFlags.Static)!;
            using var identity=System.Security.Principal.WindowsIdentity.GetCurrent();
            uint pid=(uint)Environment.ProcessId;
            Check((bool)ownership.Invoke(null,[pid,identity.User!.Value])!,"Modern WMI ownership accepts current user");
            Check(!(bool)ownership.Invoke(null,[pid,"S-1-0-0"])!,"Modern WMI ownership rejects another user");
        }
        var credentials=new Credentials();
        credentials.Values["Claude Code-credentials:test-user"]=Encoding.Unicode.GetBytes("{\"claudeAiOauth\":{\"accessToken\":\"synthetic-wincred\"}}");
        var login=new WindowsClaudeLogin(_=>throw new QuotaFailure("Login required"),credentials,"test-user");
        Check(login.Read(CancellationToken.None)=="synthetic-wincred","Windows Credential Manager credential shape");
        Check(credentials.Targets.SequenceEqual(new[]{"Claude Code-credentials","Claude Code-credentials:test-user"}),"Windows Credential Manager target order");
        Check(credentials.Last!.All(value=>value==0),"Windows credential copy cleared after parse");
        credentials.Targets.Clear();
        Check(new WindowsClaudeLogin(_=>"synthetic-file",credentials,"test-user").Read(CancellationToken.None)=="synthetic-file"&&credentials.Targets.Count==0,"Claude file credential remains first");
        // Exercise the modern bridge with synthetic wire data; never read login stores.
        var parse=typeof(QuotaProviders).Assembly.GetType("HardwarePulse.QuotaData")!.GetMethod("Parse",BindingFlags.Static|BindingFlags.NonPublic)!;
        object Read(string json)=>parse.Invoke(null,[json])!;
        var result=QuotaDecoder.Decode("Antigravity",Read("{\"groups\":[{\"displayName\":\"Gemini\",\"buckets\":[{\"window\":\"weekly\",\"remainingFraction\":0.5},{\"window\":\"session\",\"remainingFraction\":0}]}]}"),DateTimeOffset.UtcNow);
        Check(result.Provider=="Antigravity"&&result.Status=="Live"&&result.Windows.Count==2&&result.Windows[0].Remaining==0&&result.Windows[1].Remaining==50,"Modern bridge preserves real zero and fraction quota");
        foreach(string malformed in new[]{"[]",new string(' ',1048577),"{bad","{\"nested\":"+new string('[',40)+"0"+new string(']',40)+"}"}) {
            try {Read(malformed);throw new Exception("Unbounded/malformed graph accepted");}
            catch(TargetInvocationException){ }
        }
        using var canceled=new CancellationTokenSource();canceled.Cancel();
        try{QuotaProviders.Read("Antigravity",canceled.Token);throw new Exception("Cancellation was swallowed");}
        catch(OperationCanceledException){ }
        Console.WriteLine("PASS modern Windows quota JSON bounds and cancellation; synthetic source only");
    }
}
