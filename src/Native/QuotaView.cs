using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace HardwarePulse {
    public sealed partial class Shell {
        readonly QuotaSession quotas;
        readonly ProtectedQuotaCredentials quotaCredentials;
        readonly Dictionary<string,DateTimeOffset> quotaAnimated=new Dictionary<string,DateTimeOffset>();
        string quotaSignature;
        string[] QuotaOrder(){return settings.Order("quotaCardOrder",QuotaSession.Providers);}
        void WireQuota(){
            var mode=Control<ComboBox>("QuotaDisplay");mode.SelectedIndex=settings.Flag("quotaFull")?1:0;
            mode.SelectionChanged+=delegate{settings.Data["quotaFull"]=mode.SelectedIndex==1;RenderQuota();QueueSave();};
            var options=Control<StackPanel>("QuotaProviderOptions");
            foreach(var entry in QuotaCatalog.Entries){string name=entry.Id;var control=new CheckBox{Name="Quota"+name,Content=name,Margin=new Thickness(0,6,0,6)};Window.RegisterName(control.Name,control);options.Children.Add(control);control.IsChecked=settings.Flag("quota"+name,false);quotas.Enable(name,control.IsChecked==true);
                control.Click+=delegate{settings.Data["quota"+name]=control.IsChecked==true;quotas.Enable(name,control.IsChecked==true);if(!isolated)quotas.Tick(DateTimeOffset.UtcNow);RenderQuota();UpdateDesktop();QueueSave();};
            }
            BuildQuotaCredentials();
            Click("QuotaRefresh",delegate{quotas.Refresh();if(!isolated)quotas.Tick(DateTimeOffset.UtcNow);RenderQuota();});
        }
        string QuotaState(QuotaReading r){if(r.Status=="Live"&&DateTimeOffset.UtcNow-r.Observed>TimeSpan.FromMinutes(10))return language.T("Quota stale");return language.T(r.Status);}
        string QuotaValue(QuotaReading r,QuotaWindow w){return QuotaState(r)==language.T("Live")&&w.Remaining.HasValue?w.Remaining.Value.ToString("0.#")+"% "+language.T("left"):"—";}
        void RenderQuota(){
            var readings=quotas.Readings;var now=DateTimeOffset.UtcNow;
            var panel=(ResponsivePanel)Control<StackPanel>("QuotaCards");if(panel.Dragging)return;
            bool full=settings.Flag("quotaFull");var order=QuotaOrder();
            string signature=language.Preference+"|"+light+"|"+locked+"|"+full+"|"+SystemParameters.HighContrast+"|"+Window.FontSize+"|"+settings.Flag("unifiedReadingColors")+ReadingColor()+"|"+string.Join(";",readings.Select(r=>r.Provider+r.Status+r.Observed.ToString("o")+(int)(now-r.Observed).TotalMinutes));
            if(signature==quotaSignature)return;quotaSignature=signature;panel.Children.Clear();
            foreach(var original in readings.OrderBy(r=>Array.IndexOf(order,r.Provider))){
                var reading=full&&original.AllWindows.Count>0?new QuotaReading{Provider=original.Provider,Observed=original.Observed,Status=original.Status=="Quota unavailable"&&original.AllWindows.Any(w=>w.Remaining.HasValue)?"Live":original.Status,Windows=original.AllWindows,Balances=original.Balances}:original;
                var body=new StackPanel();var foreground=SystemParameters.HighContrast?SystemColors.WindowTextBrush:Brush(light?"#17202B":"#F0F5FA");
                var info=QuotaCatalog.Find(reading.Provider);
                string accent=SystemParameters.HighContrast?SystemColors.WindowTextColor.ToString():light?"#17202B":settings.Flag("unifiedReadingColors")?ReadingColor():info.Accent;
                var title=new Grid();title.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});title.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});title.ColumnDefinitions.Add(new ColumnDefinition());
                var grip=new Thumb{Width=16,Height=24,Margin=new Thickness(0,0,6,0),Cursor=Cursors.SizeAll,Focusable=true,Foreground=foreground,IsEnabled=!locked,Template=views["CPU"].Grip.Template,ToolTip=language.T("Drag To Reorder (Esc To Cancel)")};title.Children.Add(grip);
                System.Windows.Automation.AutomationProperties.SetName(grip,reading.Provider+" · "+language.T("Reading Order"));
                var icon=Icon(info.Icon,19,accent);icon.Margin=new Thickness(0,0,8,0);Grid.SetColumn(icon,1);title.Children.Add(icon);
                var name=new TextBlock{Text=reading.Provider=="Antigravity"&&!full?"Antigravity · Gemini":reading.Provider,FontSize=Window.FontSize+3,FontWeight=FontWeights.SemiBold,Foreground=foreground,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(name,2);title.Children.Add(name);body.Children.Add(title);
                string state=QuotaState(reading);if(reading.Observed!=default(DateTimeOffset))state+=" · "+Math.Max(0,(int)(now-reading.Observed).TotalMinutes)+" "+language.T("min ago");
                body.Children.Add(new TextBlock{Text=state,Foreground=foreground,Opacity=.8,Margin=new Thickness(0,5,0,8),TextWrapping=TextWrapping.Wrap});
                if(reading.Provider=="Claude"&&(reading.Status=="Login required"||reading.Status=="Login expired"))body.Children.Add(new TextBlock{Text=language.T("Open Claude Code and renew its login, then refresh. Pulse does not refresh your login."),Foreground=foreground,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)});
                foreach(var balance in reading.Balances){
                    body.Children.Add(new TextBlock{Text=language.T("Available balance"),Foreground=foreground,Opacity=.8,Margin=new Thickness(0,4,0,3)});
                    var amount=new TextBlock{Text=QuotaState(reading)==language.T("Live")?balance.Format(balance.Total):"—",Foreground=foreground,FontSize=Window.FontSize+10,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap};System.Windows.Documents.Typography.SetNumeralAlignment(amount,FontNumeralAlignment.Tabular);body.Children.Add(amount);
                    if(full&&QuotaState(reading)==language.T("Live"))body.Children.Add(new TextBlock{Text=language.T("Granted")+" · "+balance.Format(balance.Granted)+Environment.NewLine+language.T("Topped up")+" · "+balance.Format(balance.ToppedUp),Foreground=foreground,Opacity=.8,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,3)});
                }
                foreach(var window in reading.Windows){
                    var row=new Grid{Margin=new Thickness(0,6,0,3)};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
                    row.Children.Add(new TextBlock{Text=language.T(window.Label),Foreground=foreground,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,8,0)});
                    var value=new TextBlock{Text=QuotaValue(reading,window),Foreground=foreground,FontWeight=FontWeights.SemiBold};Grid.SetColumn(value,1);row.Children.Add(value);body.Children.Add(row);
                    if(window.Remaining.HasValue&&QuotaState(reading)==language.T("Live"))body.Children.Add(new ProgressBar{Minimum=0,Maximum=100,Value=window.Remaining.Value,Height=3,Foreground=Brush(accent),Background=Brush("#304F6B7C"),IsHitTestVisible=false});
                    string reset=QuotaDecoder.ResetText(window.Reset,now);if(reset!="")body.Children.Add(new TextBlock{Text=reset=="Reset pending"?language.T(reset):reset.Replace("Reset ",language.T("Reset")+" "),Foreground=foreground,Opacity=.75,Margin=new Thickness(0,3,0,5)});
                }
                var card=new Border{Tag=reading.Provider,Child=body,Padding=new Thickness(12),Margin=new Thickness(0,8,0,0),CornerRadius=new CornerRadius(16),BorderThickness=new Thickness(1),BorderBrush=Brush("#608BA8B8"),Background=SystemParameters.HighContrast?SystemColors.WindowBrush:Brush(light?"#20FFFFFF":"#20122029")};panel.Children.Add(card);
                DateTimeOffset previous;if(SystemParameters.ClientAreaAnimation&&reading.Observed!=default(DateTimeOffset)&&(!quotaAnimated.TryGetValue(reading.Provider,out previous)||previous!=reading.Observed))card.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(.65,1,TimeSpan.FromMilliseconds(160)){FillBehavior=FillBehavior.Stop});quotaAnimated[reading.Provider]=reading.Observed;
                panel.Attach(card,grip,Control<ScrollViewer>("CardScroll"),delegate{settings.Data["quotaCardOrder"]=panel.Children.Cast<Border>().Select(c=>(string)c.Tag).Concat(QuotaOrder()).Distinct().ToArray();QueueSave();});
            }
        }
        void AddDesktopQuotas(List<DesktopMetric> metrics){foreach(var reading in quotas.Readings){
            if(reading.Balances.Count>0&&QuotaState(reading)==language.T("Live")){foreach(var balance in reading.Balances)metrics.Add(new DesktopMetric("quota"+reading.Provider+balance.Currency,reading.Provider+" · "+language.T("Balance"),balance.Format(balance.Total),QuotaCatalog.Find(reading.Provider).Icon));continue;}
            string icon=QuotaCatalog.Find(reading.Provider).Icon;
            if(reading.Windows.Count==0||QuotaState(reading)!=language.T("Live")){metrics.Add(new DesktopMetric("quota"+reading.Provider,reading.Provider,QuotaState(reading),icon));continue;}
            int index=0;foreach(var window in reading.Windows)metrics.Add(new DesktopMetric("quota"+reading.Provider+(index++),(reading.Provider=="Antigravity"?"Gemini":reading.Provider)+" · "+language.T(window.Label),QuotaValue(reading,window),icon));
        }}
    }
}
