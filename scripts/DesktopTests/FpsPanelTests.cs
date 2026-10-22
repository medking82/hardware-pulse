using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse;
using HardwarePulse.Desktop;

static class FpsPanelTests {
    sealed class Source : IDesktopFpsSource {
        public int Calls,Disposals,Resets,PollThread;
        public bool Hold;
        public readonly ManualResetEventSlim Entered=new(),Release=new();
        public FrameMetrics Poll(string target){PollThread=Environment.CurrentManagedThreadId;Interlocked.Increment(ref Calls);if(Hold){Entered.Set();Release.Wait(TimeSpan.FromSeconds(5));}return new(){Ready=true,Current=60,Average=58,Minimum=30,Low=double.NaN,Status="Live"};}
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
        void Phase(string stage){Console.WriteLine($"FPS_PHASE {stage} uiThread={Environment.CurrentManagedThreadId} pollThread={source.PollThread} creates={creates} calls={source.Calls} disposals={source.Disposals} entered={source.Entered.IsSet} released={source.Release.IsSet} worker={panel.Sampling.Status}");Console.Out.Flush();}
        Phase("show-panel");var window=new Window{Content=panel,Width=360,Height=500};window.Show();
        Phase("show-desktop");var desktop=new FloatingMonitorWindow(language,new PreviewSettings(),()=>{});desktop.Show();panel.ReadingChanged+=desktop.PresentFps;
        Check(creates==0&&discoveries==0,"Disabled FPS accesses source");
        Phase("enable");panel.Enabled=true;Phase("wait-reading");Until(()=>panel.Current.Current=="60","FPS reading never arrived");Phase("reading");
        var fpsRow=desktop.GetVisualDescendants().OfType<DesktopFpsRow>().Single();
        Check(panel.Current.Low=="—"&&fpsRow.GetVisualDescendants().OfType<TextBlock>().Single(x=>x.Name=="FpsMetric0").Text=="60"&&fpsRow.GetVisualDescendants().OfType<TextBlock>().Single(x=>x.Name=="FpsMetric1").Text=="58"&&fpsRow.GetVisualDescendants().OfType<TextBlock>().Single(x=>x.Name=="FpsMetric3").Text=="—","Desktop snapshot mismatch or invented low");
        if(output!=null){Dispatcher.UIThread.RunJobs();using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"fps-panel-360.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
        var buttons=panel.GetVisualDescendants().OfType<Button>().ToArray();
        Phase("reset");buttons.Single(x=>x.Name=="ResetFps").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(panel.Current.Current=="—","Reset retains stale FPS");Until(()=>source.Resets==1,"Reset did not reach source");
        Phase("refresh-targets");buttons.Single(x=>x.Name=="RefreshFpsTargets").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var picker=panel.GetVisualDescendants().OfType<ComboBox>().Single();
        Until(()=>picker.ItemCount==2,"App discovery did not finish");picker.SelectedIndex=1;
        Check(panel.Target=="FixtureGame"&&panel.Current.Current=="—","Target change must clear previous metrics");
        Phase("hold-poll");source.Hold=true;Until(()=>source.Entered.IsSet,"Pending poll not entered");Phase("disable-held-poll");panel.Enabled=false;
        Check(!panel.Current.Enabled&&!desktop.GetVisualDescendants().OfType<DesktopFpsRow>().Any(),"Disable retains displayed FPS");
        Phase("re-enable");panel.Enabled=true;Phase("drain-re-enabled-jobs");Dispatcher.UIThread.RunJobs();Phase("drained-re-enabled-jobs");Check(creates==1,"Re-enable overlaps pending source");
        Phase("release-poll");source.Release.Set();Until(()=>creates==2&&panel.Current.Current=="60","Re-enable did not resume after disposal");Phase("resumed");
        Check(source.Disposals==1&&second.Disposals==0,"Prior source lifetime failure");
        Phase("disable");panel.Enabled=false;Until(()=>panel.Sampling.IsCompleted,"Disable did not dispose session");Phase("disposed-worker");
        Check(second.Disposals==1&&panel.Current.Current=="—","Late result or source lifetime failure");
        Phase("close-windows");panel.Dispose();desktop.Close();window.Close();Phase("closed-windows");
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
    }
}
