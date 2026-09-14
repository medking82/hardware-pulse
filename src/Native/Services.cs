using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace HardwarePulse {
    public sealed partial class Shell {
        sealed class ReleaseAsset {public string name,browser_download_url,digest;public long size;}
        sealed class ReleaseInfo {public bool draft,prerelease;public string tag_name;public ReleaseAsset[] assets;}
        readonly UpdateCheck updater=new UpdateCheck();readonly DispatcherTimer updateTimer=new DispatcherTimer();
        DateTime nextCheck=DateTime.MinValue;ReleaseAsset asset;string updateTag;bool checking,downloading;
        readonly GameOverlay overlay=new GameOverlay();readonly FrameCapture frames=new FrameCapture();readonly DispatcherTimer overlayTimer=new DispatcherTimer();Process target;
        void WireUpdater(){
            Control<CheckBox>("AutoUpdates").IsChecked=settings.Flag("autoUpdates");Control<CheckBox>("AutoDownload").IsChecked=settings.Flag("autoDownload");
            Control<CheckBox>("AutoUpdates").Click+=delegate{settings.Data["autoUpdates"]=Checked("AutoUpdates");QueueSave();};
            Control<CheckBox>("AutoDownload").Click+=delegate{bool enabled=Checked("AutoDownload");settings.Data["autoDownload"]=enabled;if(enabled){settings.Data["autoUpdates"]=true;Control<CheckBox>("AutoUpdates").IsChecked=true;}QueueSave();};
            Click("CheckUpdates",()=>CheckUpdate());Click("GetUpdate",()=>DownloadUpdate());Click("InstallUpdate",delegate{try{updater.Install();Control<Button>("InstallUpdate").IsEnabled=false;updateTimer.Start();}catch{Text("UpdateStatus",language.T("Installation canceled or failed; try again"));Control<Button>("InstallUpdate").IsEnabled=true;}});
            updateTimer.Interval=TimeSpan.FromMilliseconds(500);updateTimer.Tick+=delegate{if(downloading){Control<ProgressBar>("DownloadProgress").Value=updater.Progress;Text("UpdateStatus",language.T("Downloading update")+" · "+updater.Progress+"%");}else if(!updater.Installing){Control<Button>("InstallUpdate").IsEnabled=true;updateTimer.Stop();}};
            poll.Tick+=delegate{if(!isolated&&settings.Flag("autoUpdates")&&DateTime.Now>=nextCheck&&!checking&&!downloading&&!updater.Ready)CheckUpdate();};
        }
        async void CheckUpdate(){if(checking||downloading||updater.Installing)return;if(updater.Ready){Control<Button>("InstallUpdate").Visibility=Visibility.Visible;Text("UpdateStatus",language.T("Update ready to install"));return;}checking=true;nextCheck=DateTime.Now.AddHours(6);Control<Button>("CheckUpdates").IsEnabled=false;Text("UpdateStatus",language.T("Checking for updates…"));
            try{updater.Start();var json=await updater.Pending;if(disposed)return;var release=Json.Serializer().Deserialize<ReleaseInfo>(json);Version remote;if(release==null||release.draft||release.prerelease||!Version.TryParse((release.tag_name??"").TrimStart('v'),out remote))throw new InvalidDataException("Not a stable release");bool newer=remote>new Version("0.5.2");Text("UpdateStatus",newer?language.T("Update available")+" · "+remote:language.T("You are up to date"));Control<Button>("GetUpdate").Visibility=Visibility.Collapsed;asset=null;
                if(newer){var assets=(release.assets??new ReleaseAsset[0]).Where(a=>a.name=="HardwarePulse-Setup.exe").ToArray();if(assets.Length!=1||!UpdateCheck.ValidAsset(assets[0].browser_download_url,release.tag_name,assets[0].digest,assets[0].size))throw new InvalidDataException("Invalid installer metadata");asset=assets[0];updateTag=release.tag_name;Control<Button>("GetUpdate").Visibility=Visibility.Visible;if(settings.Flag("autoDownload"))DownloadUpdate();}
            }catch{if(!disposed)Text("UpdateStatus",language.T("Update check failed; try again"));}finally{checking=false;if(!disposed)Control<Button>("CheckUpdates").IsEnabled=!downloading;}
        }
        async void DownloadUpdate(){if(downloading||asset==null)return;downloading=true;Control<Button>("GetUpdate").IsEnabled=false;Control<Button>("CheckUpdates").IsEnabled=false;Control<Button>("InstallUpdate").Visibility=Visibility.Collapsed;Control<ProgressBar>("DownloadProgress").Visibility=Visibility.Visible;updateTimer.Start();
            try{updater.Download(asset.browser_download_url,updateTag,asset.digest,asset.size);await updater.DownloadPending;if(disposed)return;Control<Button>("GetUpdate").Visibility=Visibility.Collapsed;Control<Button>("InstallUpdate").Visibility=Visibility.Visible;Text("UpdateStatus",language.T("Update ready to install"));if(!isolated)tray.ShowBalloonTip(5000,"Hardware Pulse",language.T("Update ready to install"),Forms.ToolTipIcon.Info);}
            catch{if(!disposed)Text("UpdateStatus",language.T("Update download failed; try again"));}finally{downloading=false;updateTimer.Stop();if(!disposed){Control<ProgressBar>("DownloadProgress").Visibility=Visibility.Collapsed;Control<Button>("GetUpdate").IsEnabled=true;Control<Button>("CheckUpdates").IsEnabled=true;}}
        }
        void WireOverlay(){
            var state=settings.Map("overlay");state["enabled"]=false;
            foreach(var pair in new[]{new[]{"OverlayEnabled","enabled"},new[]{"OverlayDetailed","detail"},new[]{"OverlayFps","fps"},new[]{"OverlayCpu","cpu"},new[]{"OverlayGpu","gpu"},new[]{"OverlayMemory","memory"},new[]{"OverlayFans","fans"},new[]{"OverlayStorage","storage"}}){string key=pair[1],name=pair[0];object saved;bool fallback=key=="fps"||key=="cpu"||key=="gpu"||key=="memory";Control<CheckBox>(name).IsChecked=state.TryGetValue(key,out saved)&&saved is bool?(bool)saved:fallback;Control<CheckBox>(name).Click+=delegate{state[key]=Checked(name);if(key=="enabled"||key=="fps")StartOverlay();QueueSave();UpdateOverlay();};}
            var positions=Control<ComboBox>("OverlayPosition");object position;string selected=state.TryGetValue("position",out position)?Convert.ToString(position):"top-left";foreach(ComboBoxItem item in positions.Items)if((string)item.Tag==selected)positions.SelectedItem=item;if(positions.SelectedItem==null)positions.SelectedIndex=0;positions.SelectionChanged+=delegate{state["position"]=(string)((ComboBoxItem)positions.SelectedItem).Tag;QueueSave();UpdateOverlay();};
            overlayTimer.Interval=TimeSpan.FromMilliseconds(500);overlayTimer.Tick+=delegate{UpdateOverlay();};Click("RefreshGames",RefreshGames);Click("ResetFps",StartOverlay);Control<ComboBox>("GamePicker").SelectionChanged+=delegate{StartOverlay();};RefreshGames();
        }
        void RefreshGames(){var picker=Control<ComboBox>("GamePicker");picker.Items.Clear();foreach(var process in Process.GetProcesses().OrderBy(p=>p.ProcessName))using(process){try{if(process.MainWindowHandle!=IntPtr.Zero&&process.Id!=Process.GetCurrentProcess().Id)picker.Items.Add(new ComboBoxItem {Content=process.ProcessName+" · "+process.Id,Tag=process.Id});}catch{}}}
        void StopOverlay(){overlayTimer.Stop();frames.Dispose();overlay.Hide();if(target!=null)target.Dispose();target=null;}
        void StartOverlay(){StopOverlay();var choice=Control<ComboBox>("GamePicker").SelectedItem as ComboBoxItem;if(choice==null||!Checked("OverlayEnabled"))return;try{target=Process.GetProcessById((int)choice.Tag);if(Checked("OverlayFps"))frames.Start(Path.Combine(paths.Root,"tools","PresentMon.exe"),target.Id);overlayTimer.Start();}catch{Text("OverlayStatus",language.T("FPS capture failed"));}}
        static string OverlayValue(Reading data,string key,string unit){double value;return data.values.TryGetValue(key,out value)?value.ToString("0.#")+unit:"—";}
        void UpdateOverlay(){if(target==null||!Checked("OverlayEnabled")){overlay.Hide();return;}try{target.Refresh();if(target.HasExited){StopOverlay();return;}}catch{StopOverlay();return;}var data=latest;var parts=new List<string>();
            if(Checked("OverlayFps")){var metrics=frames.Read();parts.Add(metrics.Ready?string.Format("FPS {0:0}  AVG {1:0}  MIN {2:0}  1% LOW {3}",metrics.Current,metrics.Average,metrics.Minimum,double.IsNaN(metrics.Low)?"—":metrics.Low.ToString("0")):"FPS — · "+language.T(metrics.Status));Text("OverlayStatus",language.T(metrics.Status));}
            if(Checked("OverlayCpu"))parts.Add("CPU "+OverlayValue(data,"cpu","°C")+" · "+OverlayValue(data,"cpuLoad","%"));if(Checked("OverlayGpu"))parts.Add("GPU "+OverlayValue(data,"gpu","°C")+" · "+OverlayValue(data,"gpuLoad","%"));
            if(Checked("OverlayMemory"))foreach(string key in new[]{"ram","vram"}){Usage usage;parts.Add(data.state=="LIVE"&&data.usage.TryGetValue(key,out usage)?string.Format("{0} {1:0.0}/{2:0.0} GB",language.T(usage.label??key.ToUpperInvariant()),usage.used,usage.total):key.ToUpperInvariant()+" —");}
            if(Checked("OverlayFans"))parts.Add("FAN CPU "+OverlayValue(data,"cpuFan"," RPM")+" · GPU "+OverlayValue(data,"gpuFan"," RPM"));if(Checked("OverlayStorage"))parts.Add("NVMe "+OverlayValue(data,"diskC","°C")+" / "+OverlayValue(data,"diskD","°C"));
            if(Checked("OverlayDetailed")){parts.Insert(0,target.ProcessName+" · "+language.T("Rolling 60 s"));if(Checked("OverlayCpu"))parts.Add("Vcore "+OverlayValue(data,"vcore"," V"));if(Checked("OverlayGpu"))parts.Add("VRAM "+OverlayValue(data,"vram","°C")+" · "+OverlayValue(data,"gpuVolt"," V"));}
            if(parts.Count==0)parts.Add("Pulse");overlay.Display(target.MainWindowHandle,string.Join(Checked("OverlayDetailed")?Environment.NewLine:"   |   ",parts),(string)((ComboBoxItem)Control<ComboBox>("OverlayPosition").SelectedItem).Tag);
        }
    }
}
