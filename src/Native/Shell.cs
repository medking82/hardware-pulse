using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml;
using Forms=System.Windows.Forms;

namespace HardwarePulse {
    public sealed partial class Shell : IDisposable {
        public readonly Window Window;readonly PulsePaths paths;readonly Settings settings;readonly Languages language;
        readonly StackPanel cards;readonly Dictionary<string,CardView> views=new Dictionary<string,CardView>();
        readonly ReadingSession readings;
        readonly CollectorRecovery collectorRecovery=new CollectorRecovery();
        readonly HardwareDiagnosticLog hardwareLog;
        readonly List<Tuple<object,PropertyInfo,string>> localized=new List<Tuple<object,PropertyInfo,string>>();
        readonly List<Tuple<object,PropertyInfo,Brush>> themed=new List<Tuple<object,PropertyInfo,Brush>>();
        readonly DispatcherTimer poll=new DispatcherTimer(),saveTimer=new DispatcherTimer();
        Forms.NotifyIcon tray;Forms.ToolStripMenuItem trayShow,traySettings,trayPin,trayLock,trayExit;
        bool exit,loaded,settingsVisible,maximum,locked,measuring,light,collectorFailed,disposed;long ignoredStop;
        readonly bool isolated;
        readonly Version windowsVersion;
        bool FpsSupported {get{return WindowsCompatibility.SupportsFpsCapture(windowsVersion);}}
        bool LocalContrastSupported {get{return WindowsCompatibility.SupportsCaptureExclusion(windowsVersion);}}
        public T Control<T>(string name) where T:class{return Window.FindName(name) as T;}
        bool Checked(string name){return Control<CheckBox>(name).IsChecked==true;}
        void Text(string name,string value){Control<TextBlock>(name).Text=value;}
        readonly Dictionary<string,Brush> brushes=new Dictionary<string,Brush>();
        Brush Brush(string color){Brush brush;if(!brushes.TryGetValue(color,out brush)){brush=(Brush)new BrushConverter().ConvertFromString(color);brush.Freeze();brushes.Add(color,brush);}return brush;}
        static IEnumerable<DependencyObject> Tree(DependencyObject node){yield return node;foreach(object child in LogicalTreeHelper.GetChildren(node)){var d=child as DependencyObject;if(d!=null)foreach(var item in Tree(d))yield return item;}}
        void Catalog(DependencyObject root){foreach(var node in Tree(root)){if(node is TextBox)continue;foreach(string name in new[]{"Text","Content","Header","ToolTip"}){var prop=node.GetType().GetProperty(name);if(prop!=null&&prop.CanWrite){var value=prop.GetValue(node,null) as string;if(value!=null&&language.Contains(value))localized.Add(Tuple.Create((object)node,prop,value));}}}}
        void ThemeCatalog(){foreach(var node in Tree(Window)){if(node is ComboBox||node is ComboBoxItem||object.ReferenceEquals(node,Control<TextBlock>("OverlayPreviewText")))continue;var prop=node.GetType().GetProperty("Foreground");if(prop!=null){var brush=prop.GetValue(node,null) as Brush;if(brush!=null)themed.Add(Tuple.Create((object)node,prop,brush));}}}
        public Shell(PulsePaths paths,bool isolated=false,Version platformVersion=null){
            if(platformVersion!=null&&!isolated)throw new ArgumentException("Platform override requires isolated mode");
            windowsVersion=platformVersion??WindowsCompatibility.CurrentVersion();
            bool legacyUpdates=WindowsCompatibility.RequiresDriverFreeCollector(windowsVersion);
            updater=new UpdateCoordinator(new UpdateClient(legacyUpdates),typeof(Shell).Assembly.GetName().Version,legacyUpdates);
            this.paths=paths;this.isolated=isolated;AtomicFile.EnsureDirectory(paths.State);
            var snapshotReader=new SensorProfile.Reader(paths.Snapshot);
            readings=new ReadingSession(snapshotReader.Read);
            hardwareLog=new HardwareDiagnosticLog(paths.State);
            settings=new Settings(Path.Combine(paths.State,"widget-settings.json"));language=new Languages(Path.Combine(paths.Root,"Languages.txt"));language.Preference=settings.Text("language","auto");
            claudeAutoSource=settings.Flag("claudeAutoSource",false);claudeCliSource=!claudeAutoSource&&settings.Flag("claudeCliSource",false);claudeSnapshotSource=!claudeAutoSource&&!claudeCliSource&&settings.Flag("claudeSnapshotSource",false);quotas=new QuotaSession(ReadQuota,QuotaProviders.ReadClaudeAuto);
            if(!isolated){
                var quotaStore=new QuotaScheduleStore(paths.State);
                quotas.RestoreSchedules(quotaStore.Load());quotas.ScheduleChanged+=quotaStore.Save;
                quotas.AttemptCompleted+=new QuotaDiagnosticLog(paths.State).Record;
            }
            using(var stream=File.OpenRead(Path.Combine(paths.Root,"Panel.xaml")))Window=(Window)XamlReader.Load(stream);
            Catalog(Window);BuildSettingsLayout();ReplaceCardPanel("Cards");ReplaceCardPanel("QuotaCards");cards=Control<StackPanel>("Cards");
            var area=SystemParameters.WorkArea;
            Window.Width=settings.Number("width",280,240,Math.Max(240,area.Width));Window.Height=settings.Number("height",Math.Max(340,Math.Min(650,Math.Floor(area.Height*.9))),340,Math.Max(340,area.Height));
            if(settings.Data.ContainsKey("left")){Window.WindowStartupLocation=WindowStartupLocation.Manual;Window.Left=settings.Number("left",area.Left,area.Left,Math.Max(area.Left,area.Right-Window.Width));Window.Top=settings.Number("top",area.Top,area.Top,Math.Max(area.Top,area.Bottom-Window.Height));}
            Window.FontSize=settings.Number("fontSize",settings.Flag("large")?14:12,10,16);Window.Topmost=settings.Flag("pin");locked=settings.Flag("positionLocked");
            Control<CheckBox>("Pin").IsChecked=Window.Topmost;Control<CheckBox>("Solid").IsChecked=settings.Flag("solid");Control<Slider>("OpacitySlider").Value=settings.Number("opacity",85,0,100);Control<Slider>("FontSizeSlider").Value=Window.FontSize;
            Control<ContentControl>("BrandIcon").Content=Icon("live",22,"#A5E7D5");Window.Icon=BitmapFrame.Create(new Uri(Path.Combine(paths.Root,"assets","pulse.ico")));
            BuildCards();WireSettings();WireReadingColors();WireNetwork();WireQuota();BuildTray();WireDesktop();WireOverlay();WireUpdater();WireLocalInference();Click("ExportDiagnostics",ExportDiagnostics);ThemeCatalog();Localize();
            Window.SourceInitialized+=delegate{WindowSnap.Attach(Window);ApplyLock();ApplyMaterial();};
            Window.Loaded+=delegate{Initialize();};
            Window.SizeChanged+=delegate{ApplyDensity();QueueSave();};Window.LocationChanged+=delegate{QueueSave();};
            Window.Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){Save();if(!exit){e.Cancel=true;Window.Hide();}};
            Window.Closed+=delegate{Dispose();};
            Window.IsVisibleChanged+=delegate{if(Window.IsVisible&&loaded)UpdatePanel();};
            Window.StateChanged+=delegate{if(loaded&&Window.WindowState!=WindowState.Minimized)UpdatePanel();};
            saveTimer.Interval=TimeSpan.FromMilliseconds(750);saveTimer.Tick+=delegate{saveTimer.Stop();Save();};
            poll.Interval=TimeSpan.FromSeconds(2);poll.Tick+=delegate{UpdatePanel();};poll.Start();
        }
        public void Start(){if(DesktopEnabled)Initialize();else Show();}
        void Initialize(){if(loaded||disposed)return;loaded=true;if(!isolated){StartCollector();collectorRecovery.Observe(DateTimeOffset.UtcNow,false,File.Exists(paths.Stop));}UpdatePanel();ApplyDensity();}
        void StartCollector(){try{if(File.Exists(paths.Stop)){try{AtomicFile.Delete(paths.Stop);}catch{ignoredStop=File.GetLastWriteTimeUtc(paths.Stop).Ticks;throw;}}if(SensorProfile.Read(paths.Snapshot,DateTimeOffset.Now).state!="LIVE")using(var store=new SchedulerStore())new Startup(store,paths.Exe,WindowsIdentity.GetCurrent().User.Value).StartCollector();}catch{collectorFailed=true;}}
        void RecoverCollector(DateTimeOffset now){
            if(isolated||!loaded||disposed||exit)return;
            bool healthy=readings.Latest.state=="LIVE"&&readings.Latest.readErrorCode==0;
            if(healthy)collectorFailed=false;
            if(!collectorRecovery.Observe(now,healthy,File.Exists(paths.Stop)))return;
            try{
                using(var store=new SchedulerStore()){
                    // Recheck after COM connection; recovery must never clear a
                    // cooperative STOP or bypass the owned task validation.
                    if(!disposed&&!exit&&!File.Exists(paths.Stop))new Startup(store,paths.Exe,WindowsIdentity.GetCurrent().User.Value).StartCollector();
                }
            }catch{collectorFailed=true;}
        }
        public void UpdatePanel(){
            if(disposed)return;
            if(!isolated&&File.Exists(paths.Stop)&&File.GetLastWriteTimeUtc(paths.Stop).Ticks!=ignoredStop){Exit();return;}
            readings.Poll(DateTimeOffset.Now);RecoverCollector(DateTimeOffset.UtcNow);if(!isolated){TickQuotas(DateTimeOffset.UtcNow);localInference.Tick(DateTimeOffset.UtcNow);}RenderQuotaRefreshFeedback(DateTimeOffset.UtcNow);UpdateDesktop();
            if(!isolated)hardwareLog.Record(readings.Latest,DateTimeOffset.UtcNow);
            // Keep collection, peaks, stale-state detection and STOP handling active
            // while Settings or the tray/minimized window has no visible cards to render.
            if(Window.IsVisible&&Window.WindowState!=WindowState.Minimized){
                if(settingsVisible)UpdateSettingsSaveStatus();else RenderPanel();
            }
            if(loaded) {
                // This diagnostic is optional; sharing conflicts must not stop the UI timer.
                try{Json.WriteAtomic(Path.Combine(paths.State,"view-status.json"),new {updated=DateTimeOffset.Now.ToString("o"),state=readings.Latest.state,reason=HardwareDiagnosticLog.Reason(readings.Latest),snapshotTime=readings.Latest.time==default(DateTimeOffset)?null:readings.Latest.time.ToString("o"),mode=maximum?"max":"live",sensors=readings.Latest.values.Count});}
                catch(IOException){}catch(UnauthorizedAccessException){}
            }
        }
        void RenderPanel(){
            bool recent=readings.Latest.state=="LIVE"&&HardwareDiagnosticLog.Reason(readings.Latest)=="Snapshot busy";
            Text("Status",readings.Latest.state=="LIVE"?"● "+language.Label(recent?"Recent sample":"Live")+" · "+readings.Latest.time.ToLocalTime().ToString("HH:mm:ss")+" · "+(recent?language.Label("Retrying snapshot"):readings.Latest.values.Count+" "+language.T("sensors")):"● "+language.Label(readings.Latest.state)+" · "+language.Label(HardwareDiagnosticLog.Reason(readings.Latest)));
            if(collectorFailed&&readings.Latest.state!="LIVE")Text("Status",language.T("Collector start failed; reinstall or check permissions"));
            if(maximum)Control<TextBlock>("Status").Text+=" · "+language.T("Session peaks");
            Control<TextBlock>("Status").Foreground=Brush(light?(readings.Latest.state=="LIVE"&&!recent?"#12644D":"#804000"):(readings.Latest.state=="LIVE"&&!recent?"#A5E7D5":"#E7C5A4"));
            foreach(var view in views.Values)UpdateCard(view);RenderQuota();RenderLocalInference();
            UpdateMonitorControls();ApplyDensity(false);
        }
        void QueueSave(){
            if(!loaded||disposed)return;
            settingsSaveStatus="Unsaved Changes";UpdateSettingsSaveStatus();
            saveTimer.Stop();saveTimer.Start();
        }
        public void Save(){
            if(!loaded)return;
            if(settings.LoadFailed){UpdateSettingsSaveStatus();return;}
            var bounds=Window.RestoreBounds;
            if(!bounds.IsEmpty){settings.Data["width"]=bounds.Width;settings.Data["height"]=bounds.Height;settings.Data["left"]=bounds.Left;settings.Data["top"]=bounds.Top;}
            settings.Data["fontSize"]=Window.FontSize;settings.Data["pin"]=Window.Topmost;settings.Data["solid"]=Checked("Solid");settings.Data["opacity"]=Control<Slider>("OpacitySlider").Value;settings.Data["language"]=language.Preference;settings.Data["positionLocked"]=locked;settings.Data["cardOrder"]=cards.Children.Cast<Border>().Select(c=>(string)c.Tag).ToArray();
            try{settings.Save();settingsSaveStatus="Changes Saved";}
            catch(Exception e){settingsSaveStatus="Could Not Save Changes";AtomicFile.TryWriteAllText(Path.Combine(paths.State,"settings-error.txt"),e.Message);}
            // Show failure in the UI itself: writing an error log can fail for the same reason.
            UpdateSettingsSaveStatus();
        }
        Viewbox Icon(string name,double size,string color="#C2D8E5"){
            var doc=new XmlDocument();doc.XmlResolver=null;doc.Load(Path.Combine(paths.Root,"assets",name+".svg"));var node=doc.SelectSingleNode("/*[local-name()='svg']/*[local-name()='path']");
            if(name=="strata")return StrataIcon(doc,size,color);
            bool filled=node.Attributes["fill"]!=null&&node.Attributes["fill"].Value=="currentColor";
            var path=new System.Windows.Shapes.Path {Data=Geometry.Parse(node.Attributes["d"].Value),Fill=filled?Brush(color):null,Stroke=filled?null:Brush(color),StrokeThickness=1.7,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round};var canvas=new Canvas {Width=24,Height=24};canvas.Children.Add(path);return new Viewbox {Width=size,Height=size,Child=canvas,IsHitTestVisible=false};
        }
        void Localize(){LocalizeDeepSeekKeyStatus();LocalizeJevSessionStatus();UpdateDesktopLabels();foreach(var entry in localized)entry.Item2.SetValue(entry.Item1,language.Label(entry.Item3),null);trayShow.Text=language.Label("Show Pulse");traySettings.Text=language.Label("Settings");trayPin.Text=language.Label("Always on Top");trayLock.Text=language.Label("Lock Position and Size");trayExit.Text=language.Label("Exit");foreach(var view in views.Values)UpdateCard(view);if(loaded)RenderPanel();RenderUpdate();var games=Control<ComboBox>("GamePicker");if(games.Items.Count>0)((ComboBoxItem)games.Items[0]).Content=language.Label("Auto (foreground app)");SyncFpsSwitches();ApplyDensity();}
        void BuildTray(){tray=new Forms.NotifyIcon {Icon=new System.Drawing.Icon(Path.Combine(paths.Root,"assets","pulse.ico")),Text="Hardware Pulse",Visible=!isolated};var menu=new Forms.ContextMenuStrip();trayShow=(Forms.ToolStripMenuItem)menu.Items.Add("Show Pulse",null,delegate{ShowHome();});traySettings=(Forms.ToolStripMenuItem)menu.Items.Add("Settings",null,delegate{Show();ShowSettings(true);});menu.Items.Add(new Forms.ToolStripSeparator());trayPin=(Forms.ToolStripMenuItem)menu.Items.Add("Always on Top",null,delegate{Window.Topmost=!Window.Topmost;Control<CheckBox>("Pin").IsChecked=Window.Topmost;Save();});trayLock=(Forms.ToolStripMenuItem)menu.Items.Add("Lock Position and Size",null,delegate{locked=!locked;ApplyLock();Save();});menu.Items.Add(new Forms.ToolStripSeparator());BuildDesktopTray(menu);menu.Items.Add(new Forms.ToolStripSeparator());trayExit=(Forms.ToolStripMenuItem)menu.Items.Add("Exit",null,delegate{Exit();});menu.Opening+=delegate{trayPin.Checked=Window.Topmost;trayLock.Checked=locked;};tray.DoubleClick+=delegate{ShowHome();};tray.ContextMenuStrip=menu;}
        public void ShowHome(){ShowSettings(false);Show();}
        public void Show(){Window.Show();Window.WindowState=WindowState.Normal;Window.Activate();}
        public void Exit(){exit=true;Window.Close();}
        public void Dispose(){if(disposed)return;disposed=true;if(desktopHotkey!=null)desktopHotkey.Dispose();quotas.Dispose();localInference.Dispose();poll.Stop();saveTimer.Stop();StopOverlay();overlay.Close();if(desktop!=null){desktop.Close();desktop=null;}updateTimer.Stop();updater.Dispose();tray.Visible=false;tray.Dispose();if(!isolated){try{if(File.Exists(paths.Snapshot))AtomicFile.WriteAllText(paths.Stop,"Pulse Exit");}catch(Exception e){AtomicFile.TryWriteAllText(Path.Combine(paths.State,"shutdown-error.txt"),e.Message);}}}
    }
}
