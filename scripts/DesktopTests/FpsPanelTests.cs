using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse;
using HardwarePulse.Desktop;

static class FpsPanelTests {
    sealed class Source : IDesktopFpsSource {
        public int Calls,Disposals,Resets;
        public bool Hold;
        public readonly ManualResetEventSlim Entered=new(),Release=new();
        public FrameMetrics Poll(string target){Interlocked.Increment(ref Calls);if(Hold){Entered.Set();Release.Wait(TimeSpan.FromSeconds(5));}return new(){Ready=true,Current=60,Average=58,Minimum=30,Low=double.NaN,Status="Live"};}
        public void Reset()=>Interlocked.Increment(ref Resets);
        public void Dispose()=>Interlocked.Increment(ref Disposals);
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Until(Func<bool> done,string message) {
        var deadline=DateTime.UtcNow.AddSeconds(8);
        while(!done()&&DateTime.UtcNow<deadline){using var slice=new CancellationTokenSource(TimeSpan.FromMilliseconds(20));Dispatcher.UIThread.MainLoop(slice.Token);}
        Check(done(),message);
    }
    public static void Run(string? output=null) {
        var language=new UiLanguage("en");var source=new Source();var second=new Source();int creates=0,discoveries=0;
        var panel=new FpsPanel(language,factory:()=>Interlocked.Increment(ref creates)==1?source:second,discover:()=>{discoveries++;return ["FixtureGame"];});
        var window=new Window{Content=panel,Width=360,Height=500};window.Show();
        var desktop=new FloatingMonitorWindow(language);desktop.Show();panel.ReadingChanged+=reading=>desktop.Present(new MonitorSnapshot("1%","1 GiB","—","—",true,true){Fps=reading});
        Check(creates==0&&discoveries==0,"Disabled FPS accesses source");
        panel.Enabled=true;Until(()=>panel.Current.Current=="60","FPS reading never arrived");
        Check(panel.Current.Low=="—"&&desktop.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="60 / 58 / 30"),"Desktop snapshot mismatch or invented low");
        if(output!=null){Dispatcher.UIThread.RunJobs();using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"fps-panel-360.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
        var buttons=panel.GetVisualDescendants().OfType<Button>().ToArray();
        buttons.Single(x=>x.Name=="ResetFps").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(panel.Current.Current=="—","Reset retains stale FPS");Until(()=>source.Resets==1,"Reset did not reach source");
        buttons.Single(x=>x.Name=="RefreshFpsTargets").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var picker=panel.GetVisualDescendants().OfType<ComboBox>().Single();
        Until(()=>picker.ItemCount==2,"App discovery did not finish");picker.SelectedIndex=1;
        Check(panel.Target=="FixtureGame"&&panel.Current.Current=="—","Target change must clear previous metrics");
        source.Hold=true;Until(()=>source.Entered.IsSet,"Pending poll not entered");panel.Enabled=false;
        Check(!panel.Current.Enabled&&!desktop.GetVisualDescendants().OfType<Grid>().Any(x=>x.Name=="DesktopMetricfps"),"Disable retains displayed FPS");
        panel.Enabled=true;Dispatcher.UIThread.RunJobs();Check(creates==1,"Re-enable overlaps pending source");
        source.Release.Set();Until(()=>creates==2&&panel.Current.Current=="60","Re-enable did not resume after disposal");
        Check(source.Disposals==1&&second.Disposals==0,"Prior source lifetime failure");
        panel.Enabled=false;Until(()=>panel.Sampling.IsCompleted,"Disable did not dispose session");
        Check(second.Disposals==1&&panel.Current.Current=="—","Late result or source lifetime failure");
        panel.Dispose();desktop.Close();window.Close();
        string directory=Directory.CreateTempSubdirectory("pulse-fps-settings-").FullName;
        try {
            string path=Path.Combine(directory,"settings.json");var store=new PreviewSettingsStore(path);var settings=store.Load();Check(!settings.Fps,"New profile enables capture");settings.Fps=true;settings.FpsTarget="FixtureGame";Check(store.Save(settings),"FPS save failed");
            var saved=new PreviewSettingsStore(path).Load();Check(saved.Fps&&saved.FpsTarget=="FixtureGame","FPS preference roundtrip failed");
            File.WriteAllText(path,"{\"fpsTarget\":\"../arbitrary.exe\"}");Check(new PreviewSettingsStore(path).Load().FpsTarget=="","Path accepted as process name");
        }finally{Directory.Delete(directory,true);}
        if(OperatingSystem.IsWindows()) {
            using var self=System.Diagnostics.Process.GetCurrentProcess();
            Check(FpsProtocol.Peer((uint)self.Id,self.MainModule!.FileName)&&!FpsProtocol.Peer((uint)self.Id,"C:\\wrong.exe"),"Modern FPS peer binding");
            Check(FpsProtocol.Target(self.Id,self.StartTime.ToUniversalTime().Ticks)&&!FpsProtocol.Target(self.Id,self.StartTime.ToUniversalTime().Ticks+1),"Modern FPS process birth");
            var metrics=FpsProtocol.Metrics(FpsProtocol.Response(new(){Ready=true,Current=60,Low=double.NaN,Status="Live"}));Check(metrics.Ready&&metrics.Current==60&&double.IsNaN(metrics.Low),"Modern fixed protocol changed");
        }
        Console.WriteLine("PASS shared FPS: opt-in, common snapshot, Reset, targets, cancellation, disposal, persistence and Windows protocol identity");
        Owner(output);
    }
    public static void Owner(string? output=null) {
        var source=new Source();int creates=0;
        var monitor=new MonitorSource(true);
        var owner=new MonitorWindow(monitor,start:false,fpsFactory:()=>{creates++;return source;}){Width=360,Height=650};
        owner.Show();owner.Present(monitor.Poll(null));Dispatcher.UIThread.RunJobs();
        var panel=owner.GetVisualDescendants().OfType<FpsPanel>().Single();
        var quick=owner.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>().Single(x=>x.Name=="FpsQuick");
        Check(!panel.IsVisible&&creates==0,"Disabled owner has no FPS card or source");
        quick.IsChecked=true;quick.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Until(()=>panel.Current.Current=="60","Quick toggle enables the shared FPS session");
        owner.OpenFloatingMonitor();Dispatcher.UIThread.RunJobs();
        var desktop=owner.FloatingMonitor!;
        Check(creates==1&&desktop.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="60 / 58 / 30"),"Opening Desktop reuses current FPS without another source");
        if(output!=null){panel.BringIntoView();Dispatcher.UIThread.RunJobs();using var frame=owner.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"fps-monitor-360.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
        owner.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="OpenSettings").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Dispatcher.UIThread.RunJobs();
        var tabs=owner.GetVisualDescendants().OfType<TabControl>().Single(x=>x.Name=="SettingsTabs");tabs.SelectedIndex=5;Dispatcher.UIThread.RunJobs();
        var enabled=owner.GetVisualDescendants().OfType<CheckBox>().Single(x=>x.Name=="FpsEnabled");
        Check(enabled.IsChecked==true,"Settings reflects the quick toggle");
        if(output!=null){owner.Width=240;owner.FontSize=16;Dispatcher.UIThread.RunJobs();using var frame=owner.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"fps-settings-240.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
        enabled.IsChecked=false;Dispatcher.UIThread.RunJobs();
        Check(quick.IsChecked==false&&!panel.IsVisible&&!desktop.GetVisualDescendants().OfType<Grid>().Any(x=>x.Name=="DesktopMetricfps"),"Settings disable clears both views and quick toggle");
        Until(()=>source.Disposals==1,"Owner disable disposes source");
        owner.Close();
        var closingSource=new Source{Hold=true};
        var closingOwner=new MonitorWindow(new MonitorSource(true),start:false,fpsFactory:()=>closingSource);
        closingOwner.Show();
        var closingPanel=closingOwner.GetVisualDescendants().OfType<FpsPanel>().Single();closingPanel.Enabled=true;
        Until(()=>closingSource.Entered.IsSet,"Shutdown fixture has an active poll");
        closingOwner.Close();closingSource.Release.Set();
        Until(()=>closingPanel.Sampling.IsCompleted,"Owner shutdown completes the pending FPS session");
        Check(closingSource.Disposals==1&&!closingPanel.Current.Enabled&&closingPanel.Current.Current=="—","Shutdown disposes exactly once and rejects pending result");
        Console.WriteLine("PASS FPS owner: quick/Settings synchronization, one source, Desktop snapshot and disable cleanup");
    }
}
