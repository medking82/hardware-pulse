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
            var fresh=new PreviewSettingsStore(path).Load();
            Check(fresh.Width==280&&fresh.Height==650,"New profiles retain original WPF widget geometry");
            File.WriteAllText(path,"{\"schema\":1,\"width\":-999,\"height\":99999,\"theme\":\"bad\",\"network\":\"missing-interface\",\"codex\":true,\"future\":{\"keep\":7}}");
            var store=new PreviewSettingsStore(path);var value=store.Load();
            Check(value.Width==240&&value.Height==1600&&value.Theme=="System"&&value.Codex,"Settings normalize known values");
            value.Width=700;value.Height=650;value.Details=true;Check(store.Save(value),"Atomic save");
            Check(new PreviewSettingsStore(path).Load().Details,"Details preference roundtrip");
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
            var fixtureSource=new MonitorSource(true);
            // Manual readings keep preference assertions independent of the sampling timer.
            var window=new MonitorWindow(fixtureSource,start:false,store:new PreviewSettingsStore(path));window.Show();window.Present(fixtureSource.Poll(null));window.PresentInterfaces(fixtureSource.Interfaces());
            var main=window.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="OpenSettings");main.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            var groups=window.GetVisualDescendants().OfType<TabControl>().Single(x=>x.Name=="SettingsTabs");
            var network=window.GetVisualDescendants().OfType<ComboBox>().Single(x=>x.Name=="NetworkInterface");
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
            window.GetVisualDescendants().OfType<Slider>().Single(x=>x.Name=="AppFontSize").Value=16;
            var pin=window.GetVisualDescendants().OfType<CheckBox>().Single(x=>x.Name=="AppTopmost");pin.IsChecked=true;
            var locked=window.GetVisualDescendants().OfType<CheckBox>().Single(x=>x.Name=="AppLockPosition");locked.IsChecked=true;
            Check(window.Topmost&&!window.CanResize,"Window preferences apply immediately");
            Check(window.GetVisualDescendants().OfType<Border>().Where(x=>x.Name?.StartsWith("Resize")==true).All(x=>!x.IsVisible),"Lock removes all resize regions without disabling Settings");
            Until(()=>new PreviewSettingsStore(path).Load().Theme=="Dark");
            groups.SelectedIndex=2;Dispatcher.UIThread.RunJobs();
            theme.SelectedItem="Light";window.OpenFloatingMonitor();var desktop=window.FloatingMonitor!;
            Check(desktop.RequestedThemeVariant==ThemeVariant.Dark,"Desktop editor keeps dark theme when opened from Light App");
            theme.SelectedItem="System";Check(desktop.RequestedThemeVariant==ThemeVariant.Dark,"App theme changes do not override dark Desktop editor");theme.SelectedItem="Dark";
            Until(()=>desktop.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text?.StartsWith("72.5% left")==true));
            window.GetVisualDescendants().OfType<Slider>().Single(x=>x.Name=="DesktopFontSize").Value=20;
            window.GetVisualDescendants().OfType<Slider>().Single(x=>x.Name=="DesktopSpacing").Value=24;
            window.GetVisualDescendants().OfType<ComboBox>().Single(x=>x.Name=="DesktopColumns").SelectedIndex=2;
            var screen=desktop.Screens.ScreenFromWindow(desktop)??desktop.Screens.Primary;
            double desired=20*24*2+10+34;
            double expectedWidth=screen==null?desired:Math.Min(desired,Math.Max(desktop.MinWidth,screen.WorkingArea.Width/screen.Scaling-32));
            Check(Math.Abs(desktop.Width-expectedWidth)<1,"Selecting explicit Desktop columns expands to original cell width within working area");
            var desktopPin=window.GetVisualDescendants().OfType<CheckBox>().Single(x=>x.Name=="DesktopAlwaysOnTop");desktopPin.IsChecked=true;
            Check(desktop.FontSize==20&&desktop.Topmost&&window.FontSize==16,"Desktop preferences apply independently of App");
            desktop.GetVisualDescendants().OfType<CheckBox>().Single(x=>x.Name=="FloatingTopmost").IsChecked=false;
            Check(desktopPin.IsChecked==false&&window.Topmost,"Desktop toolbar syncs Settings without changing App topmost");
            desktopPin.IsChecked=true;
            var background=window.GetVisualDescendants().OfType<Slider>().Single(x=>x.Name=="DesktopBackgroundOpacity");
            var overlay=window.GetVisualDescendants().OfType<Slider>().Single(x=>x.Name=="DesktopOverlayOpacity");
            Check(!background.IsEnabled&&overlay.IsEnabled,"Topmost selects only its own opacity control");
            overlay.Value=72;window.GetVisualDescendants().OfType<Slider>().Single(x=>x.Name=="DesktopTextOpacity").Value=80;
            desktopPin.IsChecked=false;Check(background.IsEnabled&&!overlay.IsEnabled,"Desktop restores its background control");background.Value=88;
            desktopPin.IsChecked=true;
            if(output!=null){Dispatcher.UIThread.RunJobs();using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"desktop-settings.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
            groups.SelectedIndex=3;Dispatcher.UIThread.RunJobs();
            var unit=window.GetVisualDescendants().OfType<ComboBox>().Single(x=>x.Name=="NetworkUnit");
            window.Present(new MonitorSnapshot("10%","1 GiB","old download","old upload",true,true){DownloadBytes=125000,UploadBytes=250000,PeakDownloadBytes=1000000,PeakUploadBytes=2000000});
            unit.SelectedIndex=3;Dispatcher.UIThread.RunJobs();
            Check(desktop.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="1 Mbit/s"),"Unit change immediately updates open Desktop");
            window.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="Back").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));Dispatcher.UIThread.RunJobs();
            Check(window.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="1 Mbit/s"),"Monitor and Desktop use same selected unit");
            window.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="SessionMax").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));Dispatcher.UIThread.RunJobs();
            Check(window.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="8 Mbit/s")&&desktop.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="8 Mbit/s"),"Session Max uses raw peak in selected unit in both views");
            window.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="OpenSettings").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));Dispatcher.UIThread.RunJobs();
            groups.SelectedIndex=4;Dispatcher.UIThread.RunJobs();
            var quota=window.GetVisualDescendants().OfType<CheckBox>().Single(x=>x.Name=="EnableCodexQuota");Check(quota.IsChecked==true,"Quota choice restored with explicit demo reader");quota.IsChecked=false;
            Check(!desktop.GetVisualDescendants().OfType<Grid>().Any(x=>x.Name?.StartsWith("DesktopMetricquotaCodex")==true),"Owner propagates disabled quota to Desktop");
            if(output!=null){window.Width=360;Dispatcher.UIThread.RunJobs();using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"settings-360.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
            window.Close();Until(()=>window.Sampling.IsCompleted);
            var saved=new PreviewSettingsStore(path).Load();Check(saved.Theme=="Dark"&&!saved.Codex&&saved.Network=="missing-interface","Choices survive close");
            Check(saved.Topmost&&saved.LockPosition,"Window preferences survive close");
            Check(saved.FontSize==16,"Font size persists");
            Check(saved.NetworkUnit=="Mbit/s","Network unit persists");
            Check(saved.DesktopBackgroundOpacity==88&&saved.DesktopOverlayOpacity==72&&saved.DesktopTextOpacity==80,"Independent Desktop opacity preferences persist");
            Check(saved.DesktopFontSize==20&&saved.DesktopSpacing==24&&saved.DesktopColumns==2&&saved.DesktopTopmost,"Desktop preferences persist");
            var reopened=new MonitorWindow(new MonitorSource(true),start:false,store:new PreviewSettingsStore(path));
            Check(reopened.RequestedThemeVariant==ThemeVariant.Dark&&reopened.Width==saved.Width,"Choices survive reopen");reopened.Show();reopened.OpenFloatingMonitor();Check(reopened.FloatingMonitor!.FontSize==20&&reopened.FloatingMonitor.Topmost,"Desktop preferences restored on new view");reopened.Close();
            Check(reopened.Topmost&&!reopened.CanResize,"Window preferences restored before interaction");
            Console.WriteLine("PASS isolated preview settings: normalization, unknown fields, atomic save, error preservation, theme, quota and missing interface");
        } finally {
            Check(Path.GetFullPath(directory).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase),"Fixture cleanup boundary");
            Directory.Delete(directory,true);
        }
    }
}
