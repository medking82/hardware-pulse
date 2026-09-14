using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HardwarePulse {
    public sealed partial class Shell {
        readonly QuotaSession quotas=new QuotaSession(QuotaProviders.Read);
        string quotaSignature;
        void WireQuota(){
            foreach(string provider in QuotaSession.Providers){string name=provider;var control=Control<CheckBox>("Quota"+name);control.IsChecked=settings.Flag("quota"+name,false);quotas.Enable(name,control.IsChecked==true);
                control.Click+=delegate{settings.Data["quota"+name]=control.IsChecked==true;quotas.Enable(name,control.IsChecked==true);if(!isolated)quotas.Tick(DateTimeOffset.UtcNow);RenderQuota();UpdateDesktop();QueueSave();};
            }
            Click("QuotaRefresh",delegate{quotas.Refresh();if(!isolated)quotas.Tick(DateTimeOffset.UtcNow);RenderQuota();});
        }
        string QuotaState(QuotaReading r){if(r.Status=="Live"&&DateTimeOffset.UtcNow-r.Observed>TimeSpan.FromMinutes(10))return language.T("Quota stale");return language.T(r.Status);}
        string QuotaValue(QuotaReading r,QuotaWindow w){return QuotaState(r)==language.T("Live")&&w.Remaining.HasValue?w.Remaining.Value.ToString("0.#")+"% "+language.T("left"):"—";}
        void RenderQuota(){
            var readings=quotas.Readings;var now=DateTimeOffset.UtcNow;
            string signature=language.Preference+"|"+light+"|"+SystemParameters.HighContrast+"|"+Window.FontSize+"|"+settings.Flag("unifiedReadingColors")+ReadingColor()+"|"+string.Join(";",readings.Select(r=>r.Provider+r.Status+r.Observed.ToString("o")+(int)(now-r.Observed).TotalMinutes));
            if(signature==quotaSignature)return;quotaSignature=signature;var panel=Control<StackPanel>("QuotaCards");panel.Children.Clear();
            foreach(var reading in readings){
                var body=new StackPanel();var foreground=SystemParameters.HighContrast?SystemColors.WindowTextBrush:Brush(light?"#17202B":"#F0F5FA");
                string accent=SystemParameters.HighContrast?SystemColors.WindowTextColor.ToString():light?"#17202B":settings.Flag("unifiedReadingColors")?ReadingColor():reading.Provider=="Codex"?"#A5E7D5":reading.Provider=="Claude"?"#E7B497":"#A7CBFF";
                var title=new StackPanel{Orientation=Orientation.Horizontal};var icon=Icon(reading.Provider.ToLowerInvariant(),19,accent);icon.Margin=new Thickness(0,0,8,0);title.Children.Add(icon);title.Children.Add(new TextBlock{Text=reading.Provider=="Antigravity"?"Antigravity · Gemini":reading.Provider,FontSize=Window.FontSize+3,FontWeight=FontWeights.SemiBold,Foreground=foreground});body.Children.Add(title);
                string state=QuotaState(reading);if(reading.Observed!=default(DateTimeOffset))state+=" · "+Math.Max(0,(int)(now-reading.Observed).TotalMinutes)+" "+language.T("min ago");
                body.Children.Add(new TextBlock{Text=state,Foreground=foreground,Opacity=.8,Margin=new Thickness(0,5,0,8),TextWrapping=TextWrapping.Wrap});
                foreach(var window in reading.Windows){
                    var row=new Grid{Margin=new Thickness(0,6,0,3)};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
                    row.Children.Add(new TextBlock{Text=language.T(window.Label),Foreground=foreground,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,8,0)});
                    var value=new TextBlock{Text=QuotaValue(reading,window),Foreground=foreground,FontWeight=FontWeights.SemiBold};Grid.SetColumn(value,1);row.Children.Add(value);body.Children.Add(row);
                    if(window.Remaining.HasValue&&QuotaState(reading)==language.T("Live"))body.Children.Add(new ProgressBar{Minimum=0,Maximum=100,Value=window.Remaining.Value,Height=3,Foreground=Brush(accent),Background=Brush("#304F6B7C"),IsHitTestVisible=false});
                    string reset=QuotaData.ResetText(window.Reset,now);if(reset!="")body.Children.Add(new TextBlock{Text=reset=="Reset pending"?language.T(reset):reset.Replace("Reset ",language.T("Reset")+" "),Foreground=foreground,Opacity=.75,Margin=new Thickness(0,3,0,5)});
                }
                panel.Children.Add(new Border{Child=body,Padding=new Thickness(12),Margin=new Thickness(0,8,0,0),CornerRadius=new CornerRadius(16),BorderThickness=new Thickness(1),BorderBrush=Brush("#608BA8B8"),Background=SystemParameters.HighContrast?SystemColors.WindowBrush:Brush(light?"#20FFFFFF":"#20122029")});
            }
        }
        void AddDesktopQuotas(List<DesktopMetric> metrics){foreach(var reading in quotas.Readings){
            if(reading.Windows.Count==0||QuotaState(reading)!=language.T("Live")){metrics.Add(new DesktopMetric("quota"+reading.Provider,reading.Provider,QuotaState(reading),reading.Provider.ToLowerInvariant()));continue;}
            int index=0;foreach(var window in reading.Windows)metrics.Add(new DesktopMetric("quota"+reading.Provider+(index++),(reading.Provider=="Antigravity"?"Gemini":reading.Provider)+" · "+language.T(window.Label),QuotaValue(reading,window),reading.Provider.ToLowerInvariant()));
        }}
    }
}
