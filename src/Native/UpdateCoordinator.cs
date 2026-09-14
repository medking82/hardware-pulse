using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HardwarePulse {
    public interface IUpdateClient {
        Task<string> CheckAsync();
        Task<string> DownloadAsync(string url,string tag,string digest,long size);
        bool Ready { get; }
        bool Installing { get; }
        int Progress { get; }
        void Install();
        void CancelDownload();
    }

    // The adapter delegates verification and process launch to the existing owner.
    public sealed class UpdateClient : IUpdateClient {
        readonly UpdateCheck client=new UpdateCheck();
        public Task<string> CheckAsync(){client.Start();return client.Pending;}
        public Task<string> DownloadAsync(string url,string tag,string digest,long size){client.Download(url,tag,digest,size);return client.DownloadPending;}
        public bool Ready { get {return client.Ready;} }
        public bool Installing { get {return client.Installing;} }
        public int Progress { get {return client.Progress;} }
        public void Install(){client.Install();}
        public void CancelDownload(){client.CancelDownload();}
    }

    // Called serially by the UI context. Owns no WPF controls, timers or settings.
    public sealed class UpdateCoordinator : IDisposable {
        sealed class ReleaseAsset {public string name,browser_download_url,digest;public long size;}
        sealed class ReleaseInfo {public bool draft,prerelease;public string tag_name;public ReleaseAsset[] assets;}
        readonly IUpdateClient client;
        readonly Version installed;
        ReleaseAsset asset;string tag;bool disposed;
        public bool Checking {get;private set;}
        public bool Downloading {get;private set;}
        public string StatusKey {get;private set;}
        public string VersionText {get;private set;}
        public DateTime NextCheck {get;private set;}
        public bool Ready {get{return !disposed&&client.Ready;}}
        public bool Installing {get{return !disposed&&client.Installing;}}
        public bool Busy {get{return Checking||Downloading||Installing;}}
        public int Progress {get{return client.Progress;}}
        public bool CanDownload {get{return !disposed&&!Busy&&!Ready&&asset!=null;}}

        public UpdateCoordinator(IUpdateClient client,Version installed){this.client=client;this.installed=installed;}
        public bool ShouldCheck(DateTime now){return !disposed&&!Busy&&!Ready&&now>=NextCheck;}

        public async Task CheckAsync(DateTime now,bool autoDownload){
            if(disposed||Busy)return;
            if(Ready){StatusKey="Update ready to install";return;}
            Checking=true;NextCheck=now.AddHours(6);StatusKey="Checking for updates…";VersionText=null;asset=null;
            try {
                string json=await client.CheckAsync();if(disposed)return;
                var release=Json.Serializer().Deserialize<ReleaseInfo>(json);Version remote;
                if(release==null||release.draft||release.prerelease||!Version.TryParse((release.tag_name??"").TrimStart('v'),out remote))throw new InvalidDataException("Not a stable release");
                if(remote>installed){
                    var assets=(release.assets??new ReleaseAsset[0]).Where(a=>a!=null&&a.name=="HardwarePulse-Setup.exe").ToArray();
                    if(assets.Length!=1||!UpdateCheck.ValidAsset(assets[0].browser_download_url,release.tag_name,assets[0].digest,assets[0].size))throw new InvalidDataException("Invalid installer metadata");
                    asset=assets[0];tag=release.tag_name;StatusKey="Update available";VersionText=remote.ToString();
                }else StatusKey="You are up to date";
            }catch{if(!disposed)StatusKey="Update check failed; try again";}
            finally{Checking=false;}
            if(!disposed&&autoDownload&&asset!=null)await DownloadAsync();
        }

        public async Task DownloadAsync(){
            if(!CanDownload)return;
            Downloading=true;StatusKey="Downloading update";VersionText=null;
            try {
                await client.DownloadAsync(asset.browser_download_url,tag,asset.digest,asset.size);
                if(!disposed){if(!client.Ready)throw new InvalidDataException("Download not verified");StatusKey="Update ready to install";}
            }catch{if(!disposed)StatusKey="Update download failed; try again";}
            finally{Downloading=false;}
        }

        public void Install(){
            if(disposed||Busy||!Ready)return;
            try{client.Install();}
            catch{StatusKey="Installation canceled or failed; try again";}
        }
        public void Dispose(){if(disposed)return;disposed=true;client.CancelDownload();}
    }
}
