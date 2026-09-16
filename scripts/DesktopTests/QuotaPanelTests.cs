using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse;
using HardwarePulse.Desktop;

static class QuotaPanelTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static IEnumerable<TextBlock> Text(Window window)=>window.GetVisualDescendants().OfType<TextBlock>();
    static void Until(Func<bool> ready,string message) {
        var deadline=DateTime.UtcNow.AddSeconds(5);
        while(!ready()&&DateTime.UtcNow<deadline){using var slice=new CancellationTokenSource(TimeSpan.FromMilliseconds(20));Dispatcher.UIThread.MainLoop(slice.Token);}
        Check(ready(),message);
    }
    static QuotaReading Result() {
        var main=new QuotaWindow{Label="Weekly",Remaining=54,Reset=DateTimeOffset.UtcNow.AddDays(3)};
        return new(){Provider="Codex",Status="Live",Observed=DateTimeOffset.UtcNow,Windows=[main],AllWindows=[main,new(){Label="Additional model pool · 5-hour",Remaining=72.5},new(){Label="Unknown availability",Remaining=null}]};
    }
    public static void Run(string? output) {
        int reads=0;using var canceled=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var panel=new CodexQuotaPanel(false,cancel=>{
            int call=Interlocked.Increment(ref reads);
            if(call==2)return new(){Provider="Codex",Status="Login required"};
            if(call==3){using var registration=cancel.Register(()=>canceled.Set());release.Wait(TimeSpan.FromSeconds(5));return new(){Provider="Codex",Status="STALE RESULT"};}
            return Result();
        });
        var window=new Window{Width=360,Height=850,Content=new ScrollViewer{Content=panel}};
        window.Show();Dispatcher.UIThread.RunJobs();
        var enable=window.GetVisualDescendants().OfType<CheckBox>().Single();
        var refresh=window.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="RefreshCodexQuota");
        Check(reads==0&&!refresh.IsEnabled,"Disabled quota must not read credentials");
        enable.Focus();window.KeyPress(Key.Space,RawInputModifiers.None,PhysicalKey.Space," ");window.KeyRelease(Key.Space,RawInputModifiers.None,PhysicalKey.Space," ");
        Until(()=>Text(window).Any(x=>x.Text=="72.5% left"),"AllWindows must include additional quota pools");
        Check(Text(window).Any(x=>x.Text=="—"),"Unknown quota is unavailable, not zero");
        Check(window.GetVisualDescendants().OfType<ProgressBar>().Count()==2,"Only known quota has bars");
        if(output!=null){using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"codex-quota.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
        refresh.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Until(()=>Text(window).Any(x=>x.Text?.StartsWith("Login required")==true),"Login failure visible");
        Check(!window.GetVisualDescendants().OfType<ProgressBar>().Any(),"Failure clears stale quota");
        refresh.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Until(()=>Volatile.Read(ref reads)==3,"Pending refresh started");
        enable.IsChecked=false;Until(()=>canceled.IsSet,"Disable cancels request");
        Check(Text(window).Any(x=>x.Text=="Off")&&!refresh.IsEnabled,"Disabled UI clears readings");
        release.Set();enable.IsChecked=true;
        Until(()=>Volatile.Read(ref reads)==4&&Text(window).Any(x=>x.Text=="72.5% left"),"Re-enable recovers after canceled request");
        Check(!Text(window).Any(x=>x.Text=="STALE RESULT"),"Prior generation must not publish");
        panel.Dispose();window.Close();

        using var started=new ManualResetEventSlim();using var stopped=new ManualResetEventSlim();
        var closing=new CodexQuotaPanel(false,cancel=>{started.Set();cancel.WaitHandle.WaitOne(TimeSpan.FromSeconds(5));if(cancel.IsCancellationRequested)stopped.Set();cancel.ThrowIfCancellationRequested();return Result();});
        var closingWindow=new Window{Content=closing};closingWindow.Show();
        closingWindow.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked=true;
        Until(()=>started.IsSet,"Close test request started");
        closing.Dispose();closingWindow.Close();Until(()=>stopped.IsSet,"Dispose cancels active quota IO");
        int claudeReads=0;
        using var claude=new CodexQuotaPanel(false,_=>{claudeReads++;var result=Result();result.Provider="Claude";return result;},provider:"Claude");
        var claudeWindow=new Window{Width=360,Height=850,Content=claude};claudeWindow.Show();Dispatcher.UIThread.RunJobs();
        Check(claudeReads==0,"Claude opt-out must not access credentials");claude.QuotaEnabled=true;
        Until(()=>Text(claudeWindow).Any(x=>x.Text=="72.5% left"),"Claude renders all quota windows");
        var floating=new FloatingMonitorWindow(new UiLanguage("en"));floating.Show();
        floating.PresentQuota(Result());floating.PresentQuota(claude.CurrentReading,"Claude");Dispatcher.UIThread.RunJobs();
        Check(Text(floating).Any(x=>x.Text=="Codex · Live")&&Text(floating).Any(x=>x.Text=="Claude · Live"),"Floating monitor shows both providers");
        floating.PresentQuota(null,"Claude");Dispatcher.UIThread.RunJobs();
        Check(Text(floating).Any(x=>x.Text=="Codex · Live")&&!Text(floating).Any(x=>x.Text=="Claude · Live"),"Disabling Claude preserves Codex");
        floating.Close();claudeWindow.Close();
        int antigravityReads=0;
        using var antigravity=new CodexQuotaPanel(false,_=>new(){Provider="Antigravity",Status=Interlocked.Increment(ref antigravityReads)==1?"Open Antigravity to read quota":"Login required"},provider:"Antigravity");
        var missing=new Window{Content=antigravity};missing.Show();antigravity.QuotaEnabled=true;
        Until(()=>Text(missing).Any(x=>x.Text=="Open Antigravity to read quota"),"Missing Antigravity is explicit");
        Check(!missing.GetVisualDescendants().OfType<ProgressBar>().Any(),"Missing source does not fabricate quota");
        missing.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="RefreshAntigravityQuota").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Until(()=>Text(missing).Any(x=>x.Text=="Login required · Open Antigravity to read quota"),"Expired source directs the user to Antigravity");missing.Close();
        var owner=new MonitorWindow(new MonitorSource(true),start:false);owner.Show();
        var panels=owner.GetVisualDescendants().OfType<CodexQuotaPanel>().ToDictionary(x=>x.Provider);
        foreach(var provider in QuotaSession.Providers)panels[provider].QuotaEnabled=true;
        owner.OpenFloatingMonitor();
        Until(()=>QuotaSession.Providers.All(provider=>Text(owner.FloatingMonitor!).Any(x=>x.Text==provider+" · Live")),"All providers reach floating monitor from owner");
        Check(panels["Antigravity"].CurrentReading!.Provider=="Antigravity","Correct Antigravity snapshot identity");
        if(output!=null){using var frame=owner.FloatingMonitor!.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"antigravity-floating.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
        owner.FloatingMonitor!.Close();owner.OpenFloatingMonitor();Dispatcher.UIThread.RunJobs();
        Check(Text(owner.FloatingMonitor!).Any(x=>x.Text=="Antigravity · Live"),"Reopen reuses current provider snapshot");
        panels["Antigravity"].QuotaEnabled=false;Dispatcher.UIThread.RunJobs();
        Check(!Text(owner.FloatingMonitor!).Any(x=>x.Text=="Antigravity · Live")&&Text(owner.FloatingMonitor!).Any(x=>x.Text=="Codex · Live")&&Text(owner.FloatingMonitor!).Any(x=>x.Text=="Claude · Live"),"Antigravity opt-out preserves both other providers");
        owner.Close();
        Console.WriteLine("PASS Codex UI opt-in, complete windows, missing values, retry, stale result rejection and cancellation; synthetic reader only");
    }
}
