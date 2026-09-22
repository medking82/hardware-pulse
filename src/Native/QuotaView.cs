using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace HardwarePulse {
    public sealed partial class Shell {
        readonly QuotaSession quotas;
        volatile bool claudeSnapshotSource;
        string quotaSignature;
        string[] QuotaOrder(){return settings.Order("quotaCardOrder",QuotaSession.Providers);}
        void WireQuota(){
            var source=Control<ComboBox>("ClaudeQuotaSource");source.SelectedIndex=claudeSnapshotSource?1:0;
            Control<StackPanel>("ClaudeSnapshotTools").Visibility=claudeSnapshotSource?Visibility.Visible:Visibility.Collapsed;
            source.SelectionChanged+=delegate{
                quotas.Enable("Claude",false);claudeSnapshotSource=source.SelectedIndex==1;
                settings.Data["claudeSnapshotSource"]=claudeSnapshotSource;
                Control<StackPanel>("ClaudeSnapshotTools").Visibility=claudeSnapshotSource?Visibility.Visible:Visibility.Collapsed;
                quotas.Enable("Claude",Checked("QuotaClaude"));if(!isolated)quotas.Tick(DateTimeOffset.UtcNow);
                RenderQuota();UpdateDesktop();QueueSave();
            };
            Click("ClaudeSnapshotRebind",delegate{
                if(ClaudeStatusLineReceiver.Reset(paths.State)){
                    quotas.Enable("Claude",false);quotas.Enable("Claude",Checked("QuotaClaude"));
                    Text("ClaudeSnapshotSetupStatus",language.T("Waiting for the next CLI session. Other sessions will be rejected."));
                    RenderQuota();UpdateDesktop();
                }else Text("ClaudeSnapshotSetupStatus",language.T("Snapshot busy; try again."));
            });
            Click("ClaudeSnapshotCommand",delegate{
                var button=Control<Button>("ClaudeSnapshotCommand");var menu=new ContextMenu{PlacementTarget=button,Placement=PlacementMode.Bottom};
                foreach(bool usePowerShell in new[]{false,true}){
                    bool selected=usePowerShell;var item=new MenuItem{Header=language.T(selected?"PowerShell (without Git Bash)":"Git Bash installed")};
                    item.Click+=delegate{CopyClaudeStatusLineCommand(selected);};menu.Items.Add(item);
                }
                button.ContextMenu=menu;menu.IsOpen=true;
            });
            var mode=Control<ComboBox>("QuotaDisplay");mode.SelectedIndex=settings.Flag("quotaFull")?1:0;
            mode.SelectionChanged+=delegate{settings.Data["quotaFull"]=mode.SelectedIndex==1;RenderQuota();QueueSave();};
            foreach(string provider in QuotaSession.Providers){string name=provider;var control=Control<CheckBox>("Quota"+name);control.IsChecked=settings.Flag("quota"+name,false);quotas.Enable(name,control.IsChecked==true);
                control.Click+=delegate{settings.Data["quota"+name]=control.IsChecked==true;quotas.Enable(name,control.IsChecked==true);if(!isolated)quotas.Tick(DateTimeOffset.UtcNow);RenderQuota();UpdateDesktop();QueueSave();};
            }
            Click("QuotaRefresh",delegate{quotas.Refresh();if(!isolated)quotas.Tick(DateTimeOffset.UtcNow);RenderQuota();});
        }
        void CopyClaudeStatusLineCommand(bool powershell){
            try{Clipboard.SetText(ClaudeStatusLineCommand.Create(paths.Exe,powershell));Text("ClaudeSnapshotSetupStatus",language.T("Command copied. Set it as Claude Code statusLine.command; preserve any existing command."));}
            catch(System.Runtime.InteropServices.ExternalException){Text("ClaudeSnapshotSetupStatus",language.T("Clipboard busy; try again."));}
        }
        QuotaReading ReadQuota(string provider,System.Threading.CancellationToken token){token.ThrowIfCancellationRequested();return provider=="Claude"&&claudeSnapshotSource?ClaudeStatusLineReceiver.Read(paths.State,DateTimeOffset.UtcNow):QuotaProviders.Read(provider,token);}
        bool QuotaFresh(QuotaReading r){var age=DateTimeOffset.UtcNow-r.Observed;return (r.Status=="Live"||r.Status=="CLI snapshot"||r.Status=="Cached")&&age>=TimeSpan.Zero&&age<TimeSpan.FromMinutes(10);}
        string QuotaState(QuotaReading r){if((r.Status=="Live"||r.Status=="CLI snapshot")&&!QuotaFresh(r))return language.T("Quota stale");return language.T(r.Status);}
        bool QuotaWindowVisible(QuotaReading r,QuotaWindow w){return QuotaFresh(r)&&w.Remaining.HasValue&&(r.Source!="CLI snapshot"||w.Reset>DateTimeOffset.UtcNow)&&(r.Status!="Cached"||!w.Reset.HasValue||w.Reset>DateTimeOffset.UtcNow);}
        string QuotaValue(QuotaReading r,QuotaWindow w){return QuotaWindowVisible(r,w)?w.Remaining.Value.ToString("0.#")+"% "+language.T("left"):"—";}
        QuotaReading DisplayQuota(QuotaReading original,bool full,DateTimeOffset now){
            var cached=quotas.CachedReading(original.Provider,now);
            if(cached!=null&&!isolated&&!QuotaProviders.IsClaudeCacheScopeCurrent(cached.CacheScope))cached=null;
            var selected=cached??original;
            if(cached==null&&original.Provider=="Claude"&&original.Status=="Refresh rate limited")return new QuotaReading{Provider=original.Provider,Status=original.Status,Observed=original.Observed,Windows=new List<QuotaWindow>{new QuotaWindow{Label="5-hour"},new QuotaWindow{Label="Weekly"}}};
            if(cached==null&&(!full||selected.AllWindows.Count==0))return selected;
            return new QuotaReading{Provider=selected.Provider,Source=selected.Source,Observed=selected.Observed,Status=cached!=null?"Cached":selected.Status=="Quota unavailable"&&selected.AllWindows.Any(w=>w.Remaining.HasValue)?"Live":selected.Status,Windows=full&&selected.AllWindows.Count>0?selected.AllWindows:selected.Windows};
        }
        string QuotaRecoveryState(QuotaReading original,QuotaReading displayed,DateTimeOffset now){
            var refresh=quotas.GetState(original.Provider);bool healthy=(displayed.Status=="Live"||displayed.Status=="CLI snapshot")&&!refresh.TimedOut;
            string state=QuotaState(healthy?displayed:original);
            if(!healthy){
                if(refresh.TimedOut||original.FailureKind=="Request timeout")state=language.T("Refresh timed out");
                if(refresh.Refreshing)state+=" · "+language.T(refresh.TimedOut?"Waiting for request to stop":"Refreshing quota…");
                else if(refresh.NextAttempt.HasValue)state+=" · "+string.Format(language.T("Retry in {0} min"),Math.Max(1,(int)Math.Ceiling((refresh.NextAttempt.Value-now).TotalMinutes)));
                if(displayed.Status=="Cached")state+=" · "+language.T("Cached")+" "+Math.Max(0,(int)(now-displayed.Observed).TotalMinutes)+" "+language.T("min ago");
            }else if(displayed.Observed!=default(DateTimeOffset))state+=" · "+Math.Max(0,(int)(now-displayed.Observed).TotalMinutes)+" "+language.T("min ago");
            return state;
        }
        string QuotaDetails(QuotaReading original,QuotaReading displayed,DateTimeOffset now){
            string source=original.Provider=="Antigravity"&&original.Source=="CLI"?language.T("Source: Antigravity CLI"):null;
            var refresh=quotas.GetState(original.Provider);if((displayed.Status=="Live"||displayed.Status=="CLI snapshot")&&!refresh.TimedOut)return source;
            var lines=new List<string>{QuotaRecoveryState(original,displayed,now)};
            if(source!=null)lines.Add(source);
            if(refresh.LastSuccess.HasValue)lines.Add(language.T("Last successful update")+": "+refresh.LastSuccess.Value.ToLocalTime().ToString("g"));
            if(original.Observed!=default(DateTimeOffset))lines.Add(language.T("Last attempt")+": "+original.Observed.ToLocalTime().ToString("g"));
            if(!refresh.Refreshing&&refresh.NextAttempt.HasValue)lines.Add(language.T("Next retry")+": "+refresh.NextAttempt.Value.ToLocalTime().ToString("g"));
            if(original.HttpStatus>=100&&original.HttpStatus<=599)lines.Add("HTTP "+original.HttpStatus);
            if(!string.IsNullOrEmpty(original.FailureKind))lines.Add(language.T(original.FailureKind));
            return string.Join("\n",lines);
        }
        void RenderQuota(){
            var readings=quotas.Readings;var now=DateTimeOffset.UtcNow;
            var panel=(ResponsivePanel)Control<StackPanel>("QuotaCards");if(panel.Dragging)return;
            bool full=settings.Flag("quotaFull");var order=QuotaOrder();
            var displayed=readings.ToDictionary(r=>r.Provider,r=>DisplayQuota(r,full,now));
            string signature=language.Preference+"|"+light+"|"+locked+"|"+full+"|"+SystemParameters.HighContrast+"|"+Window.FontSize+"|"+settings.Flag("unifiedReadingColors")+ReadingColor()+"|"+string.Join(";",readings.Select(r=>r.Provider+r.Status+r.Source+r.Observed.ToString("o")+QuotaRecoveryState(r,displayed[r.Provider],now)+QuotaDetails(r,displayed[r.Provider],now)+string.Join(",",displayed[r.Provider].Windows.Select(w=>w.Label+w.Remaining+w.Reset+QuotaWindowVisible(displayed[r.Provider],w)))));
            if(signature==quotaSignature)return;quotaSignature=signature;panel.Children.Clear();
            foreach(var original in readings.OrderBy(r=>Array.IndexOf(order,r.Provider))){
                var reading=displayed[original.Provider];
                var body=new StackPanel();var foreground=SystemParameters.HighContrast?SystemColors.WindowTextBrush:Brush(light?"#17202B":"#F0F5FA");
                string accent=SystemParameters.HighContrast?SystemColors.WindowTextColor.ToString():light?"#17202B":settings.Flag("unifiedReadingColors")?ReadingColor():reading.Provider=="Codex"?"#A5E7D5":reading.Provider=="Claude"?"#E7B497":"#A7CBFF";
                var title=new Grid();title.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});title.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});title.ColumnDefinitions.Add(new ColumnDefinition());
                var grip=new Thumb{Width=16,Height=24,Margin=new Thickness(0,0,6,0),Cursor=Cursors.SizeAll,Focusable=true,Foreground=foreground,IsEnabled=!locked,Template=views["CPU"].Grip.Template,ToolTip=language.T("Drag To Reorder (Esc To Cancel)")};title.Children.Add(grip);
                System.Windows.Automation.AutomationProperties.SetName(grip,reading.Provider+" · "+language.T("Reading Order"));
                var icon=Icon(reading.Provider.ToLowerInvariant(),19,accent);icon.Margin=new Thickness(0,0,8,0);Grid.SetColumn(icon,1);title.Children.Add(icon);
                var name=new TextBlock{Text=reading.Provider=="Antigravity"&&!full?"Antigravity · Gemini":reading.Provider,FontSize=13*Window.FontSize/12,FontWeight=FontWeights.SemiBold,Foreground=foreground,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(name,2);title.Children.Add(name);body.Children.Add(title);
                string state=QuotaRecoveryState(original,reading,now);
                body.Children.Add(new TextBlock{Text=state,ToolTip=QuotaDetails(original,reading,now),Foreground=foreground,Opacity=.8,Margin=new Thickness(0,5,0,8),TextWrapping=TextWrapping.Wrap});
                foreach(var window in reading.Windows){
                    var row=new Grid{Margin=new Thickness(0,6,0,3)};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
                    row.Children.Add(new TextBlock{Text=language.T(window.Label),Foreground=foreground,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,8,0)});
                    var value=new TextBlock{Text=QuotaValue(reading,window),Foreground=foreground,FontWeight=FontWeights.SemiBold};Grid.SetColumn(value,1);row.Children.Add(value);body.Children.Add(row);
                    if(QuotaWindowVisible(reading,window))body.Children.Add(new ProgressBar{Minimum=0,Maximum=100,Value=window.Remaining.Value,Height=3,Foreground=Brush(accent),Background=Brush("#304F6B7C"),IsHitTestVisible=false});
                    string reset=QuotaDecoder.ResetText(window.Reset,now);if(reset!="")body.Children.Add(new TextBlock{Text=reset=="Reset pending"?language.T(reset):reset.Replace("Reset ",language.T("Reset")+" "),Foreground=foreground,Opacity=.75,Margin=new Thickness(0,3,0,5)});
                }
                var card=new Border{Tag=reading.Provider,Child=body,Padding=new Thickness(12),Margin=new Thickness(0,8,0,0),CornerRadius=new CornerRadius(16),BorderThickness=new Thickness(1),BorderBrush=Brush("#608BA8B8"),Background=SystemParameters.HighContrast?SystemColors.WindowBrush:Brush(light?"#20FFFFFF":"#20122029")};panel.Children.Add(card);
                panel.Attach(card,grip,Control<ScrollViewer>("CardScroll"),delegate{settings.Data["quotaCardOrder"]=panel.Children.Cast<Border>().Select(c=>(string)c.Tag).Concat(QuotaOrder()).Distinct().ToArray();QueueSave();});
            }
        }
        void AddDesktopQuotas(List<DesktopMetric> metrics){var now=DateTimeOffset.UtcNow;foreach(var original in quotas.Readings){
            var reading=DisplayQuota(original,false,now);string source=QuotaDetails(original,reading,now);
            if(reading.Windows.Count==0||!QuotaFresh(reading)){metrics.Add(new DesktopMetric("quota"+reading.Provider,reading.Provider,QuotaState(reading),reading.Provider.ToLowerInvariant()){ToolTip=source});continue;}
            int index=0;foreach(var window in reading.Windows)metrics.Add(new DesktopMetric("quota"+reading.Provider+(index++),(reading.Provider=="Antigravity"?"Gemini":reading.Provider+(reading.Source=="CLI snapshot"?" ("+language.T("CLI snapshot")+")":""))+" · "+language.T(window.Label),QuotaValue(reading,window)+(reading.Status=="Cached"?" · "+language.T("Cached"):""),reading.Provider.ToLowerInvariant()){ToolTip=source});
        }}
    }
}
