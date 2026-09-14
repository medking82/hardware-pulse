using System;
using System.IO;
using System.Threading.Tasks;
using HardwarePulse;

static class UpdateCoordinatorTests {
    sealed class Client : IUpdateClient {
        public Task<string> CheckResult,DownloadResult;
        public int Checks,Downloads,Installs,Cancels;public bool FailInstall,ReadyValue;
        public bool Ready {get{return ReadyValue;}}
        public bool Installing {get;set;}
        public int Progress {get{return 50;}}
        public Task<string> CheckAsync(){Checks++;return CheckResult;}
        public Task<string> DownloadAsync(string url,string tag,string digest,long size){Downloads++;return DownloadResult;}
        public void Install(){Installs++;if(FailInstall)throw new IOException("Canceled");Installing=true;}
        public void CancelDownload(){Cancels++;}
    }
    static void Check(bool pass,string reason){if(!pass)throw new Exception(reason);}
    static string Release(string tag="v0.5.3",bool prerelease=false,bool duplicate=false,bool badDigest=false){
        var asset=new {name="HardwarePulse-Setup.exe",browser_download_url="https://github.com/medking82/hardware-pulse/releases/download/"+tag+"/HardwarePulse-Setup.exe",digest=badDigest?"":"sha256:"+new string('a',64),size=100};
        return Json.Serializer().Serialize(new {tag_name=tag,draft=false,prerelease=prerelease,assets=duplicate?new[]{asset,asset}:new[]{asset}});
    }
    static int Main(){
        try{
            var now=new DateTime(2026,9,14,12,0,0);var version=new Version("0.5.2.0");
            var client=new Client();var pending=new TaskCompletionSource<string>();client.CheckResult=pending.Task;
            using(var coordinator=new UpdateCoordinator(client,version)){
                Check(coordinator.ShouldCheck(now),"Initial auto check");
                var check=coordinator.CheckAsync(now,false);coordinator.CheckAsync(now,false).GetAwaiter().GetResult();
                coordinator.DownloadAsync().GetAwaiter().GetResult();coordinator.Install();
                Check(client.Checks==1&&client.Downloads==0&&client.Installs==0&&coordinator.Checking,"Duplicate or premature actions");
                pending.SetResult(Release());check.GetAwaiter().GetResult();
                Check(coordinator.CanDownload&&coordinator.VersionText=="0.5.3"&&!coordinator.ShouldCheck(now.AddHours(5))&&coordinator.ShouldCheck(now.AddHours(6)),"Release availability and interval");
                var download=new TaskCompletionSource<string>();client.DownloadResult=download.Task;
                var operation=coordinator.DownloadAsync();coordinator.DownloadAsync().GetAwaiter().GetResult();
                Check(client.Downloads==1&&coordinator.Downloading,"Duplicate download");
                download.SetException(new IOException("Network failed"));operation.GetAwaiter().GetResult();
                Check(coordinator.CanDownload&&coordinator.StatusKey=="Update download failed; try again","Failed download retry");
                // Real client becomes Ready on completion; begin the retry before that transition.
                client.ReadyValue=false;download=new TaskCompletionSource<string>();client.DownloadResult=download.Task;
                operation=coordinator.DownloadAsync();client.ReadyValue=true;download.SetResult("verified");operation.GetAwaiter().GetResult();
                Check(coordinator.Ready&&!coordinator.CanDownload,"Verified download completion");
                coordinator.CheckAsync(now,false).GetAwaiter().GetResult();Check(client.Checks==1,"Ready cache triggered another check");
                client.FailInstall=true;coordinator.Install();Check(!coordinator.Installing&&coordinator.StatusKey=="Installation canceled or failed; try again","Install cancellation retry");
                client.FailInstall=false;coordinator.Install();coordinator.Install();Check(client.Installs==2&&coordinator.Installing,"Duplicate installer launch");
            }
            Check(client.Cancels==1,"Dispose must cancel once");
            foreach(string json in new[]{Release(prerelease:true),Release(duplicate:true),Release(badDigest:true),"{broken"}){
                client=new Client {CheckResult=Task.FromResult(json)};
                using(var c=new UpdateCoordinator(client,version)){c.CheckAsync(now,true).GetAwaiter().GetResult();Check(!c.CanDownload&&client.Downloads==0&&c.StatusKey=="Update check failed; try again","Invalid metadata admitted");}
            }
            client=new Client {CheckResult=Task.FromResult(Release("v0.5.2"))};
            using(var c=new UpdateCoordinator(client,version)){c.CheckAsync(now,true).GetAwaiter().GetResult();Check(c.StatusKey=="You are up to date"&&client.Downloads==0,"Current release");}
            client=new Client {CheckResult=Task.FromResult(Release())};var auto=new TaskCompletionSource<string>();client.DownloadResult=auto.Task;
            using(var c=new UpdateCoordinator(client,version)){var operation=c.CheckAsync(now,true);Check(c.Downloading&&client.Downloads==1,"Automatic download");client.ReadyValue=true;auto.SetResult("verified");operation.GetAwaiter().GetResult();Check(c.Ready,"Auto download ready");}
            client=new Client();pending=new TaskCompletionSource<string>();client.CheckResult=pending.Task;
            var closed=new UpdateCoordinator(client,version);var late=closed.CheckAsync(now,true);closed.Dispose();closed.Dispose();pending.SetResult(Release());late.GetAwaiter().GetResult();
            Check(client.Downloads==0&&client.Cancels==1&&!closed.Ready&&!closed.ShouldCheck(now.AddDays(1)),"Late check after disposal");
            client=new Client {CheckResult=Task.FromResult(Release())};var lateDownload=new TaskCompletionSource<string>();client.DownloadResult=lateDownload.Task;
            closed=new UpdateCoordinator(client,version);late=closed.CheckAsync(now,true);closed.Dispose();client.ReadyValue=true;lateDownload.SetResult("verified");late.GetAwaiter().GetResult();closed.Install();
            Check(!closed.Ready&&client.Installs==0&&client.Cancels==1&&closed.StatusKey!="Update ready to install","Late download after disposal");
            Console.WriteLine("PASS headless updater: stable metadata, duplicate actions, retries, scheduling, auto download, install cancellation and disposal");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
