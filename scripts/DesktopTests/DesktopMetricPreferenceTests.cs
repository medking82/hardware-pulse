using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class DesktopMetricPreferenceTests {
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
    public static void Run(string? output=null) {
        string directory=Directory.CreateTempSubdirectory("pulse-desktop-metrics-").FullName;
        MonitorWindow? owner=null;
        try {
            string path=Path.Combine(directory,"settings.json");
            File.WriteAllText(path,"{\"desktopOrder\":[\"Memory\",\"Memory\",7,\"invalid\"],\"desktopVisible\":{\"CPU\":false,\"future\":false}}");
            var settings=new PreviewSettingsStore(path).Load();
            Check(settings.DesktopOrder[0]=="Memory"&&settings.DesktopOrder.Count==PreviewSettings.DesktopKeys.Length&&!settings.DesktopVisible["CPU"],"Normalize metric order and visibility");
            owner=new MonitorWindow(new MonitorSource(true),start:false,store:new PreviewSettingsStore(path));owner.Show();
            var sample=new MonitorSnapshot("21%","4 / 16 GiB","1 KiB/s","2 KiB/s",true,true);owner.Present(sample);owner.OpenFloatingMonitor();
            var desktop=owner.FloatingMonitor!;
            Grid Row(string key)=>desktop.GetVisualDescendants().OfType<Grid>().Single(x=>x.Name=="DesktopMetric"+key);
            var cpu=Row("CPU");Check(!cpu.IsVisible&&Grid.GetRow(Row("Memory"))==0,"Hidden Desktop CPU does not leave a layout gap");
            owner.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="OpenSettings").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Dispatcher.UIThread.RunJobs();
            owner.GetVisualDescendants().OfType<TabControl>().Single(x=>x.Name=="SettingsTabs").SelectedIndex=2;Dispatcher.UIThread.RunJobs();
            CheckBox Show(string key)=>owner.GetVisualDescendants().OfType<CheckBox>().Single(x=>x.Name=="ShowDesktop"+key);
            Show("CPU").IsChecked=true;owner.Present(sample);Dispatcher.UIThread.RunJobs();
            Check(ReferenceEquals(cpu,Row("CPU"))&&cpu.IsVisible,"Visibility update and polling reuse metric controls");
            owner.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="MoveDesktopDownMemory").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Dispatcher.UIThread.RunJobs();
            Check(Grid.GetRow(cpu)==0&&Grid.GetRow(Row("Memory"))==1,"Metric ordering applies to existing Desktop");
            if(output!=null){owner.Width=360;owner.Height=650;Dispatcher.UIThread.RunJobs();Show("CPU").BringIntoView();Dispatcher.UIThread.RunJobs();using var frame=owner.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"desktop-metric-settings.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
            foreach(string key in PreviewSettings.DesktopKeys)Show(key).IsChecked=false;Dispatcher.UIThread.RunJobs();
            Check(desktop.GetVisualDescendants().OfType<TextBlock>().Single(x=>x.Name=="DesktopEmpty").IsVisible,"All-hidden editor has recovery guidance");
            Show("Memory").IsChecked=true;owner.Present(sample);Dispatcher.UIThread.RunJobs();
            Check(Row("Memory").IsVisible&&Grid.GetRow(Row("Memory"))==0&&!cpu.IsVisible,"Re-enable after all-hidden recovers compact layout");
            owner.Close();owner=null;
            var saved=new PreviewSettingsStore(path).Load();
            Check(saved.DesktopOrder[0]=="CPU"&&saved.DesktopVisible["Memory"]&&!saved.DesktopVisible["CPU"]&&!saved.DesktopVisible["future"],"Metric preferences persist and preserve unknown visibility entries");
            Check(saved.HiddenCards.Count==0&&saved.CardOrder.SequenceEqual(PreviewSettings.CardKeys),"Desktop preferences do not change App cards");
            Console.WriteLine("PASS Desktop metric preferences: normalization, visibility, order, reuse, empty recovery and independent persistence");
        } finally {owner?.Close();Directory.Delete(directory,true);}
    }
}
