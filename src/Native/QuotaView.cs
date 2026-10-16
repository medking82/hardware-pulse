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
        volatile bool claudeCliSource;
        string quotaSignature;
        string quotaReadySignature;
        string deepSeekKeyStatus;
        bool argonSeen;
        const string ArgonWeekly="gemini-argon-wtu-weekly";
        string QuotaCaption(string label){return string.Join(" · ",(label??"").Split(new[]{" · "},StringSplitOptions.None).Select(language.Label));}
        void SetDeepSeekKeyStatus(string key){deepSeekKeyStatus=key;Text("DeepSeekKeyStatus",language.Label(key));}
        void LocalizeDeepSeekKeyStatus(){if(deepSeekKeyStatus!=null)Text("DeepSeekKeyStatus",language.Label(deepSeekKeyStatus));}
        DateTimeOffset nextClaudeRevisionCheck;
        readonly Dictionary<string,QuotaReading> quotaRefreshBefore=new Dictionary<string,QuotaReading>();
        Brush AiCardBackground(){return SystemParameters.HighContrast?SystemColors.WindowBrush:Brush(light?"#20FFFFFF":"#20122029");}
        void StyleAiCard(Border card){card.Padding=new Thickness(12);card.Margin=new Thickness(0,8,0,0);card.CornerRadius=new CornerRadius(16);card.BorderThickness=new Thickness(1);card.BorderBrush=SystemParameters.HighContrast?SystemColors.WindowTextBrush:Brush("#608BA8B8");card.Background=AiCardBackground();}
        Thumb AiCardGrip(string title){
            var grip=new Thumb{Width=16,Height=24,Margin=new Thickness(0,0,6,0),Cursor=Cursors.SizeAll,Focusable=true,IsEnabled=!locked,Template=views["CPU"].Grip.Template,ToolTip=language.T("Drag To Reorder (Esc To Cancel)")};
            System.Windows.Automation.AutomationProperties.SetName(grip,title+" · "+language.T("Reading Order"));return grip;
        }
        string[] QuotaOrder(){return settings.Order("quotaCardOrder",QuotaSession.Providers.Concat(new[]{"localModelSpeed"}));}
        void SaveQuotaOrder(){var panel=Control<StackPanel>("QuotaCards");settings.Data["quotaCardOrder"]=panel.Children.Cast<Border>().Select(c=>(string)c.Tag).Concat(QuotaOrder()).Distinct().ToArray();QueueSave();}
        string DeepSeekCurrency(){string value=settings.Text("deepSeekCurrency","CNY");return value=="USD"||value=="All"?value:"CNY";}
        void WireQuota(){
            var desktopVisible=settings.Map("desktopVisible");
            if(!desktopVisible.ContainsKey("quotaAntigravityArgonWeekly")){
                object sessionVisible,weeklyVisible;
                bool hidden=desktopVisible.TryGetValue("quotaAntigravity0",out sessionVisible)&&sessionVisible is bool&&!(bool)sessionVisible&&desktopVisible.TryGetValue("quotaAntigravity1",out weeklyVisible)&&weeklyVisible is bool&&!(bool)weeklyVisible;
                desktopVisible["quotaAntigravityArgonWeekly"]=!hidden;
            }
            var source=Control<ComboBox>("ClaudeQuotaSource");source.SelectedIndex=claudeCliSource?2:claudeSnapshotSource?1:0;
            Control<StackPanel>("ClaudeSnapshotTools").Visibility=claudeSnapshotSource?Visibility.Visible:Visibility.Collapsed;
            Control<TextBlock>("ClaudeQueryHelp").Visibility=claudeCliSource?Visibility.Visible:Visibility.Collapsed;
            source.SelectionChanged+=delegate{
                quotas.Enable("Claude",false,claudeSnapshotSource);claudeSnapshotSource=source.SelectedIndex==1;claudeCliSource=source.SelectedIndex==2;
                settings.Data["claudeSnapshotSource"]=claudeSnapshotSource;
                settings.Data["claudeCliSource"]=claudeCliSource;quotaRefreshBefore.Remove("Claude");
                Control<StackPanel>("ClaudeSnapshotTools").Visibility=claudeSnapshotSource?Visibility.Visible:Visibility.Collapsed;
                Control<TextBlock>("ClaudeQueryHelp").Visibility=claudeCliSource?Visibility.Visible:Visibility.Collapsed;
                quotas.Enable("Claude",Checked("QuotaClaude"),claudeSnapshotSource);if(!isolated)quotas.Tick(DateTimeOffset.UtcNow);
                RenderQuota();UpdateDesktop();QueueSave();
            };
            Click("ClaudeSnapshotRebind",delegate{
                if(ClaudeStatusLineReceiver.Reset(paths.State)){
                    quotas.Enable("Claude",false,claudeSnapshotSource);quotas.Enable("Claude",Checked("QuotaClaude"),claudeSnapshotSource);
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
            var currency=Control<ComboBox>("DeepSeekCurrency");
            currency.SelectedItem=currency.Items.Cast<ComboBoxItem>().Single(item=>(string)item.Tag==DeepSeekCurrency());
            currency.SelectionChanged+=delegate{
                var selected=currency.SelectedItem as ComboBoxItem;if(selected==null)return;
                settings.Data["deepSeekCurrency"]=(string)selected.Tag;RenderQuota();UpdateDesktop();QueueSave();
            };
            foreach(string provider in QuotaSession.Providers){string name=provider;var control=Control<CheckBox>("Quota"+name);control.IsChecked=settings.Flag("quota"+name,false);quotas.Enable(name,control.IsChecked==true,name=="Claude"&&claudeSnapshotSource);
                control.Click+=delegate{settings.Data["quota"+name]=control.IsChecked==true;quotas.Enable(name,control.IsChecked==true,name=="Claude"&&claudeSnapshotSource);if(!isolated)quotas.Tick(DateTimeOffset.UtcNow);RenderQuota();UpdateDesktop();QueueSave();};
            }
            // DeepSeek needs an API key instead of a local login; the editor is the only path that
            // writes it, and it never leaves this machine except to the fixed provider endpoint.
            var deepSeekKey=Control<PasswordBox>("DeepSeekKey");
            SetDeepSeekKeyStatus(ProtectedQuotaCredentials.Exists("DeepSeek")?"Encrypted key saved":"API key required");
            Click("DeepSeekKeySave",delegate{
                var save=Control<Button>("DeepSeekKeySave");var remove=Control<Button>("DeepSeekKeyRemove");
                save.IsEnabled=false;remove.IsEnabled=false;string value=deepSeekKey.Password;deepSeekKey.Clear();
                if(!ProtectedQuotaCredentials.Valid(value))SetDeepSeekKeyStatus("Enter a valid API key. The previous key was not changed.");
                else{
                    try{ProtectedQuotaCredentials.Save("DeepSeek",value);quotas.CredentialsChanged("DeepSeek",true);quotaRefreshBefore.Remove("DeepSeek");SetDeepSeekKeyStatus("Encrypted key saved. Refresh quota to check the account.");}
                    catch{SetDeepSeekKeyStatus("Could not store the key for this Windows user.");}
                }
                save.IsEnabled=true;remove.IsEnabled=true;RenderQuota();UpdateDesktop();QueueSave();
            });
            Click("DeepSeekKeyRemove",delegate{
                deepSeekKey.Clear();
                try{ProtectedQuotaCredentials.Remove("DeepSeek");quotas.CredentialsChanged("DeepSeek",false);quotaRefreshBefore.Remove("DeepSeek");SetDeepSeekKeyStatus("API key required");}
                catch{SetDeepSeekKeyStatus("Could not remove the key. The stored key was not changed.");}
                RenderQuota();UpdateDesktop();QueueSave();
            });
            Click("QuotaRefresh",delegate{RefreshQuota(null);});
        }
        void RefreshQuota(string provider){
            var now=DateTimeOffset.UtcNow;quotaRefreshBefore.Clear();
            foreach(var reading in quotas.Readings){if(provider!=null&&reading.Provider!=provider)continue;quotaRefreshBefore[reading.Provider]=reading;quotas.RequestRefresh(reading.Provider,now);}
            if(quotaRefreshBefore.Count==0){Text("QuotaRefreshStatus",language.T("Enable quota in Settings first"));Control<TextBlock>("QuotaRefreshStatus").Visibility=Visibility.Visible;}
            if(!isolated)quotas.Tick(now);
            RenderQuota();UpdateDesktop();
        }
        void TickQuotas(DateTimeOffset now){CheckClaudeLoginRecovery(now,QuotaProviders.IsClaudeCacheScopeCurrent);quotas.Tick(now);}
        void CheckClaudeLoginRecovery(DateTimeOffset now,Func<string,bool> isCurrent){
            if(now<nextClaudeRevisionCheck)return;nextClaudeRevisionCheck=now.AddSeconds(30);
            var reading=quotas.Readings.FirstOrDefault(r=>r.Provider=="Claude");
            if(reading!=null&&reading.Status=="Login required"&&!claudeSnapshotSource&&!claudeCliSource&&!string.IsNullOrEmpty(reading.CacheScope)&&!quotas.GetState("Claude").Refreshing&&!isCurrent(reading.CacheScope))quotas.RecoverClaudeLogin(now);
        }
        void RenderQuotaRefreshFeedback(DateTimeOffset now){
            var output=Control<TextBlock>("QuotaRefreshStatus");if(quotaRefreshBefore.Count==0)return;
            var lines=new List<string>();
            foreach(var pair in quotaRefreshBefore){
                var reading=quotas.Readings.FirstOrDefault(r=>r.Provider==pair.Key);var refresh=quotas.GetState(pair.Key);
                string state;
                if(reading==null)state=language.Label("Off");
                else if(refresh.Refreshing)state=refresh.TimedOut?language.Label("Refresh timed out")+" · "+language.Label("Waiting for request to stop"):language.Label("Refreshing quota…");
                else if(!object.ReferenceEquals(reading,pair.Value))state=reading.Status=="Live"||reading.Status=="CLI snapshot"?language.Label("Quota refresh complete"):QuotaRecoveryState(reading,reading,now);
                else if(refresh.NextAttempt.HasValue&&refresh.NextAttempt.Value>now)state=string.Format(language.Label("Refresh deferred until {0}"),refresh.NextAttempt.Value.ToLocalTime().ToString("HH:mm:ss"))+" · "+language.Label(reading.Status);
                else state=language.Label("Refresh pending");
                lines.Add(pair.Key+": "+state);
            }
            output.Text=string.Join("\n",lines);output.Visibility=Visibility.Visible;
        }
        void CopyClaudeStatusLineCommand(bool powershell){
            try{Clipboard.SetText(ClaudeStatusLineCommand.Create(paths.Exe,powershell));Text("ClaudeSnapshotSetupStatus",language.T("Command copied. Set it as Claude Code statusLine.command; preserve any existing command."));}
            catch(System.Runtime.InteropServices.ExternalException){Text("ClaudeSnapshotSetupStatus",language.T("Clipboard busy; try again."));}
        }
        QuotaReading ReadQuota(string provider,System.Threading.CancellationToken token){token.ThrowIfCancellationRequested();return provider=="Claude"&&claudeSnapshotSource?ClaudeStatusLineReceiver.Read(paths.State,DateTimeOffset.UtcNow):provider=="Claude"&&claudeCliSource?QuotaProviders.ReadClaudeCli(token):QuotaProviders.Read(provider,token);}
        bool QuotaFresh(QuotaReading r){var age=DateTimeOffset.UtcNow-r.Observed;return (r.Status=="Live"||r.Status=="CLI snapshot"||r.Status=="Cached")&&age>=TimeSpan.Zero&&age<TimeSpan.FromMinutes(10);}
        string QuotaState(QuotaReading r){if((r.Status=="Live"||r.Status=="CLI snapshot")&&!QuotaFresh(r))return language.Label("Quota stale");return language.Label(r.Status);}
        bool QuotaWindowVisible(QuotaReading r,QuotaWindow w){return QuotaFresh(r)&&w.Remaining.HasValue&&(r.Source!="CLI snapshot"||w.Reset>DateTimeOffset.UtcNow)&&(r.Status!="Cached"||!w.Reset.HasValue||w.Reset>DateTimeOffset.UtcNow);}
        string QuotaValue(QuotaReading r,QuotaWindow w){return QuotaWindowVisible(r,w)?w.Remaining.Value.ToString("0.#")+"% "+language.Label("left"):"—";}
        // A money provider shows the amount or an explicit blank; it never invents a percentage.
        string MoneyValue(QuotaReading r,QuotaBalance b){return QuotaFresh(r)&&!string.IsNullOrEmpty(b.Amount)?b.Currency+" "+b.Amount:"—";}
        string MoneyEmptyValue(QuotaReading reading){return !QuotaFresh(reading)?QuotaState(reading):DeepSeekCurrency()=="All"?language.T("No Balance"):string.Format(language.T("No {0} Balance"),DeepSeekCurrency());}
        QuotaReading DisplayQuota(QuotaReading original,bool full,DateTimeOffset now){
            if(original.Provider=="Antigravity"){
                var windows=(full&&original.AllWindows.Count>0?original.AllWindows:original.Windows).ToList();
                argonSeen=argonSeen||windows.Any(w=>w.Id==ArgonWeekly);
                if(argonSeen&&!windows.Any(w=>w.Id==ArgonWeekly))windows.Add(new QuotaWindow{Id=ArgonWeekly,Label=full?"Gemini 4 Argon · Weekly":"Argon · Weekly"});
                string status=full&&original.Status=="Quota unavailable"&&windows.Any(w=>w.Remaining.HasValue)?"Live":original.Status;
                return new QuotaReading{Provider=original.Provider,Status=status,Source=original.Source,Observed=original.Observed,Windows=windows};
            }
            if(QuotaCatalog.IsMoney(original.Provider)){
                var balances=full?original.AllBalances:original.Balances;string currency=DeepSeekCurrency();
                return new QuotaReading{Provider=original.Provider,Status=original.Status,Source=original.Source,Observed=original.Observed,HttpStatus=original.HttpStatus,FailureKind=original.FailureKind,Balances=balances.Where(b=>original.Provider!="DeepSeek"||currency=="All"||b.Currency==currency).ToList(),AllBalances=original.AllBalances};
            }
            var cached=quotas.CachedReading(original.Provider,now);
            if(cached!=null&&!isolated&&!QuotaProviders.IsClaudeCacheScopeCurrent(cached.CacheScope))cached=null;
            var selected=cached??original;
            if(cached==null&&original.Provider=="Claude"&&original.Status=="Refresh rate limited")return new QuotaReading{Provider=original.Provider,Status=original.Status,Observed=original.Observed,Windows=new List<QuotaWindow>{new QuotaWindow{Label="5-hour"},new QuotaWindow{Label="Weekly"}}};
            if(cached==null&&(!full||selected.AllWindows.Count==0))return selected;
            return new QuotaReading{Provider=selected.Provider,Source=selected.Source,Observed=selected.Observed,Status=cached!=null?"Cached":selected.Status=="Quota unavailable"&&selected.AllWindows.Any(w=>w.Remaining.HasValue)?"Live":selected.Status,Windows=full&&selected.AllWindows.Count>0?selected.AllWindows:selected.Windows};
        }
        string QuotaRecoveryState(QuotaReading original,QuotaReading displayed,DateTimeOffset now){
            var refresh=quotas.GetState(original.Provider);bool healthy=(displayed.Status=="Live"||displayed.Status=="CLI snapshot")&&!refresh.TimedOut;
            string state=original.Provider=="Claude"&&original.Status=="Login required"?language.Label("Last quota login check failed"):QuotaState(healthy?displayed:original);
            if(refresh.TimedOut||original.FailureKind=="Request timeout")state=language.Label("Refresh timed out");
            if(refresh.Refreshing)return state+" · "+language.Label(refresh.TimedOut?"Waiting for request to stop":"Refreshing quota…");
            QuotaReading before;
            if(healthy&&quotaRefreshBefore.TryGetValue(original.Provider,out before)&&object.ReferenceEquals(original,before)&&refresh.NextAttempt.HasValue&&refresh.NextAttempt.Value>now)return state+" · "+string.Format(language.Label("Refresh deferred until {0}"),refresh.NextAttempt.Value.ToLocalTime().ToString("HH:mm:ss"));
            if(!healthy){
                if(refresh.NextAttempt.HasValue)state+=" · "+string.Format(language.Label("Retry in {0} min"),Math.Max(1,(int)Math.Ceiling((refresh.NextAttempt.Value-now).TotalMinutes)));
                if(displayed.Status=="Cached")state+=" · "+language.T("Cached")+" "+Math.Max(0,(int)(now-displayed.Observed).TotalMinutes)+" "+language.Label("min ago");
            }else if(displayed.Observed!=default(DateTimeOffset))state+=" · "+Math.Max(0,(int)(now-displayed.Observed).TotalMinutes)+" "+language.Label("min ago");
            return state;
        }
        string QuotaDetails(QuotaReading original,QuotaReading displayed,DateTimeOffset now){
            string source=original.Source=="CLI"?original.Provider=="Antigravity"?language.T("Source: Antigravity CLI"):original.Provider=="Claude"?language.T("Source: Claude Code /usage"):null:null;
            var refresh=quotas.GetState(original.Provider);if((displayed.Status=="Live"||displayed.Status=="CLI snapshot")&&!refresh.TimedOut)return source;
            var lines=new List<string>{QuotaRecoveryState(original,displayed,now)};
            if(source!=null)lines.Add(source);
            if(refresh.LastSuccess.HasValue)lines.Add(language.Label("Last successful update")+": "+refresh.LastSuccess.Value.ToLocalTime().ToString("g"));
            if(original.Observed!=default(DateTimeOffset))lines.Add(language.Label("Last attempt")+": "+original.Observed.ToLocalTime().ToString("g"));
            if(!refresh.Refreshing&&refresh.NextAttempt.HasValue)lines.Add(language.Label("Next retry")+": "+refresh.NextAttempt.Value.ToLocalTime().ToString("g"));
            if(original.HttpStatus>=100&&original.HttpStatus<=599)lines.Add("HTTP "+original.HttpStatus);
            if(!string.IsNullOrEmpty(original.FailureKind))lines.Add(language.T(original.FailureKind));
            return string.Join("\n",lines);
        }
        void RenderQuota(){
            var readings=quotas.Readings;var now=DateTimeOffset.UtcNow;RenderQuotaRefreshFeedback(now);
            var panel=(ResponsivePanel)Control<StackPanel>("QuotaCards");if(panel.Dragging)return;
            bool full=settings.Flag("quotaFull");var order=QuotaOrder();
            var displayed=readings.ToDictionary(r=>r.Provider,r=>DisplayQuota(r,full,now));
            string signature=language.Preference+"|"+light+"|"+locked+"|"+full+"|"+DeepSeekCurrency()+"|"+SystemParameters.HighContrast+"|"+Window.FontSize+"|"+settings.Flag("unifiedReadingColors")+ReadingColor()+"|"+string.Join(";",readings.Select(r=>r.Provider+r.Status+r.Source+r.Observed.ToString("o")+QuotaRecoveryState(r,displayed[r.Provider],now)+QuotaDetails(r,displayed[r.Provider],now)+string.Join(",",displayed[r.Provider].Windows.Select(w=>w.Label+w.Remaining+w.Reset+QuotaWindowVisible(displayed[r.Provider],w)))+string.Join(",",displayed[r.Provider].Balances.Select(b=>b.Label+b.Currency+b.Amount))));
            string readySignature=string.Join(",",readings.Select(r=>quotas.CanRequestRefresh(r.Provider,now)));
            if(signature==quotaSignature&&readySignature==quotaReadySignature)return;quotaSignature=signature;quotaReadySignature=readySignature;panel.Children.Clear();
            foreach(var original in readings.OrderBy(r=>Array.IndexOf(order,r.Provider))){
                var reading=displayed[original.Provider];
                var body=new StackPanel();var foreground=SystemParameters.HighContrast?SystemColors.WindowTextBrush:Brush(light?"#17202B":"#F0F5FA");
                string accent=SystemParameters.HighContrast?SystemColors.WindowTextColor.ToString():light?"#17202B":settings.Flag("unifiedReadingColors")?ReadingColor():QuotaCatalog.Accent(reading.Provider);
                var title=new Grid();title.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});title.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});title.ColumnDefinitions.Add(new ColumnDefinition());title.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
                var grip=AiCardGrip(reading.Provider);grip.Foreground=foreground;title.Children.Add(grip);
                var icon=Icon(reading.Provider.ToLowerInvariant(),19,accent);icon.Margin=new Thickness(0,0,8,0);Grid.SetColumn(icon,1);title.Children.Add(icon);
                var name=new TextBlock{Text=reading.Provider=="Antigravity"&&!full?"Antigravity · Gemini":reading.Provider,FontSize=13*Window.FontSize/12,FontWeight=FontWeights.SemiBold,Foreground=foreground,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(name,2);title.Children.Add(name);
                string provider=reading.Provider;bool canRefresh=quotas.CanRequestRefresh(provider,now);
                var refreshButton=new Button{Content="↻",Foreground=foreground,ToolTip=language.T(canRefresh?"Refresh Quota":"Refresh available after the minimum interval or retry deadline."),Padding=new Thickness(5,1,5,1),Margin=new Thickness(6,0,0,0),VerticalAlignment=VerticalAlignment.Center,IsEnabled=canRefresh};
                ToolTipService.SetShowOnDisabled(refreshButton,true);
                System.Windows.Automation.AutomationProperties.SetName(refreshButton,provider+" · "+language.T("Refresh Quota"));refreshButton.Click+=delegate{RefreshQuota(provider);};Grid.SetColumn(refreshButton,3);title.Children.Add(refreshButton);body.Children.Add(title);
                string state=QuotaRecoveryState(original,reading,now);
                body.Children.Add(new TextBlock{Text=state,ToolTip=QuotaDetails(original,reading,now),Foreground=foreground,Opacity=.8,Margin=new Thickness(0,5,0,8),TextWrapping=TextWrapping.Wrap});
                if(QuotaCatalog.IsMoney(reading.Provider)){
                    if(reading.Balances.Count==0)body.Children.Add(new TextBlock{Text=MoneyEmptyValue(reading),Foreground=foreground,TextWrapping=TextWrapping.Wrap});
                    foreach(var balance in reading.Balances){
                        var row=new Grid{Margin=new Thickness(0,6,0,3)};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
                        row.Children.Add(new TextBlock{Text=balance.Label=="Available balance"?language.T("Balance"):language.Label(balance.Label),ToolTip=language.Label(balance.Label),Foreground=foreground,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,8,0)});
                        var value=new TextBlock{Text=MoneyValue(reading,balance),Foreground=foreground,FontWeight=FontWeights.SemiBold};Grid.SetColumn(value,1);row.Children.Add(value);body.Children.Add(row);
                    }
                }else foreach(var window in reading.Windows){
                    if(window.Id==ArgonWeekly){
                        body.Children.Add(new Border{Height=1,Background=Brush("#608BA8B8"),Margin=new Thickness(0,12,0,8)});
                        var heading=new StackPanel{Orientation=Orientation.Horizontal};heading.Children.Add(new TextBlock{Text="Argon",FontWeight=FontWeights.SemiBold,Foreground=foreground});
                        heading.Children.Add(new TextBlock{Text=language.Label("Independent quota"),Foreground=foreground,Opacity=.75,Margin=new Thickness(8,0,0,0),VerticalAlignment=VerticalAlignment.Center});body.Children.Add(heading);
                    }
                    var row=new Grid{Margin=new Thickness(0,6,0,3)};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
                    row.Children.Add(new TextBlock{Text=window.Id==ArgonWeekly?language.Label("Weekly"):QuotaCaption(window.Label),Foreground=foreground,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,8,0)});
                    var value=new TextBlock{Text=QuotaValue(reading,window),Foreground=foreground,FontWeight=FontWeights.SemiBold};Grid.SetColumn(value,1);row.Children.Add(value);body.Children.Add(row);
                    if(QuotaWindowVisible(reading,window))body.Children.Add(new ProgressBar{Minimum=0,Maximum=100,Value=window.Remaining.Value,Height=3,Foreground=Brush(accent),Background=Brush("#304F6B7C"),IsHitTestVisible=false});
                    string reset=QuotaDecoder.ResetText(window.Reset,now);if(reset!="")body.Children.Add(new TextBlock{Text=reset=="Reset pending"?language.T(reset):reset.Replace("Reset ",language.T("Reset")+" "),Foreground=foreground,Opacity=.75,Margin=new Thickness(0,3,0,5)});
                }
                var card=new Border{Tag=reading.Provider,Child=body};StyleAiCard(card);panel.Children.Add(card);
                panel.Attach(card,grip,Control<ScrollViewer>("CardScroll"),SaveQuotaOrder);
            }
            RenderLocalInference();
        }
        void AddDesktopQuotas(List<DesktopMetric> metrics){var now=DateTimeOffset.UtcNow;foreach(var original in quotas.Readings){
            var reading=DisplayQuota(original,false,now);string source=QuotaDetails(original,reading,now);
            if(QuotaCatalog.IsMoney(reading.Provider)){
                if(reading.Balances.Count==0||!QuotaFresh(reading)){metrics.Add(new DesktopMetric("quota"+reading.Provider,reading.Provider,MoneyEmptyValue(reading),reading.Provider.ToLowerInvariant()){ToolTip=source});continue;}
                int position=0;foreach(var balance in reading.Balances)metrics.Add(new DesktopMetric("quota"+reading.Provider+(position++),reading.Provider,balance.Currency+" "+balance.Amount,reading.Provider.ToLowerInvariant()){ToolTip=language.Label(balance.Label)+" · "+balance.Currency+(string.IsNullOrEmpty(source)?"":"\n"+source)});
                continue;
            }
            if(reading.Windows.Count==0||!QuotaFresh(reading)){metrics.Add(new DesktopMetric("quota"+reading.Provider,reading.Provider,QuotaState(reading),reading.Provider.ToLowerInvariant()){ToolTip=source});continue;}
            int index=0;foreach(var window in reading.Windows){
                string key="quota"+reading.Provider+(index++),title=(reading.Provider=="Antigravity"?"Gemini":reading.Provider+(reading.Source=="CLI snapshot"?" ("+language.T("CLI snapshot")+")":""))+" · "+QuotaCaption(window.Label);
                string detail=source;
                if(reading.Provider=="Antigravity"){
                    if(window.Id==ArgonWeekly){key="quotaAntigravityArgonWeekly";title="Argon · "+language.Label("Weekly");detail="Gemini 4 Argon · "+language.Label("Independent quota")+"\n"+QuotaDecoder.ResetText(window.Reset,now)+(string.IsNullOrEmpty(source)?"":"\n"+source);}
                    else if(window.Id=="gemini-5h"||window.Label=="5-hour")key="quotaAntigravity0";
                    else if(window.Id=="gemini-weekly"||window.Label=="Weekly")key="quotaAntigravity1";
                }
                metrics.Add(new DesktopMetric(key,title,QuotaValue(reading,window)+(reading.Status=="Cached"?" · "+language.T("Cached"):""),reading.Provider.ToLowerInvariant()){ToolTip=detail});
            }
        }}
    }
}
