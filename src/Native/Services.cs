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
        readonly UpdateCoordinator updater=new UpdateCoordinator(new UpdateClient(),typeof(Shell).Assembly.GetName().Version);
        readonly DispatcherTimer updateTimer=new DispatcherTimer();
        readonly GameOverlay overlay=new GameOverlay();FpsClient frames;readonly DispatcherTimer overlayTimer=new DispatcherTimer();Process target;bool refreshingGames;
        void WireUpdater(){
            Control<CheckBox>("AutoUpdates").IsChecked=settings.Flag("autoUpdates");Control<CheckBox>("AutoDownload").IsChecked=settings.Flag("autoDownload");
            Control<CheckBox>("AutoUpdates").Click+=delegate{settings.Data["autoUpdates"]=Checked("AutoUpdates");QueueSave();};
            Control<CheckBox>("AutoDownload").Click+=delegate{bool enabled=Checked("AutoDownload");settings.Data["autoDownload"]=enabled;if(enabled){settings.Data["autoUpdates"]=true;Control<CheckBox>("AutoUpdates").IsChecked=true;}QueueSave();};
            Click("CheckUpdates",()=>RunUpdate(updater.CheckAsync(DateTime.Now,settings.Flag("autoDownload"))));
            Click("GetUpdate",()=>RunUpdate(updater.DownloadAsync()));
            Click("InstallUpdate",delegate{updater.Install();RenderUpdate();if(updater.Installing)updateTimer.Start();});
            Click("HomeUpdate",delegate{
                if(updater.Busy)return;
                if(updater.Ready){updater.Install();RenderUpdate();if(updater.Installing)updateTimer.Start();}
                else if(updater.CanDownload)RunUpdate(updater.DownloadAsync());
                else RunUpdate(updater.CheckAsync(DateTime.Now,settings.Flag("autoDownload")));
            });
            updateTimer.Interval=TimeSpan.FromMilliseconds(500);updateTimer.Tick+=delegate{RenderUpdate();if(!updater.Busy)updateTimer.Stop();};
            poll.Tick+=delegate{if(!isolated&&settings.Flag("autoUpdates")&&updater.ShouldCheck(DateTime.Now))RunUpdate(updater.CheckAsync(DateTime.Now,settings.Flag("autoDownload")));};
        }
        async void RunUpdate(Task operation){
            bool wasReady=updater.Ready;RenderUpdate();if(updater.Busy)updateTimer.Start();
            await operation;if(disposed)return;
            RenderUpdate();if(!updater.Busy)updateTimer.Stop();
            if(!wasReady&&updater.Ready&&!isolated)tray.ShowBalloonTip(5000,"Hardware Pulse",language.T("Update ready to install"),Forms.ToolTipIcon.Info);
        }
        void RenderUpdate(){
            if(disposed)return;
            if(updater.StatusKey!=null)Text("UpdateStatus",language.T(updater.StatusKey)+(updater.Downloading?" · "+updater.Progress+"%":updater.VersionText!=null?" · "+updater.VersionText:""));
            Control<Button>("CheckUpdates").IsEnabled=!updater.Busy;
            Control<Button>("GetUpdate").Visibility=updater.CanDownload||updater.Downloading?Visibility.Visible:Visibility.Collapsed;
            Control<Button>("GetUpdate").IsEnabled=updater.CanDownload;
            Control<Button>("InstallUpdate").Visibility=updater.Ready?Visibility.Visible:Visibility.Collapsed;
            Control<Button>("InstallUpdate").IsEnabled=updater.Ready&&!updater.Busy;
            Control<ProgressBar>("DownloadProgress").Visibility=updater.Downloading?Visibility.Visible:Visibility.Collapsed;
            Control<ProgressBar>("DownloadProgress").Value=updater.Progress;
            var home=Control<Button>("HomeUpdate");
            bool failed=updater.StatusKey=="Update check failed; try again"||updater.StatusKey=="Update download failed; try again"||updater.StatusKey=="Installation canceled or failed; try again";
            string caption=updater.Installing?language.T("Installing update…"):
                updater.Downloading?language.T("Downloading update")+" · "+updater.Progress+"%":
                updater.Checking?language.T("Checking for updates…"):
                failed?language.T("Retry update"):
                updater.Ready?language.T("Install and restart"):
                updater.CanDownload?language.T("Update to")+" "+updater.VersionText:null;
            home.Visibility=caption==null?Visibility.Collapsed:Visibility.Visible;
            home.Content=caption;home.IsEnabled=!updater.Busy;
            home.ToolTip=updater.StatusKey==null?null:language.T(updater.StatusKey);
            System.Windows.Automation.AutomationProperties.SetName(home,caption??language.T("Check for Updates"));
        }
        void WireOverlay(){
            var state=settings.Map("overlay");
            WireOverlayAppearance();
            foreach(var pair in new[]{new[]{"OverlayEnabled","enabled"},new[]{"OverlayDetailed","detail"},new[]{"OverlayFps","fps"},new[]{"OverlayCpu","cpu"},new[]{"OverlayGpu","gpu"},new[]{"OverlayMemory","memory"},new[]{"OverlayFans","fans"},new[]{"OverlayStorage","storage"}}){string key=pair[1],name=pair[0];object saved;bool fallback=key=="fps"||key=="cpu"||key=="gpu"||key=="memory";Control<CheckBox>(name).IsChecked=state.TryGetValue(key,out saved)&&saved is bool?(bool)saved:fallback;Control<CheckBox>(name).Click+=delegate{state[key]=Checked(name);if(key=="enabled"||key=="fps")StartOverlay();QueueSave();UpdateOverlay();};}
            var positions=Control<ComboBox>("OverlayPosition");object position;string selected=state.TryGetValue("position",out position)?Convert.ToString(position):"top-left";foreach(ComboBoxItem item in positions.Items)if((string)item.Tag==selected)positions.SelectedItem=item;if(positions.SelectedItem==null)positions.SelectedIndex=0;positions.SelectionChanged+=delegate{state["position"]=(string)((ComboBoxItem)positions.SelectedItem).Tag;QueueSave();UpdateOverlay();};
            overlayTimer.Interval=TimeSpan.FromMilliseconds(500);overlayTimer.Tick+=delegate{UpdateOverlay();};Click("RefreshGames",RefreshGames);Click("ResetFps",delegate{if(frames!=null)frames.Reset();});Control<ComboBox>("GamePicker").SelectionChanged+=delegate{if(refreshingGames)return;var item=Control<ComboBox>("GamePicker").SelectedItem as ComboBoxItem;state["processName"]=item==null?"":(string)item.Tag;QueueSave();StartOverlay();};RefreshGames();StartOverlay();WireFpsSwitches();
        }
        void RefreshGames(){
            var picker=Control<ComboBox>("GamePicker");object saved;string selected=settings.Map("overlay").TryGetValue("processName",out saved)?Convert.ToString(saved):"";
            refreshingGames=true;try{picker.Items.Clear();picker.Items.Add(new ComboBoxItem{Content=language.T("Auto (foreground app)"),Tag=""});var names=new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach(var process in Process.GetProcesses())using(process){try{if(OverlayTarget.Eligible(process))names.Add(process.ProcessName);}catch{}}
                if(!string.IsNullOrEmpty(selected))names.Add(selected);foreach(string name in names)picker.Items.Add(new ComboBoxItem{Content=name,Tag=name});
                foreach(ComboBoxItem item in picker.Items)if(string.Equals((string)item.Tag,selected,StringComparison.OrdinalIgnoreCase))picker.SelectedItem=item;if(picker.SelectedItem==null)picker.SelectedIndex=0;
            }finally{refreshingGames=false;}
        }
        void StopOverlay(){overlayTimer.Stop();if(frames!=null)frames.Dispose();frames=null;overlay.Hide();if(target!=null)target.Dispose();target=null;}
        void StartOverlay(){SyncFpsSwitches();StopOverlay();if(!Checked("OverlayEnabled")){Text("OverlayStatus",language.T("FPS capture stopped"));return;}if(isolated)return;if(Checked("OverlayFps"))frames=new FpsClient(paths.Exe);overlayTimer.Start();UpdateOverlay();}
        static string OverlayValue(Reading data,string key,string unit){double value;return data.values.TryGetValue(key,out value)?value.ToString("0.#")+unit:"—";}
        void UpdateOverlay(){if(!Checked("OverlayEnabled")||isolated){overlay.Hide();return;}
            var choice=Control<ComboBox>("GamePicker").SelectedItem as ComboBoxItem;string name=choice==null?"":(string)choice.Tag;
            var next=OverlayTarget.Resolve(name,target);if(!object.ReferenceEquals(next,target)){if(target!=null)target.Dispose();target=next;}
            if(target==null){if(frames!=null)frames.Select(0,0);overlay.Hide();Text("OverlayStatus",language.T("Waiting for target app"));return;}
            string processName;IntPtr targetWindow;
            try{target.Refresh();processName=target.ProcessName;targetWindow=target.MainWindowHandle;if(frames!=null)frames.Select(target.Id,target.StartTime.ToUniversalTime().Ticks);}catch{target.Dispose();target=null;if(frames!=null)frames.Select(0,0);overlay.Hide();return;}
            var data=readings.Latest;var parts=new List<string>();
            if(Checked("OverlayFps")&&frames!=null){var metrics=frames.Read();parts.Add(metrics.Ready?string.Format("FPS {0:0}  AVG {1:0}  MIN {2:0}  1% LOW {3}",metrics.Current,metrics.Average,metrics.Minimum,double.IsNaN(metrics.Low)?"—":metrics.Low.ToString("0")):"FPS — · "+language.T(metrics.Status));Text("OverlayStatus",processName+" · "+language.T(metrics.Status));}
            if(Checked("OverlayCpu"))parts.Add("CPU "+OverlayValue(data,"cpu","°C")+" · "+OverlayValue(data,"cpuLoad","%"));if(Checked("OverlayGpu"))parts.Add("GPU "+OverlayValue(data,"gpu","°C")+" · "+OverlayValue(data,"gpuLoad","%"));
            if(Checked("OverlayMemory"))foreach(string key in new[]{"ram","vram"}){Usage usage;parts.Add(data.state=="LIVE"&&data.usage.TryGetValue(key,out usage)?string.Format("{0} {1:0.0}/{2:0.0} GB",language.T(usage.label??key.ToUpperInvariant()),usage.used,usage.total):key.ToUpperInvariant()+" —");}
            if(Checked("OverlayFans"))parts.Add("FAN CPU "+OverlayValue(data,"cpuFan"," RPM")+" · GPU "+OverlayValue(data,"gpuFan"," RPM"));if(Checked("OverlayStorage"))parts.Add("NVMe "+OverlayValue(data,"diskC","°C")+" / "+OverlayValue(data,"diskD","°C"));
            if(Checked("OverlayDetailed")){parts.Insert(0,processName+" · "+language.T("Rolling 60 s"));if(Checked("OverlayCpu"))parts.Add("Vcore "+OverlayValue(data,"vcore"," V"));if(Checked("OverlayGpu"))parts.Add("VRAM "+OverlayValue(data,"vram","°C")+" · "+OverlayValue(data,"gpuVolt"," V"));}
            if(parts.Count==0)parts.Add("Pulse");overlay.Display(targetWindow,string.Join(Checked("OverlayDetailed")?Environment.NewLine:"   |   ",parts),(string)((ComboBoxItem)Control<ComboBox>("OverlayPosition").SelectedItem).Tag);
        }
    }
}
