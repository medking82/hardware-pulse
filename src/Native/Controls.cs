using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Forms=System.Windows.Forms;

namespace HardwarePulse {
    public sealed partial class Shell {
        void Click(string name,Action action){Control<Button>(name).Click+=delegate{action();};}
        void WireSettings(){
            Control<Button>("Settings").Content=Icon("settings",20);Control<Button>("Back").Content=Icon("back",16);Control<Button>("Minimize").Content=Icon("minimize",14);Control<Button>("Close").Content=Icon("close",14);
            Click("Settings",()=>ShowSettings(true));Click("Back",()=>ShowSettings(false));Click("Minimize",()=>Window.WindowState=WindowState.Minimized);Click("Close",()=>Window.Close());
            Click("Live",()=>{maximum=false;UpdatePanel();});Click("Max",()=>{maximum=true;UpdatePanel();});Click("Details",()=>{settings.Data["details"]=!settings.Flag("details");ApplyDensity();QueueSave();});
            Control<FrameworkElement>("DragHandle").MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){if(!locked&&e.ButtonState==MouseButtonState.Pressed)Window.DragMove();};
            Control<ScrollViewer>("CardScroll").SizeChanged+=delegate{ApplyDensity();};
            Control<CheckBox>("Pin").Click+=delegate{Window.Topmost=Checked("Pin");QueueSave();};Control<CheckBox>("LockPosition").IsChecked=locked;Control<CheckBox>("LockPosition").Click+=delegate{locked=Checked("LockPosition");ApplyLock();QueueSave();};
            Control<CheckBox>("Solid").Click+=delegate{ApplyMaterial();QueueSave();};Control<Slider>("OpacitySlider").ValueChanged+=delegate{ApplyMaterial();QueueSave();};
            Text("FontSizeValue",Window.FontSize+" px");Control<Slider>("FontSizeSlider").ValueChanged+=delegate{Window.FontSize=Control<Slider>("FontSizeSlider").Value;Text("FontSizeValue",Window.FontSize+" px");ApplyDensity();QueueSave();};
            var picker=Control<ComboBox>("LanguagePicker");foreach(ComboBoxItem item in picker.Items)if((string)item.Tag==language.Preference)picker.SelectedItem=item;picker.SelectionChanged+=delegate{var item=picker.SelectedItem as ComboBoxItem;if(item!=null){language.Preference=(string)item.Tag;Localize();QueueSave();}};
            Click("BackgroundColor",delegate{using(var dialog=new Forms.ColorDialog {FullOpen=true,Color=System.Drawing.ColorTranslator.FromHtml(BackgroundHex())})if(dialog.ShowDialog()==Forms.DialogResult.OK){settings.Data["background"]="#"+dialog.Color.R.ToString("X2")+dialog.Color.G.ToString("X2")+dialog.Color.B.ToString("X2");ApplyMaterial();QueueSave();}});
            Click("GitHub",()=>Process.Start(new ProcessStartInfo("https://github.com/medking82/hardware-pulse") {UseShellExecute=true}));
            foreach(var pair in new[]{new[]{"CPU","CPU Name"},new[]{"GPU","GPU Name"},new[]{"Memory","Memory Name"},new[]{"NVMe","NVMe Name"},new[]{"Airflow","Case / Motherboard"},new[]{"ramA","Module 1"},new[]{"ramB","Module 2"},new[]{"diskC","Drive 1"},new[]{"diskD","Drive 2"},new[]{"cpuFan","CPU Fan Name"},new[]{"bottom","System Fan 1"},new[]{"top","System Fan 2"}}){
                string key=pair[0];var label=Label(pair[1],11);Catalog(label);Control<StackPanel>("NameFields").Children.Add(label);object saved;var editor=new TextBox {MaxLength=160,Padding=new Thickness(6),Margin=new Thickness(0,0,0,12),Text=settings.Map("names").TryGetValue(key,out saved)?Convert.ToString(saved):""};editor.TextChanged+=delegate{settings.Map("names")[key]=editor.Text.Trim();foreach(var view in views.Values)UpdateCard(view);ApplyDensity();QueueSave();};Control<StackPanel>("NameFields").Children.Add(editor);
            }
            if(!isolated)RefreshStartup();Control<CheckBox>("StartWithWindows").Click+=async delegate{
                bool wanted=Checked("StartWithWindows");Control<CheckBox>("StartWithWindows").IsEnabled=false;
                try{int code=await Task.Run(()=>{using(var child=Process.Start(new ProcessStartInfo(paths.Exe,wanted?"--enable-startup":"--disable-startup") {UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden})){child.WaitForExit();return child.ExitCode;}});RefreshStartup();if(code!=0)Text("StartupStatus",language.T("Startup change failed"));}
                catch{RefreshStartup();Text("StartupStatus",language.T("Startup change canceled or failed"));}finally{Control<CheckBox>("StartWithWindows").IsEnabled=true;}
            };
        }
        void RefreshStartup(){try{using(var store=new SchedulerStore())Control<CheckBox>("StartWithWindows").IsChecked=new Startup(store,paths.Exe,WindowsIdentity.GetCurrent().User.Value).IsEnabled();Text("StartupStatus","");}catch{Control<CheckBox>("StartWithWindows").IsChecked=false;Text("StartupStatus",language.T("Startup tasks are unavailable; reinstall to repair"));}}
        public void ShowSettings(bool show){settingsVisible=show;Control<ScrollViewer>("SettingsPage").Visibility=show?Visibility.Visible:Visibility.Collapsed;foreach(string name in new[]{"CardScroll","MonitorControls","MonitorFooter","Status"})Control<FrameworkElement>(name).Visibility=show?Visibility.Collapsed:Visibility.Visible;Control<Button>(show?"Back":"Settings").Focus();ApplyDensity();ApplyMaterial();}
        void ApplyLock(){Window.SetValue(WindowSnap.PositionLockedProperty,locked);Window.ResizeMode=locked?ResizeMode.NoResize:ResizeMode.CanResizeWithGrip;Control<FrameworkElement>("DragHandle").Cursor=locked?Cursors.Arrow:Cursors.SizeAll;Control<CheckBox>("LockPosition").IsChecked=locked;foreach(var view in views.Values){view.Grip.IsEnabled=!locked;view.Grip.Opacity=locked?0:1;}ApplyMaterial();}
        string BackgroundHex(){string color=settings.Text("background","#35383B");return System.Text.RegularExpressions.Regex.IsMatch(color,"^#[0-9a-fA-F]{6}$")?color:"#35383B";}
        void ApplyMaterial(){
            IntPtr hwnd=new WindowInteropHelper(Window).Handle;if(hwnd==IntPtr.Zero)return;
            bool solid=Checked("Solid")||SystemParameters.HighContrast,clear=(locked&&!settingsVisible)||Control<Slider>("OpacitySlider").Value==0;
            bool supported=PulseBackdrop.ApplyStable(hwnd,solid,clear);int dark=1,round=2;PulseBackdrop.DwmSetWindowAttribute(hwnd,20,ref dark,4);PulseBackdrop.DwmSetWindowAttribute(hwnd,33,ref round,4);
            var margins=new PulseBackdrop.Margins {Left=-1,Right=-1,Top=-1,Bottom=-1};PulseBackdrop.DwmExtendFrameIntoClientArea(hwnd,ref margins);HwndSource.FromHwnd(hwnd).CompositionTarget.BackgroundColor=Colors.Transparent;
            var color=(Color)ColorConverter.ConvertFromString(BackgroundHex());light=(.2126*color.R+.7152*color.G+.0722*color.B)/255>.55;
            double opacity=Control<Slider>("OpacitySlider").Value/100;if(locked&&!settingsVisible&&!solid)opacity*=.25;if(solid||!supported)opacity=1;
            Window.Background=new SolidColorBrush(Color.FromArgb((byte)Math.Round(255*opacity),color.R,color.G,color.B));Control<Slider>("OpacitySlider").IsEnabled=!solid&&supported;Text("OpacityValue",Math.Round(opacity*100)+"%");
            foreach(var entry in themed)entry.Item2.SetValue(entry.Item1,light?Brush("#17202B"):entry.Item3,null);
            var viewport=Control<Grid>("Viewport");if(viewport.Background!=null){var background=viewport.Background.Clone();background.Opacity=opacity;viewport.Background=background;}
            foreach(var view in views.Values){var background=Brush(light?"#DDEEF1F4":"#3031485B");background.Opacity=opacity;view.Border.Background=background;}
        }
    }
}
