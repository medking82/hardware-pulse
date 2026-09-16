using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class SettingsTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Until(Func<bool> done) {
        var end=DateTime.UtcNow.AddSeconds(5);
        while(!done()&&DateTime.UtcNow<end){using var slice=new CancellationTokenSource(TimeSpan.FromMilliseconds(20));Dispatcher.UIThread.MainLoop(slice.Token);}
        Check(done(),"Settings operation timed out");
    }
    public static void Run(string? output) {
        string directory=Directory.CreateTempSubdirectory("pulse-preview-settings-").FullName;
        try {
            string path=Path.Combine(directory,"settings.json");
            File.WriteAllText(path,"{\"schema\":1,\"width\":-999,\"height\":99999,\"theme\":\"bad\",\"network\":\"missing-interface\",\"codex\":true,\"future\":{\"keep\":7}}");
            var store=new PreviewSettingsStore(path);var value=store.Load();
            Check(!value.Claude,"Existing settings must not opt into Claude credentials");
            Check(!value.Antigravity,"Existing settings must not opt into Antigravity");
            value.Antigravity=true;
            Check(value.FloatingIconsFollowApp&&value.FloatingTextOpacity==100&&value.FloatingTextColor=="","Old settings retain readable appearance defaults");
            Check(value.Width==360&&value.Height==1600&&value.Theme=="System"&&value.Codex,"Settings normalize known values");
            value.Width=700;value.Height=650;Check(store.Save(value),"Atomic save");
            value.FloatingWidth=620;value.FloatingHeight=730;value.FloatingX=-900;value.FloatingY=80;value.FloatingPositionSet=true;value.FloatingTopmost=true;
            value.FloatingBackgroundOpacity=35;
            Check(store.Save(value),"Floating settings save");
            var floatingSaved=new PreviewSettingsStore(path).Load();
            Check(floatingSaved.Antigravity,"Antigravity opt-in round trip");
            value.Antigravity=false;Check(store.Save(value),"Antigravity opt-out saved independently");
            Check(floatingSaved.FloatingBackgroundOpacity==35,"Floating background opacity round trip");
            Check(floatingSaved.FloatingWidth==620&&floatingSaved.FloatingHeight==730&&floatingSaved.FloatingX==-900&&floatingSaved.FloatingY==80&&floatingSaved.FloatingTopmost&&floatingSaved.FloatingPositionSet,"Floating settings round trip");
            using(var doc=JsonDocument.Parse(File.ReadAllText(path)))Check(doc.RootElement.GetProperty("future").GetProperty("keep").GetInt32()==7,"Unknown fields retained");
            if(!OperatingSystem.IsWindows())Check((File.GetUnixFileMode(path)&(UnixFileMode.GroupRead|UnixFileMode.OtherRead|UnixFileMode.GroupWrite|UnixFileMode.OtherWrite))==0,"Settings private permissions");
            foreach(string bad in new[]{"{broken","{\"schema\":2,\"codex\":true}","{\"schema\":\"invalid\"}",new string(' ',65537)}) {
                string broken=Path.Combine(directory,"broken.json");File.WriteAllText(broken,bad);
                var invalid=new PreviewSettingsStore(broken);var defaults=invalid.Load();
                Check(!defaults.Codex&&invalid.Error!=null&&!invalid.Save(defaults)&&File.ReadAllText(broken)==bad,"Unreadable/new schema file preserved; quota remains off");
            }
            var denied=new PreviewSettingsStore(Path.Combine(path,"child.json"));denied.Load();
            Check(!denied.Save(value),"Write failure reported");
            Check(!Directory.GetFiles(directory,".settings-*.tmp").Any(),"No temporary save debris");

            // Explicit temporary profile and demo reader: never touch personal settings or login.
            var window=new MonitorWindow(new MonitorSource(true),store:new PreviewSettingsStore(path));window.Show();
            var main=window.GetVisualDescendants().OfType<TabControl>().Single(x=>x.Name=="MainTabs");main.SelectedIndex=1;
            Dispatcher.UIThread.RunJobs();
            var groups=window.GetVisualDescendants().OfType<TabControl>().Single(x=>x.Name=="SettingsTabs");
            var network=window.GetVisualDescendants().OfType<ComboBox>().Single();
            Until(()=>network.Items.Count>0);Check(network.SelectedItem==null,"Missing interface must not switch silently");
            window.PresentInterfaces(["other","missing-interface"]);
            Check((string?)network.SelectedItem=="missing-interface","Reconnected saved interface restored");
            window.PresentInterfaces(["other"]);
            Check(network.SelectedItem==null,"Removed interface must not fall back to another device");
            window.PresentInterfaces([]);
            Check(network.SelectedItem==null&&window.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Name=="NetworkStatus"&&x.Text!.StartsWith("No network")),"Empty discovery has actionable status");
            var refresh=window.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="RefreshInterfaces");
            refresh.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Until(()=>refresh.IsEnabled&&network.Items.Count>0);
            Check(network.SelectedItem==null,"Refresh preserves absent saved preference");
            window.PresentInterfaces(["missing-interface","other"]);network.SelectedItem="other";
            window.PresentInterfaces(["missing-interface","other"]);
            Check((string?)network.SelectedItem=="other","Refresh preserves explicit selection regardless of ordering");
            network.SelectedItem="missing-interface";
            groups.SelectedIndex=1;Dispatcher.UIThread.RunJobs();
            var theme=window.GetVisualDescendants().OfType<ComboBox>().Single(x=>x.Name=="PreviewTheme");theme.SelectedItem="Dark";
            Check(window.RequestedThemeVariant==ThemeVariant.Dark,"Theme applies immediately");
            Until(()=>new PreviewSettingsStore(path).Load().Theme=="Dark");
            groups.SelectedIndex=3;Dispatcher.UIThread.RunJobs();window.OpenFloatingMonitor();
            var topmost=window.GetVisualDescendants().OfType<CheckBox>().Single(x=>x.Name=="DesktopTopmost");
            topmost.IsChecked=false;Check(!window.FloatingMonitor!.Topmost,"Settings topmost did not apply live");
            var floatingTopmost=window.FloatingMonitor.GetVisualDescendants().OfType<CheckBox>().Single();
            floatingTopmost.IsChecked=true;Check(topmost.IsChecked==true,"Floating topmost did not synchronize Settings");
            var fontSize=window.GetVisualDescendants().OfType<Slider>().Single(x=>x.Name=="FloatingFontSize");
            fontSize.Value=24;Check(window.FloatingMonitor.FontSize==24,"Floating font size did not apply live");
            Until(()=>new PreviewSettingsStore(path).Load().FloatingFontSize==24);
            var opacity=window.GetVisualDescendants().OfType<Slider>().Single(x=>x.Name=="FloatingBackgroundOpacity");
            if(output!=null){window.Width=360;Dispatcher.UIThread.RunJobs();using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"desktop-settings-360.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
            Check(opacity.Value==35,"Desktop opacity control did not restore saved preference");
            var blur=window.GetVisualDescendants().OfType<CheckBox>().Single(x=>x.Name=="FloatingBackgroundBlur");
            Check(blur.IsChecked==false,"Blur must remain opt in");blur.IsChecked=true;
            var textOpacity=window.GetVisualDescendants().OfType<Slider>().Single(x=>x.Name=="FloatingTextOpacity");
            var rowSpacing=window.GetVisualDescendants().OfType<Slider>().Single(x=>x.Name=="FloatingRowSpacing");
            var textColor=window.GetVisualDescendants().OfType<TextBox>().Single(x=>x.Name=="FloatingTextColor");
            var follow=window.GetVisualDescendants().OfType<CheckBox>().Single(x=>x.Name=="FloatingIconsFollowApp");
            textColor.Text="#123456";Dispatcher.UIThread.RunJobs();textOpacity.Value=40;rowSpacing.Value=12;
            var cpuLabel=window.FloatingMonitor.GetVisualDescendants().OfType<TextBlock>().Single(x=>x.Text=="CPU");
            var readingBrush=(Avalonia.Media.ISolidColorBrush)cpuLabel.Foreground!;
            Check(readingBrush.Color==Avalonia.Media.Color.Parse("#66123456"),"Custom text color and alpha must apply independently");
            Check(window.FloatingMonitor.BackgroundOpacity==35&&window.FloatingMonitor.Opacity==1,"Text appearance must preserve background and window opacity");
            follow.IsChecked=false;
            var cpuIcon=window.FloatingMonitor.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single(x=>Equals(x.Tag,"cpu"));
            Check(((Avalonia.Media.ISolidColorBrush)(cpuIcon.Stroke??cpuIcon.Fill)!).Color==readingBrush.Color,"Opt-out icons follow text color");
            follow.IsChecked=true;var appColor=((Avalonia.Media.ISolidColorBrush)(cpuIcon.Stroke??cpuIcon.Fill)!).Color;
            window.FloatingMonitor.Topmost=!window.FloatingMonitor.Topmost;
            Check(((Avalonia.Media.ISolidColorBrush)(cpuIcon.Stroke??cpuIcon.Fill)!).Color==appColor&&appColor.A==255,"Topmost must preserve App icon colors");
            window.FloatingMonitor.Topmost=true;
            textColor.Text="#12";Dispatcher.UIThread.RunJobs();
            Check(((Avalonia.Media.ISolidColorBrush)cpuLabel.Foreground!).Color==readingBrush.Color,"Incomplete color must preserve prior valid appearance");
            Until(()=>new PreviewSettingsStore(path).Load().FloatingRowSpacing==12);
            var appearance=new PreviewSettingsStore(path).Load();
            Check(appearance.FloatingTextColor=="#123456"&&appearance.FloatingTextOpacity==40&&appearance.FloatingIconsFollowApp,"Appearance settings round trip");
            Check(window.FloatingMonitor!.BackgroundBlur,"Blur preference did not reach open floating window");
            Until(()=>new PreviewSettingsStore(path).Load().FloatingBackgroundBlur);
            opacity.Value=0;Check(window.FloatingMonitor!.BackgroundOpacity==0&&window.FloatingMonitor.Opacity==1,"Background adjustment faded entire window");
            Until(()=>new PreviewSettingsStore(path).Load().FloatingBackgroundOpacity==0);
            window.FloatingMonitor.Close();window.OpenFloatingMonitor();
            Check(window.FloatingMonitor!.BackgroundOpacity==0,"Transparent background not restored on reopen");
            Check(window.FloatingMonitor.FontSize==24&&window.FloatingMonitor.Topmost,"Font size/topmost not restored");
            Check(window.FloatingMonitor.BackgroundBlur,"Blur preference lost on reopen");
            blur.IsChecked=false;Check(window.FloatingMonitor.MaterialStatus=="Background blur is off.","Disabling blur not reflected in status");
            opacity.Value=100;Check(window.FloatingMonitor.BackgroundOpacity==100,"Solid background not applied live");window.FloatingMonitor.Close();
            groups.SelectedIndex=2;Dispatcher.UIThread.RunJobs();
            var quota=window.GetVisualDescendants().OfType<CheckBox>().Single(x=>x.Name=="EnableCodexQuota");Check(quota.IsChecked==true,"Quota choice restored with explicit demo reader");quota.IsChecked=false;
            var claude=window.GetVisualDescendants().OfType<CheckBox>().Single(x=>x.Name=="EnableClaudeQuota");
            Check(claude.IsChecked==false,"Claude starts disabled independently of Codex");claude.IsChecked=true;
            var antigravity=window.GetVisualDescendants().OfType<CheckBox>().Single(x=>x.Name=="EnableAntigravityQuota");
            Check(antigravity.IsChecked==false,"Antigravity starts disabled independently");antigravity.IsChecked=true;
            if(output!=null){window.Width=360;Dispatcher.UIThread.RunJobs();using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"settings-360.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
            window.Close();Until(()=>window.Sampling.IsCompleted);
            var saved=new PreviewSettingsStore(path).Load();Check(saved.Theme=="Dark"&&!saved.Codex&&saved.Network=="missing-interface","Choices survive close");
            Check(saved.Claude&&saved.Antigravity&&!saved.Codex,"Provider opt-ins persist independently");
            var reopened=new MonitorWindow(new MonitorSource(true),start:false,store:new PreviewSettingsStore(path));
            Check(reopened.RequestedThemeVariant==ThemeVariant.Dark&&reopened.Width==saved.Width,"Choices survive reopen");reopened.Show();reopened.Close();
            Console.WriteLine("PASS isolated preview settings: normalization, unknown fields, atomic save, error preservation, theme, quota and missing interface");
        } finally {
            Check(Path.GetFullPath(directory).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase),"Fixture cleanup boundary");
            Directory.Delete(directory,true);
        }
    }
}
