using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse;
using HardwarePulse.Desktop;

static class DesktopUpdateTests {
    sealed class Client : IUpdateClient {
        public Task<string> Metadata=Task.FromResult(Release());
        public TaskCompletionSource<string>? DownloadPending;
        public int Checks,Downloads,Installs,Cancels;
        public bool Ready {get;set;}
        public bool Installing {get;set;}
        public int Progress=>50;
        public bool FailInstall;
        public Task<string> CheckAsync(){Checks++;return Metadata;}
        public async Task<string> DownloadAsync(string url,string tag,string digest,long size){Downloads++;if(DownloadPending!=null)await DownloadPending.Task;Ready=true;return "verified";}
        public void Install(){Installs++;if(FailInstall)throw new InvalidOperationException("canceled");Installing=true;}
        public void CancelDownload(){Cancels++;}
    }
    static object Asset(string name="HardwarePulse-Setup.exe",string digest="",long size=100)=>new{name,browser_download_url="https://github.com/medking82/hardware-pulse/releases/download/v0.7.1/"+name,digest=digest.Length==0?"sha256:"+new string('a',64):digest,size};
    static string Release(bool preview=false,bool duplicate=false)=>JsonSerializer.Serialize(new{tag_name="v0.7.1",draft=false,prerelease=preview,assets=duplicate?new[]{Asset(),Asset()}:new[]{Asset("Pulse-linux-x64.tar.gz"),Asset(),Asset("Pulse-macos-arm64.tar.gz")}});
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
    static void Wait(Task task) {
        var until=DateTime.UtcNow.AddSeconds(5);
        while(!task.IsCompleted&&DateTime.UtcNow<until){using var slice=new CancellationTokenSource(20);Dispatcher.UIThread.MainLoop(slice.Token);}
        Check(task.IsCompleted,"Update operation timed out");task.GetAwaiter().GetResult();
    }
    public static void Run() {
        var language=new UiLanguage("en");var settings=new PreviewSettings();var client=new Client();int saved=0;
        using var panel=new DesktopUpdatePanel(language,settings,()=>saved++,client:client,version:new Version(0,7,0));
        var window=new Window{Width=360,Height=520,Content=panel};window.Show();Dispatcher.UIThread.RunJobs();
        try {
            panel.Poll(DateTime.UtcNow);Check(client.Checks==0,"Disabled automatic updates queried network");
            panel.Install();Check(client.Installs==0,"Unverified installer was launched");
            Wait(panel.CheckAsync());Check(panel.StatusKey=="Update available","Modern metadata selection did not find stable Windows installer");
            var pending=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);client.DownloadPending=pending;
            var downloading=panel.DownloadAsync();Wait(panel.DownloadAsync());Check(client.Downloads==1,"Duplicate download admitted");
            Check(panel.GetVisualDescendants().OfType<ProgressBar>().Single().IsVisible,"No download progress UI");
            pending.SetResult("verified");Wait(downloading);Check(panel.StatusKey=="Update ready to install"&&client.Installs==0,"Download installed automatically");
            client.FailInstall=true;panel.Install();Check(panel.StatusKey=="Installation canceled or failed; try again","Install cancellation was not recoverable");
            client.FailInstall=false;panel.Install();Check(client.Installs==2&&client.Installing&&panel.StatusKey=="Installing update…","Explicit install failed");client.Installing=false;
            foreach(string locale in new[]{"en","zh-CN","zh-TW"}){language.Select(locale);Dispatcher.UIThread.RunJobs();Check(panel.GetVisualDescendants().OfType<Button>().All(x=>x.Bounds.Width<=window.ClientSize.Width),"Update buttons overflow narrow UI");}
            var auto=panel.GetVisualDescendants().OfType<CheckBox>().Single(x=>x.Name=="AutoDownload");auto.IsChecked=true;
            Check(settings.AutoDownload&&settings.AutoUpdates&&saved==1,"Auto-download did not opt into checks/persistence");
        }finally{window.Close();}
        foreach(string json in new[]{Release(preview:true),Release(duplicate:true),"{}","{\"draft\":\"false\"}",JsonSerializer.Serialize(new{tag_name="v0.7.1",assets=new[]{Asset(digest:"sha256:bad")}}),JsonSerializer.Serialize(new{tag_name="v0.7.1",assets=new[]{Asset(size:101*1024*1024)}})}) {
            var invalid=new Client{Metadata=Task.FromResult(json)};
            using var c=new UpdateCoordinator(invalid,new Version(0,7,0));Wait(c.CheckAsync(DateTime.UtcNow,true));
            Check(!c.CanDownload&&invalid.Downloads==0&&c.StatusKey=="Update check failed; try again","Invalid/prerelease metadata admitted by modern parser");
        }
        var late=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lateClient=new Client{Metadata=late.Task};var closed=new DesktopUpdatePanel(language,new PreviewSettings{AutoDownload=true},()=>{},client:lateClient,version:new Version(0,7,0));
        var operation=closed.CheckAsync();closed.Dispose();late.SetResult(Release());Wait(operation);Check(lateClient.Downloads==0&&lateClient.Cancels==1,"Closed panel admitted late download");
        var automaticClient=new Client();using(var automaticPanel=new DesktopUpdatePanel(language,new PreviewSettings{AutoUpdates=true,AutoDownload=true},()=>{},client:automaticClient,version:new Version(0,7,0))) {
            var now=DateTime.UtcNow;automaticPanel.Poll(now);Wait(automaticPanel.Operation);automaticPanel.Poll(now.AddMinutes(1));
            Check(automaticClient.Checks==1&&automaticClient.Downloads==1&&automaticClient.Installs==0,"Automatic check/download cadence or install boundary changed");
        }
        using(var real=new DesktopUpdatePanel(language,new PreviewSettings{AutoUpdates=true,AutoDownload=true},()=>{})){Wait(real.CheckAsync());real.Poll(DateTime.UtcNow);Check(real.StatusKey=="Updates require the installed stable Windows version.","Preview/development build admitted update transport");}
        var verifier=new UpdateCheck();bool refused=false;try{verifier.Install();}catch(InvalidOperationException){refused=true;}Check(refused,"Modern installer launch accepted an unverified file");
        using var bytes=new MemoryStream(System.Text.Encoding.UTF8.GetBytes("abc"));Check(UpdateCheck.HashMatches(bytes,"ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"),"Modern SHA256 verification failed");
        string directory=Directory.CreateTempSubdirectory("pulse-update-settings-").FullName;
        try {
            string path=Path.Combine(directory,"settings.json");var store=new PreviewSettingsStore(path);
            Check(store.Save(new PreviewSettings{AutoUpdates=true,AutoDownload=true}),"Could not save update preferences");
            var restored=new PreviewSettingsStore(path).Load();Check(restored.AutoUpdates&&restored.AutoDownload,"Update preferences did not persist");
        }finally{Directory.Delete(directory,true);}
        Console.WriteLine("PASS shared updates: stable unique asset, digest/size, duplicate actions, explicit install, cancellation, automatic cadence, late disposal and preview gate");
    }
    public static void LiveMetadata() {
        var client=new UpdateCheck();client.Start();string json=client.Pending.GetAwaiter().GetResult();
        using var document=JsonDocument.Parse(json);Console.WriteLine("LIVE_UPDATE_METADATA "+document.RootElement.GetProperty("tag_name").GetString());
    }
}

