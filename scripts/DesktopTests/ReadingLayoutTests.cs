using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class ReadingLayoutTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static Grid Entry(ReadingLayoutEditor editor,string id)=>editor.Children.Cast<Grid>().Single(x=>(string?)x.Tag==id);
    static void Click(Grid row,string name)=>row.Children.OfType<Button>().Single(x=>x.Name==name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    public static void Run(string? output=null,bool native=false) {
        string directory=Directory.CreateTempSubdirectory("pulse-layout-").FullName;
        try {
            string path=Path.Combine(directory,"settings.json");
            File.WriteAllText(path,"{\"future\":{\"keep\":true},\"desktopLayout\":{\"futureStyle\":{\"keep\":true},\"Order\":[\"hardware/fan\",\"Memory\",\"missing\"],\"Hidden\":[\"Download\",\"hardware/fan\"]}}");
            var store=new PreviewSettingsStore(path);
            var owner=new MonitorWindow(new MonitorSource(true),start:false,store:store);owner.Show();
            var snapshot=new MonitorSnapshot("20%","4 GiB","1 KiB/s","2 KiB/s",true,true){WindowsHardwareSupported=true,WindowsHardware=[new("fan","Fixture fan","1200 RPM")]};
            owner.Present(snapshot);owner.OpenFloatingMonitor();
            var floating=owner.FloatingMonitor!;
            var panel=floating.GetVisualDescendants().OfType<AdaptiveReadingsPanel>().Single(x=>x.Name=="DesktopReadings");
            Check((string?)panel.Children[0].Tag=="hardware/fan"&&!panel.Children[0].IsVisible,"Stored order/hidden hardware lost");
            Check(panel.Children.Single(x=>(string?)x.Tag=="Memory").IsVisible,"Default display lost");
            var main=owner.GetVisualDescendants().OfType<TabControl>().Single(x=>x.Name=="MainTabs");main.SelectedIndex=1;Dispatcher.UIThread.RunJobs();
            var tabs=owner.GetVisualDescendants().OfType<TabControl>().Single(x=>x.Name=="SettingsTabs");tabs.SelectedIndex=4;Dispatcher.UIThread.RunJobs();
            var editors=owner.GetVisualDescendants().OfType<ReadingLayoutEditor>().ToArray();
            var cards=editors.Single(x=>x.Name=="CardLayoutEditor");var desktop=editors.Single(x=>x.Name=="DesktopLayoutEditor");
            var fan=Entry(desktop,"hardware/fan");((CheckBox)fan.Children[0]).IsChecked=true;
            var retained=panel.Children.Single(x=>(string?)x.Tag=="hardware/fan");Check(retained.IsVisible,"Show metric does not apply live");
            Click(fan,"MoveReadingDown");Check((string?)panel.Children[0].Tag=="Memory","Cross-group metric reorder failed");
            var cpu=Entry(cards,"CPU");((CheckBox)cpu.Children[0]).IsChecked=false;
            Click(Entry(cards,"Memory"),"MoveReadingUp");
            main.SelectedIndex=0;Dispatcher.UIThread.RunJobs();
            var cardPanel=owner.GetVisualDescendants().OfType<AdaptiveReadingsPanel>().Single(x=>x.Name=="MonitorCards");
            Check(cardPanel.Children[0].GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="Memory"),"Card reorder failed");
            var hiddenCpu=cardPanel.Children.Single(x=>x.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="CPU"));Check(!hiddenCpu.IsVisible,"Hide card failed");
            owner.Present(snapshot with {Cpu="35%",WindowsHardware=[new("fan","Fixture fan","1500 RPM")]});
            Check(!hiddenCpu.IsVisible&&hiddenCpu.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="35%"),"Hidden card lost updates or became visible");
            Check(ReferenceEquals(retained,panel.Children.Single(x=>(string?)x.Tag=="hardware/fan"))&&retained.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="1500 RPM"),"Live row identity/value lost");
            owner.Present(snapshot with {WindowsHardware=[]});owner.Present(snapshot);
            Check((string?)panel.Children[0].Tag=="Memory"&&panel.Children.Single(x=>(string?)x.Tag=="hardware/fan").IsVisible,"Returning device loses preference");
            owner.Language.Select("zh-CN");Check((string?)panel.Children[0].Tag=="Memory","Localization changed stable order");owner.Language.Select("en");
            Check(!owner.GetVisualDescendants().OfType<FpsPanel>().Single().Enabled&&owner.GetVisualDescendants().OfType<CodexQuotaPanel>().All(x=>!x.QuotaEnabled),"Layout enabled optional acquisition");
            main.SelectedIndex=1;Dispatcher.UIThread.RunJobs();tabs.SelectedIndex=4;owner.Width=360;owner.Height=850;Dispatcher.UIThread.RunJobs();
            if(native){var until=DateTime.UtcNow.AddMilliseconds(500);while(DateTime.UtcNow<until){using var slice=new CancellationTokenSource(TimeSpan.FromMilliseconds(20));Dispatcher.UIThread.MainLoop(slice.Token);}}
            foreach(var row in desktop.Children.Cast<Grid>()) {
                Check(((CheckBox)row.Children[0]).IsEnabled,"Visibility toggle accidentally disabled");
                Check(row.Children[1].Bounds.Width>=100&&row.Children[3].Bounds.Right<=row.Bounds.Width+1,"Narrow editor clips label/actions");
            }
            if(output!=null){using var frame=owner.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"layout-settings-360.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
            owner.Close();var loaded=new PreviewSettingsStore(path).Load();
            Check(loaded.Cards.Hidden.Contains("CPU")&&loaded.Cards.Arrange(["CPU","Memory"])[0]=="Memory","Card preference roundtrip failed");
            Check(loaded.DesktopRows.Order.Contains("missing")&&loaded.DesktopRows.Hidden.Contains("Download")&&!loaded.DesktopRows.Hidden.Contains("hardware/fan"),"Missing/future metric preferences discarded");
            Check(File.ReadAllText(path).Contains("\"future\"")&&loaded.DesktopRows.Extra.ContainsKey("futureStyle"),"Unknown root or nested setting lost");
            var again=new MonitorWindow(new MonitorSource(true),start:false,store:new PreviewSettingsStore(path));again.Show();again.Present(snapshot);again.OpenFloatingMonitor();
            var restored=again.FloatingMonitor!.GetVisualDescendants().OfType<AdaptiveReadingsPanel>().Single(x=>x.Name=="DesktopReadings");
            Check((string?)restored.Children[0].Tag=="Memory"&&!restored.Children.Single(x=>(string?)x.Tag=="Download").IsVisible,"Restart did not restore Desktop layout");again.Close();
        }finally{Directory.Delete(directory,true);}
        Console.WriteLine("PASS reading layouts: independent visibility/order, retained hidden updates, hotplug, localization, narrow editor, restart and no optional acquisition");
    }
}
