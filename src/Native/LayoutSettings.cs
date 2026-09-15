using System;
using System.Windows;
using System.Windows.Controls;

namespace HardwarePulse {
    public sealed partial class Shell {
        string settingsCategory="General";
        readonly System.Collections.Generic.List<Button> settingsTabs=new System.Collections.Generic.List<Button>();
        void ReplaceCardPanel(string name){
            var old=Control<StackPanel>(name);var parent=(Panel)old.Parent;int index=parent.Children.IndexOf(old);
            var replacement=new ResponsivePanel{Name=name};parent.Children.Remove(old);parent.Children.Insert(index,replacement);
            Window.UnregisterName(name);Window.RegisterName(name,replacement);
        }
        void BuildSettingsLayout(){
            var page=Control<ScrollViewer>("SettingsPage");var old=(StackPanel)page.Content;
            var children=new System.Collections.Generic.List<UIElement>();foreach(UIElement child in old.Children)children.Add(child);old.Children.Clear();
            var desktopLayout=Control<Expander>("DesktopSection");children.Remove(desktopLayout);children.Insert(children.IndexOf(Control<Expander>("DesktopAppearanceSection")),desktopLayout);
            var panel=new ResponsivePanel{Name="SettingsSections",MinimumColumnWidth=350,RowGap=14,IndependentColumns=true};foreach(var child in children){
                var section=child as Expander;if(section!=null){string title=section.Header as string;section.Tag=title.StartsWith("Desktop")?"Desktop":title=="App Appearance"||title=="Window"?"App Appearance":title=="Game Overlay"?"FPS":title=="AI Quota"?"AI Quota":title=="App Cards"||title=="Hardware Names"?"App Cards":"General";}
                panel.Children.Add(child);
            }
            page.Content=panel;Window.RegisterName("SettingsSections",panel);
            var toolbar=Control<Border>("SettingsToolbar");var heading=(UIElement)toolbar.Child;toolbar.Child=null;
            var contents=new StackPanel();contents.Children.Add(heading);var tabs=new WrapPanel{Margin=new Thickness(0,8,0,0)};contents.Children.Add(tabs);toolbar.Child=contents;toolbar.Height=double.NaN;
            foreach(string category in new[]{"General","App Appearance","Desktop","App Cards","FPS","AI Quota"}){
                var button=new Button{Content=category,Tag=category,Padding=new Thickness(10,5,10,5),Margin=new Thickness(0,0,6,6),MinHeight=34};
                button.Click+=delegate{SelectSettingsCategory((string)button.Tag);};settingsTabs.Add(button);tabs.Children.Add(button);
            }
            toolbar.SizeChanged+=delegate{page.Margin=new Thickness(0,toolbar.ActualHeight,0,0);};
            SelectSettingsCategory(settingsCategory);
        }
        void SelectSettingsCategory(string category){settingsCategory=category;foreach(UIElement child in Control<ResponsivePanel>("SettingsSections").Children){var section=child as Expander;if(section!=null){bool selected=(string)section.Tag==category;section.Visibility=selected?Visibility.Visible:Visibility.Collapsed;if(selected)section.IsExpanded=true;}}Control<ScrollViewer>("SettingsPage").ScrollToTop();UpdateSettingsTabs();}
        void UpdateSettingsTabs(){foreach(var button in settingsTabs){button.Content=language.T((string)button.Tag);button.Foreground=SystemParameters.HighContrast?SystemColors.ControlTextBrush:Brush(light?"#17202B":"#F0F5FA");button.FontWeight=(string)button.Tag==settingsCategory?FontWeights.Bold:FontWeights.Normal;button.BorderThickness=new Thickness((string)button.Tag==settingsCategory?2:1);System.Windows.Automation.AutomationProperties.SetHelpText(button,(string)button.Tag==settingsCategory?language.T("Selected"):"");}}
        void EnterDesktop(){
            settings.Data["desktopEnabled"]=true;settings.Data["desktopLocked"]=true;
            Control<CheckBox>("DesktopEnabled").IsChecked=true;Control<CheckBox>("DesktopLocked").IsChecked=true;
            UpdateDesktop();Save();Window.Hide();
            if(!isolated&&!settings.Flag("desktopHintShown")){tray.ShowBalloonTip(7000,"Pulse",language.T("Desktop is ready. Right-click the Pulse tray icon to edit or return to the App."),System.Windows.Forms.ToolTipIcon.Info);settings.Data["desktopHintShown"]=true;QueueSave();}
        }
        bool DesktopMetricEnabled(string key){
            if(key=="fps"){object fps;return settings.Map("desktopVisible").TryGetValue(key,out fps)&&fps is bool&&(bool)fps;}
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
            string color=DesktopPaletteColor(icon,textColor);
            if(!settings.Flag("desktopAlwaysOnTop")||SystemParameters.HighContrast||color==textColor)return color;
            var tint=(System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color);
            return "#"+((byte)(tint.R*.38)).ToString("X2")+((byte)(tint.G*.38)).ToString("X2")+((byte)(tint.B*.38)).ToString("X2");
        }
        string DesktopPaletteColor(string icon,string textColor){
            if(!settings.Flag("desktopAppIconColors",true)||SystemParameters.HighContrast)return textColor;
            if(light)return "#17202B";
            if(settings.Flag("unifiedReadingColors"))return ReadingColor();
            switch(icon){case "cpu":case "codex":return "#A5E7D5";case "gpu":case "antigravity":return "#A7CBFF";
                case "memory":return "#E7C5A4";case "claude":return "#E7B497";case "nvme":return "#B9B7ED";
                case "airflow":return "#A8D4D0";case "network":case "ethernet":case "wifi":case "signal":return "#A9D8E8";default:return textColor;}
        }
    }
}
