using System;
using System.Windows;
using System.Windows.Controls;

namespace HardwarePulse {
    public sealed partial class Shell {
        void ReplaceCardPanel(string name){
            var old=Control<StackPanel>(name);var parent=(Panel)old.Parent;int index=parent.Children.IndexOf(old);
            var replacement=new ResponsivePanel{Name=name};parent.Children.Remove(old);parent.Children.Insert(index,replacement);
            Window.UnregisterName(name);Window.RegisterName(name,replacement);
        }
        void BuildSettingsLayout(){
            var page=Control<ScrollViewer>("SettingsPage");var old=(StackPanel)page.Content;
            var children=new System.Collections.Generic.List<UIElement>();foreach(UIElement child in old.Children)children.Add(child);old.Children.Clear();
            var panel=new ResponsivePanel{Name="SettingsSections",MinimumColumnWidth=350,RowGap=14,IndependentColumns=true};foreach(var child in children)panel.Children.Add(child);
            page.Content=panel;Window.RegisterName("SettingsSections",panel);
        }
        void EnterDesktop(){
            settings.Data["desktopEnabled"]=true;settings.Data["desktopLocked"]=true;
            Control<CheckBox>("DesktopEnabled").IsChecked=true;Control<CheckBox>("DesktopLocked").IsChecked=true;
            UpdateDesktop();Save();Window.Hide();
            if(!isolated&&!settings.Flag("desktopHintShown")){tray.ShowBalloonTip(7000,"Pulse",language.T("Desktop is ready. Right-click the Pulse tray icon to edit or return to the App."),System.Windows.Forms.ToolTipIcon.Info);settings.Data["desktopHintShown"]=true;QueueSave();}
        }
        bool DesktopMetricEnabled(string key){
            if(key=="quotaCodex")return DesktopMetricEnabled("quotaCodex0");
            if(key=="quotaAntigravity")return DesktopMetricEnabled("quotaAntigravity0")||DesktopMetricEnabled("quotaAntigravity1");
            if(key=="quotaClaude")return DesktopMetricEnabled("quotaClaude0")||DesktopMetricEnabled("quotaClaude1");
            object value;return !settings.Map("desktopVisible").TryGetValue(key,out value)||!(value is bool)||(bool)value;
        }
        string QuotaDesktopTitle(string key){
            return key=="quotaCodex0"?"Codex · "+language.T("Weekly"):
                key=="quotaAntigravity0"?"Gemini · "+language.T("5-hour"):
                key=="quotaAntigravity1"?"Gemini · "+language.T("Weekly"):
                key=="quotaClaude0"?"Claude · "+language.T("5-hour"):"Claude · "+language.T("Weekly");
        }
        string DesktopIconColor(string icon,string textColor){
            if(!settings.Flag("desktopAppIconColors",true)||SystemParameters.HighContrast)return textColor;
            if(light)return "#17202B";
            if(settings.Flag("unifiedReadingColors"))return ReadingColor();
            switch(icon){case "cpu":case "codex":return "#A5E7D5";case "gpu":case "antigravity":return "#A7CBFF";
                case "memory":return "#E7C5A4";case "claude":return "#E7B497";case "nvme":return "#B9B7ED";
                case "airflow":return "#A8D4D0";case "network":case "ethernet":case "wifi":case "signal":return "#A9D8E8";default:return textColor;}
        }
    }
}
