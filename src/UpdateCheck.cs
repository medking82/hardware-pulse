using System;
using System.Net;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
public sealed class UpdateCheck {
    readonly bool legacyWindows;
    public UpdateCheck(bool legacyWindows=false){this.legacyWindows=legacyWindows;}
    public static string AssetName(bool legacyWindows){return legacyWindows?"HardwarePulse-Win7-x64-Setup.exe":"HardwarePulse-Setup.exe";}
    public Task<string> Pending { get; private set; }
    public Task<string> DownloadPending { get; private set; }
    public int Progress { get { return progress; } }
    volatile int progress;
    volatile bool canceled;
    volatile HttpWebRequest activeDownload;
    string verifiedPath, expectedDigest;
    long expectedSize;
    Process installer;
    public bool Installing { get { return installer!=null && !installer.HasExited; } }
    public bool Ready { get { return verifiedPath!=null && DownloadPending!=null && DownloadPending.Status==TaskStatus.RanToCompletion && File.Exists(verifiedPath); } }
    public static bool ValidAsset(string url,string tag,string digest,long size,bool legacyWindows=false) {
        Uri uri;
        return Regex.IsMatch(tag??"",@"^v\d+\.\d+\.\d+$") &&
            Uri.TryCreate(url,UriKind.Absolute,out uri) && uri.Scheme=="https" && uri.Host=="github.com" && uri.IsDefaultPort && uri.UserInfo=="" && uri.Query=="" && uri.Fragment=="" &&
            uri.AbsolutePath=="/medking82/hardware-pulse/releases/download/"+tag+"/"+AssetName(legacyWindows) &&
            Regex.IsMatch(digest??"",@"^sha256:[a-fA-F0-9]{64}$") && size>0 && size<=100*1024*1024;
    }
    public void Download(string url,string tag,string digest,long size) {
        if(Installing)throw new InvalidOperationException("Installation in progress");
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)12288;
        if(!ValidAsset(url,tag,digest,size,legacyWindows))throw new InvalidDataException("Invalid release asset metadata");
        if(DownloadPending!=null && !DownloadPending.IsCompleted)return;
        if(Ready && expectedDigest==digest.Substring(7) && expectedSize==size)return;
        if(verifiedPath!=null && File.Exists(verifiedPath))File.Delete(verifiedPath);
        canceled=false;progress=0;verifiedPath=null;expectedDigest=digest.Substring(7);expectedSize=size;
        DownloadPending=Task.Run(()=> {
            string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"HardwarePulse","updates");
            Directory.CreateDirectory(folder);
            string path=Path.Combine(folder,Guid.NewGuid().ToString("N")+"-Setup.exe");
            bool complete=false;
            try {
                string next=url;
                for(int redirect=0;redirect<6;redirect++) {
                    var uri=new Uri(next);
                    if(uri.Scheme!="https" || !uri.IsDefaultPort || uri.UserInfo!="" || (uri.Host!="github.com" && uri.Host!="release-assets.githubusercontent.com" && uri.Host!="objects.githubusercontent.com"))throw new InvalidDataException("Unexpected download host");
                    var request=(HttpWebRequest)WebRequest.Create(uri);activeDownload=request;
                    request.AllowAutoRedirect=false;request.UserAgent="HardwarePulse-UpdateDownload";request.Timeout=15000;request.ReadWriteTimeout=15000;
                    if(canceled)throw new OperationCanceledException();
                    using(var response=(HttpWebResponse)request.GetResponse()) {
                        int status=(int)response.StatusCode;
                        if(status>=300 && status<400){next=new Uri(uri,response.Headers["Location"]).AbsoluteUri;continue;}
                        if(status!=200 || (response.ContentLength>=0 && response.ContentLength!=size))throw new InvalidDataException("Unexpected download response");
                        using(var input=response.GetResponseStream())using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None)) {
                            var buffer=new byte[65536];long total=0;int count;
                            while((count=input.Read(buffer,0,buffer.Length))>0) {
                                if(canceled)throw new OperationCanceledException();
                                total+=count;if(total>size)throw new InvalidDataException("Download exceeds expected size");
                                file.Write(buffer,0,count);progress=(int)(total*100/size);
                            }
                            file.Flush();file.Position=0;
                            if(total!=size || !HashMatches(file,expectedDigest))throw new InvalidDataException("Download checksum mismatch");
                        }
                        if(canceled)throw new OperationCanceledException();
                        verifiedPath=path;complete=true;return path;
                    }
                }
                throw new InvalidDataException("Too many download redirects");
            } finally {activeDownload=null;if(!complete && File.Exists(path))File.Delete(path);}
        });
    }
    public static bool HashMatches(Stream stream,string digest) {
        using(var sha=SHA256.Create())return String.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-",""),digest,StringComparison.OrdinalIgnoreCase);
    }
    public void Install() {
        if(Installing)return;
        if(verifiedPath==null || DownloadPending==null || DownloadPending.Status!=TaskStatus.RanToCompletion)throw new InvalidOperationException("No verified update");
        // Hold a read-only handle across launch so replacement/writes are refused during verification and UAC.
        using(var file=new FileStream(verifiedPath,FileMode.Open,FileAccess.Read,FileShare.Read)) {
            if(file.Length!=expectedSize || !HashMatches(file,expectedDigest)){verifiedPath=null;throw new InvalidDataException("Cached installer changed");}
            var info=new ProcessStartInfo(verifiedPath,"/SILENT /NORESTART /CLOSEAPPLICATIONS /PULSEUPDATE=1");
            // Let Inno's loader request elevation itself, retaining the original medium-integrity token.
            info.UseShellExecute=true;
            if(installer!=null)installer.Dispose();
            installer=Process.Start(info);if(installer==null)throw new InvalidOperationException("Installer did not start");
        }
    }
    public void CancelDownload() {canceled=true;var request=activeDownload;if(request!=null)request.Abort();}
    public void Start() {
        if(Pending!=null && !Pending.IsCompleted)return;
        Pending=Task.Run(()=> {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)12288;
            var request=(HttpWebRequest)WebRequest.Create("https://api.github.com/repos/medking82/hardware-pulse/releases/latest");
            request.UserAgent="HardwarePulse-UpdateCheck";request.Timeout=10000;request.ReadWriteTimeout=10000;
            request.AllowAutoRedirect=false;
            using(var response=(HttpWebResponse)request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream())) {
                if(response.StatusCode!=HttpStatusCode.OK)throw new InvalidDataException("Unexpected release response");
                var text=new System.Text.StringBuilder();var buffer=new char[4096];int count;
                while((count=reader.Read(buffer,0,buffer.Length))>0){if(text.Length+count>1024*1024)throw new InvalidDataException("Release metadata too large");text.Append(buffer,0,count);}
                return text.ToString();
            }
        });
    }
}
