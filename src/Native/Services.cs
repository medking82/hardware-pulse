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
        readonly GameOverlay overlay=new GameOverlay();readonly FrameCapture frames=new FrameCapture();readonly DispatcherTimer overlayTimer=new DispatcherTimer();Process target;
        void WireUpdater(){
            Control<CheckBox>("AutoUpdates").IsChecked=settings.Flag("autoUpdates");Control<CheckBox>("AutoDownload").IsChecked=settings.Flag("autoDownload");
            Control<CheckBox>("AutoUpdates").Click+=delegate{settings.Data["autoUpdates"]=Checked("AutoUpdates");QueueSave();};
            Control<CheckBox>("AutoDownload").Click+=delegate{bool enabled=Checked("AutoDownload");settings.Data["autoDownload"]=enabled;if(enabled){settings.Data["autoUpdates"]=true;Control<CheckBox>("AutoUpdates").IsChecked=true;}QueueSave();};
            Click("CheckUpdates",()=>RunUpdate(updater.CheckAsync(DateTime.Now,settings.Flag("autoDownload"))));
            Click("GetUpdate",()=>RunUpdate(updater.DownloadAsync()));
            Click("InstallUpdate",delegate{updater.Install();RenderUpdate();if(updater.Installing)updateTimer.Start();});
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
        void UpdateOverlay(){if(target==null||!Checked("OverlayEnabled")){overlay.Hide();return;}try{target.Refresh();if(target.HasExited){StopOverlay();return;}}catch{StopOverlay();return;}var data=readings.Latest;var parts=new List<string>();
            if(Checked("OverlayFps")){var metrics=frames.Read();parts.Add(metrics.Ready?string.Format("FPS {0:0}  AVG {1:0}  MIN {2:0}  1% LOW {3}",metrics.Current,metrics.Average,metrics.Minimum,double.IsNaN(metrics.Low)?"—":metrics.Low.ToString("0")):"FPS — · "+language.T(metrics.Status));Text("OverlayStatus",language.T(metrics.Status));}
            if(Checked("OverlayCpu"))parts.Add("CPU "+OverlayValue(data,"cpu","°C")+" · "+OverlayValue(data,"cpuLoad","%"));if(Checked("OverlayGpu"))parts.Add("GPU "+OverlayValue(data,"gpu","°C")+" · "+OverlayValue(data,"gpuLoad","%"));
            if(Checked("OverlayMemory"))foreach(string key in new[]{"ram","vram"}){Usage usage;parts.Add(data.state=="LIVE"&&data.usage.TryGetValue(key,out usage)?string.Format("{0} {1:0.0}/{2:0.0} GB",language.T(usage.label??key.ToUpperInvariant()),usage.used,usage.total):key.ToUpperInvariant()+" —");}
            if(Checked("OverlayFans"))parts.Add("FAN CPU "+OverlayValue(data,"cpuFan"," RPM")+" · GPU "+OverlayValue(data,"gpuFan"," RPM"));if(Checked("OverlayStorage"))parts.Add("NVMe "+OverlayValue(data,"diskC","°C")+" / "+OverlayValue(data,"diskD","°C"));
            if(Checked("OverlayDetailed")){parts.Insert(0,target.ProcessName+" · "+language.T("Rolling 60 s"));if(Checked("OverlayCpu"))parts.Add("Vcore "+OverlayValue(data,"vcore"," V"));if(Checked("OverlayGpu"))parts.Add("VRAM "+OverlayValue(data,"vram","°C")+" · "+OverlayValue(data,"gpuVolt"," V"));}
            if(parts.Count==0)parts.Add("Pulse");overlay.Display(target.MainWindowHandle,string.Join(Checked("OverlayDetailed")?Environment.NewLine:"   |   ",parts),(string)((ComboBoxItem)Control<ComboBox>("OverlayPosition").SelectedItem).Tag);
        }
    }
}
