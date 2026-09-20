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
                try{Clipboard.SetText("\""+paths.Exe.Replace('\\','/')+"\" --claude-statusline");Text("ClaudeSnapshotSetupStatus",language.T("Command copied. Set it as Claude Code statusLine.command; preserve any existing command."));}
                catch(System.Runtime.InteropServices.ExternalException){Text("ClaudeSnapshotSetupStatus",language.T("Clipboard busy; try again."));}
            });
            var mode=Control<ComboBox>("QuotaDisplay");mode.SelectedIndex=settings.Flag("quotaFull")?1:0;
            mode.SelectionChanged+=delegate{settings.Data["quotaFull"]=mode.SelectedIndex==1;RenderQuota();QueueSave();};
            foreach(string provider in QuotaSession.Providers){string name=provider;var control=Control<CheckBox>("Quota"+name);control.IsChecked=settings.Flag("quota"+name,false);quotas.Enable(name,control.IsChecked==true);
                control.Click+=delegate{settings.Data["quota"+name]=control.IsChecked==true;quotas.Enable(name,control.IsChecked==true);if(!isolated)quotas.Tick(DateTimeOffset.UtcNow);RenderQuota();UpdateDesktop();QueueSave();};
            }
            Click("QuotaRefresh",delegate{quotas.Refresh();if(!isolated)quotas.Tick(DateTimeOffset.UtcNow);RenderQuota();});
        }
        QuotaReading ReadQuota(string provider,System.Threading.CancellationToken token){token.ThrowIfCancellationRequested();return provider=="Claude"&&claudeSnapshotSource?ClaudeStatusLineReceiver.Read(paths.State,DateTimeOffset.UtcNow):QuotaProviders.Read(provider,token);}
        bool QuotaFresh(QuotaReading r){var age=DateTimeOffset.UtcNow-r.Observed;return (r.Status=="Live"||r.Status=="CLI snapshot")&&age>=TimeSpan.Zero&&age<TimeSpan.FromMinutes(10);}
        string QuotaState(QuotaReading r){if((r.Status=="Live"||r.Status=="CLI snapshot")&&!QuotaFresh(r))return language.T("Quota stale");return language.T(r.Status);}
        bool QuotaWindowVisible(QuotaReading r,QuotaWindow w){return QuotaFresh(r)&&w.Remaining.HasValue&&(r.Source!="CLI snapshot"||w.Reset>DateTimeOffset.UtcNow);}
        string QuotaValue(QuotaReading r,QuotaWindow w){return QuotaWindowVisible(r,w)?w.Remaining.Value.ToString("0.#")+"% "+language.T("left"):"—";}
        void RenderQuota(){
            var readings=quotas.Readings;var now=DateTimeOffset.UtcNow;
            var panel=(ResponsivePanel)Control<StackPanel>("QuotaCards");if(panel.Dragging)return;
            bool full=settings.Flag("quotaFull");var order=QuotaOrder();
            string signature=language.Preference+"|"+light+"|"+locked+"|"+full+"|"+SystemParameters.HighContrast+"|"+Window.FontSize+"|"+settings.Flag("unifiedReadingColors")+ReadingColor()+"|"+string.Join(";",readings.Select(r=>r.Provider+r.Status+r.Source+r.Observed.ToString("o")+(int)(now-r.Observed).TotalMinutes+string.Join(",",r.Windows.Concat(r.AllWindows).Select(w=>w.Label+w.Remaining+w.Reset+QuotaWindowVisible(r,w)))));
            if(signature==quotaSignature)return;quotaSignature=signature;panel.Children.Clear();
            foreach(var original in readings.OrderBy(r=>Array.IndexOf(order,r.Provider))){
                var reading=full&&original.AllWindows.Count>0?new QuotaReading{Provider=original.Provider,Source=original.Source,Observed=original.Observed,Status=original.Status=="Quota unavailable"&&original.AllWindows.Any(w=>w.Remaining.HasValue)?"Live":original.Status,Windows=original.AllWindows}:original;
                var body=new StackPanel();var foreground=SystemParameters.HighContrast?SystemColors.WindowTextBrush:Brush(light?"#17202B":"#F0F5FA");
                string accent=SystemParameters.HighContrast?SystemColors.WindowTextColor.ToString():light?"#17202B":settings.Flag("unifiedReadingColors")?ReadingColor():reading.Provider=="Codex"?"#A5E7D5":reading.Provider=="Claude"?"#E7B497":"#A7CBFF";
                var title=new Grid();title.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});title.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});title.ColumnDefinitions.Add(new ColumnDefinition());
                var grip=new Thumb{Width=16,Height=24,Margin=new Thickness(0,0,6,0),Cursor=Cursors.SizeAll,Focusable=true,Foreground=foreground,IsEnabled=!locked,Template=views["CPU"].Grip.Template,ToolTip=language.T("Drag To Reorder (Esc To Cancel)")};title.Children.Add(grip);
                System.Windows.Automation.AutomationProperties.SetName(grip,reading.Provider+" · "+language.T("Reading Order"));
                var icon=Icon(reading.Provider.ToLowerInvariant(),19,accent);icon.Margin=new Thickness(0,0,8,0);Grid.SetColumn(icon,1);title.Children.Add(icon);
                var name=new TextBlock{Text=reading.Provider=="Antigravity"&&!full?"Antigravity · Gemini":reading.Provider,FontSize=13*Window.FontSize/12,FontWeight=FontWeights.SemiBold,Foreground=foreground,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(name,2);title.Children.Add(name);body.Children.Add(title);
                string state=QuotaState(reading);if(reading.Source=="CLI")state+=" · CLI";if(reading.Observed!=default(DateTimeOffset))state+=" · "+Math.Max(0,(int)(now-reading.Observed).TotalMinutes)+" "+language.T("min ago");
                body.Children.Add(new TextBlock{Text=state,Foreground=foreground,Opacity=.8,Margin=new Thickness(0,5,0,8),TextWrapping=TextWrapping.Wrap});
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
        void AddDesktopQuotas(List<DesktopMetric> metrics){foreach(var reading in quotas.Readings){
            if(reading.Windows.Count==0||!QuotaFresh(reading)){metrics.Add(new DesktopMetric("quota"+reading.Provider,reading.Provider,QuotaState(reading),reading.Provider.ToLowerInvariant()));continue;}
            int index=0;foreach(var window in reading.Windows)metrics.Add(new DesktopMetric("quota"+reading.Provider+(index++),(reading.Provider=="Antigravity"?"Gemini"+(reading.Source=="CLI"?" (CLI)":""):reading.Provider+(reading.Source=="CLI snapshot"?" ("+language.T("CLI snapshot")+")":""))+" · "+language.T(window.Label),QuotaValue(reading,window),reading.Provider.ToLowerInvariant()));
        }}
    }
}
