using System.Reflection;
using HardwarePulse;

static class WindowsQuotaTests {
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static void Run() {
        if(OperatingSystem.IsWindows()) {
            var ownership=typeof(QuotaProviders).Assembly.GetType("HardwarePulse.AntigravityQuota")!.GetMethod("IsOwnedProcess",BindingFlags.NonPublic|BindingFlags.Static)!;
            using var identity=System.Security.Principal.WindowsIdentity.GetCurrent();
            uint pid=(uint)Environment.ProcessId;
            Check((bool)ownership.Invoke(null,[pid,identity.User!.Value])!,"Modern WMI ownership accepts current user");
            Check(!(bool)ownership.Invoke(null,[pid,"S-1-0-0"])!,"Modern WMI ownership rejects another user");
        }
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
