using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HardwarePulse {
    public sealed partial class Shell {
        string settingsCategory="General";
        string settingsSaveStatus="Changes Save Automatically";
        readonly System.Collections.Generic.List<Button> settingsTabs=new System.Collections.Generic.List<Button>();
        void ReplaceCardPanel(string name){
            var old=Control<StackPanel>(name);var parent=(Panel)old.Parent;int index=parent.Children.IndexOf(old);
            var replacement=new ResponsivePanel{Name=name,EqualRowHeight=true};parent.Children.Remove(old);parent.Children.Insert(index,replacement);
            Window.UnregisterName(name);Window.RegisterName(name,replacement);
        }
        void BuildSettingsLayout(){
            var page=Control<ScrollViewer>("SettingsPage");var old=(StackPanel)page.Content;
            var children=new System.Collections.Generic.List<UIElement>();foreach(UIElement child in old.Children)children.Add(child);old.Children.Clear();
            var desktopLayout=Control<Expander>("DesktopSection");children.Remove(desktopLayout);children.Insert(children.IndexOf(Control<Expander>("DesktopAppearanceSection")),desktopLayout);
            var panel=new ResponsivePanel{Name="SettingsSections",MinimumColumnWidth=350,RowGap=14,IndependentColumns=true};foreach(var child in children){
                var section=child as Expander;if(section!=null){string title=section.Header as string;section.Tag=title.StartsWith("Desktop")?"Desktop":title=="App Appearance"||title=="Window"?"App Appearance":title=="Game Overlay"?"FPS":title=="AI Quota"?"AI Quota":title=="App Cards"||title=="Hardware Names"?"App Cards":"General";}
                if(section!=null&&(string)section.Header=="Local Inference")section.Tag="AI Quota";
                panel.Children.Add(child);
            }
            page.Content=panel;Window.RegisterName("SettingsSections",panel);
            var toolbar=Control<Border>("SettingsToolbar");var heading=(UIElement)toolbar.Child;toolbar.Child=null;
            var contents=new StackPanel();contents.Children.Add(heading);var tabs=new WrapPanel{Margin=new Thickness(0,8,0,0)};contents.Children.Add(tabs);toolbar.Child=contents;toolbar.Height=double.NaN;
            foreach(string category in new[]{"General","App Appearance","Desktop","App Cards","FPS","AI Quota"}){
                var button=new Button{Content=category,Tag=category,Padding=new Thickness(10,5,10,5),Margin=new Thickness(0,0,6,6),MinHeight=34};
                button.Click+=delegate{SelectSettingsCategory((string)button.Tag);};settingsTabs.Add(button);tabs.Children.Add(button);
                button.KeyDown+=SettingsTabKeyDown;
            }
            toolbar.SizeChanged+=delegate{page.Margin=new Thickness(0,toolbar.ActualHeight,0,0);};
            SelectSettingsCategory(settingsCategory);
        }
        void SettingsTabKeyDown(object sender,KeyEventArgs e) {
            if(Keyboard.Modifiers!=ModifierKeys.None)return;
            int index=settingsTabs.IndexOf((Button)sender);
            switch(e.Key) {
                case Key.Left:index=(index+settingsTabs.Count-1)%settingsTabs.Count;break;
                case Key.Right:index=(index+1)%settingsTabs.Count;break;
                case Key.Home:index=0;break;
                case Key.End:index=settingsTabs.Count-1;break;
                default:return;
            }
            SelectSettingsCategory((string)settingsTabs[index].Tag);
            settingsTabs[index].Focus();
            e.Handled=true;
        }
        void UpdateSettingsSaveStatus() {
            bool unreadable=settings.LoadFailed;
            try{settings.CheckForChanges();}
            catch(IOException){unreadable=true;}catch(UnauthorizedAccessException){unreadable=true;}catch(InvalidDataException){unreadable=true;}
            var status=Control<TextBlock>("SettingsSaveStatus");
            status.Text=language.T(unreadable?"Settings Unreadable · Not Saving":settings.ChangedOnDisk?"Settings Changed · Not Saving":settingsSaveStatus);
            status.ToolTip=language.T(unreadable?
                "The original settings file is preserved. Changes will not be saved. Restore the file and reopen Hardware Pulse.":
                settings.ChangedOnDisk?"Settings Changed Outside This Window. The File Is Preserved. Exit Hardware Pulse From The Tray, Then Reopen To Load It.":
                settingsSaveStatus=="Could Not Save Changes"?"Check folder permissions and available disk space, then try saving again.":
                "Changes are saved automatically on this device.");
        }
        void SelectSettingsCategory(string category){settingsCategory=category;foreach(UIElement child in Control<ResponsivePanel>("SettingsSections").Children){var section=child as Expander;if(section!=null){bool selected=(string)section.Tag==category;section.Visibility=selected?Visibility.Visible:Visibility.Collapsed;if(selected)section.IsExpanded=true;}}Control<ScrollViewer>("SettingsPage").ScrollToTop();UpdateSettingsTabs();}
        void UpdateSettingsTabs(){foreach(var button in settingsTabs){button.Content=language.T((string)button.Tag);bool selected=(string)button.Tag==settingsCategory;SetSelected(button,selected);button.BorderThickness=new Thickness(selected?2:1);}}
        void EnterDesktop(){
            settings.Data["desktopEnabled"]=true;settings.Data["desktopLocked"]=true;
            Control<CheckBox>("DesktopEnabled").IsChecked=true;Control<CheckBox>("DesktopLocked").IsChecked=true;
            UpdateDesktop();Save();Window.Hide();
            if(!isolated&&!settings.Flag("desktopHintShown")){tray.ShowBalloonTip(7000,"Pulse",language.T("Desktop is ready. Right-click the Pulse tray icon to edit or return to the App."),System.Windows.Forms.ToolTipIcon.Info);settings.Data["desktopHintShown"]=true;QueueSave();}
        }
        bool DesktopMetricEnabled(string key){
            key=DesktopOrderKey(key);
            if(key=="fps"){object fps;return FpsSupported&&settings.Map("desktopVisible").TryGetValue(key,out fps)&&fps is bool&&(bool)fps;}
            if(key=="quotaCodex")return DesktopMetricEnabled("quotaCodex0");
            if(key=="quotaAntigravity")return DesktopMetricEnabled("quotaAntigravity0")||DesktopMetricEnabled("quotaAntigravity1")||DesktopMetricEnabled("quotaAntigravityArgonWeekly");
            if(key=="quotaClaude")return DesktopMetricEnabled("quotaClaude0")||DesktopMetricEnabled("quotaClaude1");
            if(key=="netSignal")key="wifiSignal";
            object value;var visible=settings.Map("desktopVisible");
            if(visible.TryGetValue(key,out value)&&value is bool)return (bool)value;
            if(key=="wifiSignal"&&visible.TryGetValue("netSignal",out value)&&value is bool)return (bool)value;
            return true;
        }
        string QuotaDesktopTitle(string key){
            foreach(var provider in QuotaCatalog.All)if(provider.Money&&key=="quota"+provider.Name)return provider.Name;
            return key=="quotaCodex0"?"Codex · "+language.T("Weekly"):
                key=="quotaAntigravity0"?"Gemini · "+language.Label("5-hour"):
                key=="quotaAntigravity1"?"Gemini · "+language.T("Weekly"):
                key=="quotaAntigravityArgonWeekly"?"Argon · "+language.T("Weekly"):
                key=="quotaClaude0"?"Claude · "+language.Label("5-hour"):"Claude · "+language.T("Weekly");
        }
        string DesktopIconColor(string icon,string textColor){
            return DesktopPaletteColor(icon,textColor);
        }
        string DesktopPaletteColor(string icon,string textColor){
            if(!settings.Flag("desktopAppIconColors",true)||SystemParameters.HighContrast)return textColor;
            if(icon=="fps")return "#9EDFD3";
            if(light)return "#17202B";
            if(settings.Flag("unifiedReadingColors"))return ReadingColor();
            foreach(var provider in QuotaCatalog.All)if(provider.Icon==icon)return provider.Accent;
            foreach(var provider in LocalInferenceCatalog.All)if(provider.Icon==icon)return provider.Accent;
            switch(icon){case "cpu":return "#A5E7D5";case "gpu":return "#A7CBFF";
                case "memory":return "#E7C5A4";case "nvme":return "#B9B7ED";
                case "airflow":return "#A8D4D0";case "network":case "ethernet":case "wifi":case "signal":return "#A9D8E8";default:return textColor;}
        }
    }
}
