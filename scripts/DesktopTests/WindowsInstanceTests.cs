using System.Diagnostics;
using Avalonia.Threading;
using HardwarePulse;
using HardwarePulse.Desktop;

static class WindowsInstanceTests {
    sealed class Source : IMonitorSource {
        public bool IsDemo=>true;public int Calls;
        public string[] Interfaces()=>[];
        public MonitorSnapshot Poll(string? name){Interlocked.Increment(ref Calls);return new("20%","4 GiB","—","—",true,true);}
    }
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
    static void Until(Func<bool> done) {
        var deadline=DateTime.UtcNow.AddSeconds(5);
        while(!done()&&DateTime.UtcNow<deadline){using var slice=new CancellationTokenSource(TimeSpan.FromMilliseconds(20));Dispatcher.UIThread.MainLoop(slice.Token);}
        Check(done(),"Instance activation timed out");
    }
    static Process Child(string command,string id) {
        var info=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        if(string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath),"dotnet",StringComparison.OrdinalIgnoreCase))info.ArgumentList.Add(typeof(WindowsInstanceTests).Assembly.Location);
        info.ArgumentList.Add(command);info.ArgumentList.Add(id);
        return Process.Start(info)!;
    }
    static void Notify(string id) {
        using var child=Child("--instance-secondary",id);
        try{Check(child.WaitForExit(5000),"Secondary instance did not exit");Check(child.ExitCode==0,"Secondary instance acquired ownership or failed: "+child.StandardError.ReadToEnd());}
        finally{if(!child.HasExited){child.Kill();child.WaitForExit();}}
    }
    public static int Secondary(string id) {using var instance=new WindowsInstanceSession(id);if(instance.IsPrimary)return 2;instance.Notify();return 0;}
    public static int Hold(string id) {using var instance=new WindowsInstanceSession(id);if(!instance.IsPrimary)return 2;Console.WriteLine("READY");Console.Out.Flush();Thread.Sleep(30000);return 0;}
    public static void Native() {
        if(!OperatingSystem.IsWindows())return;
        string id="Tests."+Guid.NewGuid().ToString("N");
        var source=new Source();var window=new MonitorWindow(source);window.Show();Until(()=>source.Calls>0);var sampler=window.Sampling;
        using(var first=new WindowsInstanceSession(id)) {
            Check(first.IsPrimary,"First instance did not acquire ownership");
            window.Hide();Notify(id);int restores=0;
            first.Listen(action=>Dispatcher.UIThread.Post(action),()=>{restores++;window.RestoreMain();});
            Until(()=>restores==1&&window.IsVisible);
            Check(ReferenceEquals(sampler,window.Sampling),"Early activation created another sampler");
            window.Hide();Notify(id);Until(()=>restores==2&&window.IsVisible);
            Check(ReferenceEquals(sampler,window.Sampling),"Repeated launch replaced sampling worker");
            using(var separate=new WindowsInstanceSession(id+".Other"))Check(separate.IsPrimary,"Unrelated profile identity blocked");
            window.Close();Until(()=>sampler.IsCompleted);Notify(id);Until(()=>restores==3);Check(!window.IsVisible,"Late activation resurrected closed window");
        }
        using(var again=new WindowsInstanceSession(id))Check(again.IsPrimary,"Graceful shutdown did not release ownership");
        string abandoned=id+".Abandoned";
        using(var child=Child("--instance-owner",abandoned)) {
            try {
                var ready=child.StandardOutput.ReadLineAsync();Check(ready.Wait(5000)&&ready.Result=="READY","Crash fixture did not acquire ownership");
                using var observer=new WindowsInstanceSession(abandoned);Check(!observer.IsPrimary,"Live owner lost mutex");
                child.Kill();child.WaitForExit();
                using var recovered=new WindowsInstanceSession(abandoned);Check(recovered.IsPrimary,"Abandoned ownership not recoverable");
            }finally{if(!child.HasExited){child.Kill();child.WaitForExit();}}
        }
        string pending=id+".Pending";Action? queued=null;
        using(var first=new WindowsInstanceSession(pending)) {
            first.Listen(action=>Volatile.Write(ref queued,action),()=>throw new Exception("Disposed activation callback ran"));Notify(pending);Until(()=>Volatile.Read(ref queued)!=null);
        }
        queued!();
        Console.WriteLine("PASS Windows instance: second-process exclusion, early/repeated activation, same sampler, identity isolation, graceful/crash recovery and late callback disposal");
    }
}
