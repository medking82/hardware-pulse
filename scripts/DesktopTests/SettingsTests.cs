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
            Check(value.Width==360&&value.Height==1600&&value.Theme=="System"&&value.Codex,"Settings normalize known values");
            value.Width=700;value.Height=650;Check(store.Save(value),"Atomic save");
            value.FloatingWidth=620;value.FloatingHeight=730;value.FloatingX=-900;value.FloatingY=80;value.FloatingPositionSet=true;value.FloatingTopmost=true;
            value.FloatingBackgroundOpacity=35;
            Check(store.Save(value),"Floating settings save");
            var floatingSaved=new PreviewSettingsStore(path).Load();
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
            var opacity=window.GetVisualDescendants().OfType<Slider>().Single(x=>x.Name=="FloatingBackgroundOpacity");
            if(output!=null){window.Width=360;Dispatcher.UIThread.RunJobs();using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"desktop-settings-360.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
            Check(opacity.Value==35,"Desktop opacity control did not restore saved preference");
            opacity.Value=0;Check(window.FloatingMonitor!.BackgroundOpacity==0&&window.FloatingMonitor.Opacity==1,"Background adjustment faded entire window");
            Until(()=>new PreviewSettingsStore(path).Load().FloatingBackgroundOpacity==0);
            window.FloatingMonitor.Close();window.OpenFloatingMonitor();
            Check(window.FloatingMonitor!.BackgroundOpacity==0,"Transparent background not restored on reopen");
            opacity.Value=100;Check(window.FloatingMonitor.BackgroundOpacity==100,"Solid background not applied live");window.FloatingMonitor.Close();
            groups.SelectedIndex=2;Dispatcher.UIThread.RunJobs();
            var quota=window.GetVisualDescendants().OfType<CheckBox>().Single(x=>x.Name=="EnableCodexQuota");Check(quota.IsChecked==true,"Quota choice restored with explicit demo reader");quota.IsChecked=false;
            if(output!=null){window.Width=360;Dispatcher.UIThread.RunJobs();using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"settings-360.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
            window.Close();Until(()=>window.Sampling.IsCompleted);
            var saved=new PreviewSettingsStore(path).Load();Check(saved.Theme=="Dark"&&!saved.Codex&&saved.Network=="missing-interface","Choices survive close");
            var reopened=new MonitorWindow(new MonitorSource(true),start:false,store:new PreviewSettingsStore(path));
            Check(reopened.RequestedThemeVariant==ThemeVariant.Dark&&reopened.Width==saved.Width,"Choices survive reopen");reopened.Show();reopened.Close();
            Console.WriteLine("PASS isolated preview settings: normalization, unknown fields, atomic save, error preservation, theme, quota and missing interface");
        } finally {
            Check(Path.GetFullPath(directory).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase),"Fixture cleanup boundary");
            Directory.Delete(directory,true);
        }
    }
}
