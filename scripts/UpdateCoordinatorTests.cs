using System;
using System.IO;
using System.Threading.Tasks;
using HardwarePulse;

static class UpdateCoordinatorTests {
    sealed class Client : IUpdateClient {
        public Task<string> CheckResult,DownloadResult;
        public int Checks,Downloads,Installs,Cancels;public bool FailInstall,ReadyValue;
        public string DownloadUrl,DownloadTag,DownloadDigest;public long DownloadSize;
        public bool Ready {get{return ReadyValue;}}
        public bool Installing {get;set;}
        public int Progress {get{return 50;}}
        public Task<string> CheckAsync(){Checks++;return CheckResult;}
        public Task<string> DownloadAsync(string url,string tag,string digest,long size){Downloads++;DownloadUrl=url;DownloadTag=tag;DownloadDigest=digest;DownloadSize=size;return DownloadResult;}
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
            // Mixed release assets must never route an installed WPF user to a shared host.
            var mixed=Json.Serializer().Deserialize<System.Collections.Generic.Dictionary<string,object>>(Release("v0.7.0"));
            var installer=new {name="HardwarePulse-Setup.exe",browser_download_url="https://github.com/medking82/hardware-pulse/releases/download/v0.7.0/HardwarePulse-Setup.exe",digest="sha256:"+new string('b',64),size=123};
            var archives=new System.Collections.Generic.List<object>();
            foreach(string rid in new[]{"win-x64","win-arm64","linux-x64","linux-arm64","osx-x64","osx-arm64"}){
                foreach(string suffix in new[]{".tar.gz",".tar.gz.sha256"})archives.Add(new {name="Pulse-"+rid+suffix,browser_download_url="https://example.invalid/never-download",digest="",size=1});
            }
            foreach(int position in new[]{0,6,12}){
                var assets=new System.Collections.Generic.List<object>(archives);assets.Insert(position,installer);mixed["assets"]=assets;
                var target=new Client {CheckResult=Task.FromResult(Json.Serializer().Serialize(mixed)),DownloadResult=Task.FromResult("verified")};
                using(var c=new UpdateCoordinator(target,version)){
                    c.CheckAsync(now,true).GetAwaiter().GetResult();
                    Check(target.Downloads==1&&target.DownloadUrl==installer.browser_download_url&&target.DownloadTag=="v0.7.0"&&target.DownloadDigest==installer.digest&&target.DownloadSize==123,"Mixed assets changed Windows installer selection");
                }
            }
            foreach(bool duplicate in new[]{false,true}){
                var assets=new System.Collections.Generic.List<object>(archives);
                if(duplicate){assets.Add(installer);assets.Add(installer);}mixed["assets"]=assets;
                var target=new Client {CheckResult=Task.FromResult(Json.Serializer().Serialize(mixed))};
                using(var c=new UpdateCoordinator(target,version)){
                    c.CheckAsync(now,true).GetAwaiter().GetResult();
                    Check(target.Downloads==0&&!c.CanDownload&&c.StatusKey=="Update check failed; try again","Missing/duplicate installer admitted among platform assets");
                }
            }
            Console.WriteLine("PASS mixed-platform release assets: exact Windows installer at any position; absent/duplicate rejected");
            var legacyInstaller=new {name="HardwarePulse-Win7-x64-Setup.exe",browser_download_url="https://github.com/medking82/hardware-pulse/releases/download/v0.7.0/HardwarePulse-Win7-x64-Setup.exe",digest="sha256:"+new string('c',64),size=456};
            foreach(bool legacy in new[]{false,true}){
                mixed["assets"]=new object[]{legacyInstaller,installer};
                var target=new Client {CheckResult=Task.FromResult(Json.Serializer().Serialize(mixed)),DownloadResult=Task.FromResult("verified")};
                using(var c=new UpdateCoordinator(target,version,legacy)){
                    c.CheckAsync(now,true).GetAwaiter().GetResult();
                    Check(target.Downloads==1&&target.DownloadUrl==(legacy?legacyInstaller.browser_download_url:installer.browser_download_url)&&target.DownloadSize==(legacy?456:123),"OS update channel crossed");
                }
            }
            foreach(int scenario in new[]{0,1,2,3}){
                mixed["assets"]=scenario==0?new object[]{installer}:scenario==1?new object[]{legacyInstaller,legacyInstaller}:scenario==2?new object[]{new {name=legacyInstaller.name,browser_download_url=installer.browser_download_url,digest=legacyInstaller.digest,size=456}}:new object[]{new {name=legacyInstaller.name,browser_download_url=legacyInstaller.browser_download_url,digest="",size=456}};
                var target=new Client {CheckResult=Task.FromResult(Json.Serializer().Serialize(mixed))};
                using(var c=new UpdateCoordinator(target,version,true)){
                    c.CheckAsync(now,true).GetAwaiter().GetResult();c.Install();
                    Check(target.Downloads==0&&target.Installs==0&&!c.CanDownload&&!c.Checking,"Invalid legacy update became actionable");
                    Check(c.StatusKey==(scenario==0?"No compatible Windows 7 update is available.":"Update check failed; try again"),"Legacy asset rejection explanation incorrect");
                }
            }
            Console.WriteLine("PASS OS update channels: exact selection, missing/duplicate/cross-channel URL and absent digest rejected");
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
