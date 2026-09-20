using Avalonia;
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
    static QuotaReading Result(string provider) {
        var main=new QuotaWindow{Label="Weekly",Remaining=54,Reset=DateTimeOffset.UtcNow.AddDays(3)};
        return new(){Provider=provider,Status="Live",Observed=DateTimeOffset.UtcNow,Windows=[main],AllWindows=[main,new(){Label="Additional model pool · 5-hour",Remaining=72.5},new(){Label="Unknown availability",Remaining=null}]};
    }
    public static void Run(string? output,string provider="Codex") {
        MonitorVisibility(provider);
        int reads=0;using var canceled=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var panel=new QuotaPanel(false,cancel=>{
            int call=Interlocked.Increment(ref reads);
            if(call==2)return new(){Provider=provider,Status="Login required"};
            if(call==3){using var registration=cancel.Register(()=>canceled.Set());release.Wait(TimeSpan.FromSeconds(5));return new(){Provider=provider,Status="STALE RESULT"};}
            return Result(provider);
        },provider:provider);
        var desktop=new FloatingMonitorWindow(new UiLanguage("en")){Height=850};
        var snapshot=new MonitorSnapshot("1%","1 GiB","—","—",true,true);
        panel.ReadingChanged+=()=>desktop.Present(snapshot with {CodexQuota=provider=="Codex"?panel.CurrentReading:null,ClaudeQuota=provider=="Claude"?panel.CurrentReading:null,AntigravityQuota=provider=="Antigravity"?panel.CurrentReading:null},true);
        desktop.Show();
        Check(desktop.ActualThemeVariant==Avalonia.Styling.ThemeVariant.Dark,"Desktop editor theme matches its dark background");
        var window=new Window{Width=360,Height=850,Content=new ScrollViewer{Content=panel}};
        window.Show();Dispatcher.UIThread.RunJobs();
        var enable=window.GetVisualDescendants().OfType<CheckBox>().Single();
        var refresh=window.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="Refresh"+provider+"Quota");
        Check(reads==0&&!refresh.IsEnabled,"Disabled quota must not read credentials");
        enable.Focus();window.KeyPress(Key.Space,RawInputModifiers.None,PhysicalKey.Space," ");window.KeyRelease(Key.Space,RawInputModifiers.None,PhysicalKey.Space," ");
        Until(()=>Text(window).Any(x=>x.Text=="54.0% left"),"Essential quota shown by default");
        var priorTheme=window.RequestedThemeVariant;window.RequestedThemeVariant=Avalonia.Styling.ThemeVariant.Dark;
        panel.ApplyReadingPalette(new(true,"#80D4FA"));Dispatcher.UIThread.RunJobs();
        Check(window.GetVisualDescendants().OfType<ProgressBar>().All(x=>((Avalonia.Media.ISolidColorBrush)x.Foreground!).Color==Avalonia.Media.Color.Parse("#80D4FA"))&&reads==1,"Quota palette update must recolor existing bars without another provider read");
        panel.ApplyReadingPalette(new());window.RequestedThemeVariant=priorTheme;
        Check(!Text(window).Any(x=>x.Text=="72.5% left"),"Additional pools hidden in essential mode");
        panel.ShowAll=true;Dispatcher.UIThread.RunJobs();
        Check(Text(window).Any(x=>x.Text=="72.5% left"),"AllWindows includes additional quota pools when selected");
        Check(Text(desktop).Any(x=>x.Text?.StartsWith("54.0% left")==true)&&!Text(desktop).Any(x=>x.Text=="72.5% left"),"Desktop retains essential quota regardless of App display mode or Session Max");
        panel.ShowAll=false;Dispatcher.UIThread.RunJobs();
        Check(!Text(window).Any(x=>x.Text=="72.5% left"),"Switching back removes additional pools immediately");
        panel.ShowAll=true;Dispatcher.UIThread.RunJobs();
        Check(reads==1,"Desktop consumes existing result without a second reader");
        var hidden=new PreviewSettings();hidden.DesktopVisible["quota"+provider]=false;desktop.ApplyPreferences(hidden);Dispatcher.UIThread.RunJobs();
        Check(desktop.GetVisualDescendants().OfType<Grid>().Where(x=>x.Name?.StartsWith("DesktopMetricquota"+provider)==true).All(x=>!x.IsVisible),"Desktop quota visibility covers all pools");
        hidden.DesktopVisible["quota"+provider]=true;desktop.ApplyPreferences(hidden);
        Check(Text(window).Any(x=>x.Text=="—"),"Unknown quota is unavailable, not zero");
        Check(window.GetVisualDescendants().OfType<ProgressBar>().Count()==2,"Only known quota has bars");
        if(output!=null){using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(output,provider.ToLowerInvariant()+"-quota.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);Dispatcher.UIThread.RunJobs();using var desktopFrame=desktop.CaptureRenderedFrame();desktopFrame!.Save(Path.Combine(output,"desktop-"+provider.ToLowerInvariant()+"-quota.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
        window.Width=240;window.FontSize=16;window.RequestedThemeVariant=Avalonia.Styling.ThemeVariant.Dark;Dispatcher.UIThread.RunJobs();
        foreach(var text in Text(window).Where(x=>x.IsEffectivelyVisible)) {
            var position=text.TranslatePoint(default,window);
            Check(position.HasValue&&position.Value.X>=0&&position.Value.X+text.Bounds.Width<=window.ClientSize.Width+.5,"Quota text stays inside a narrow window at the largest App font: "+text.Text);
        }
        refresh.Focus();Check(refresh.IsFocused,"Quota refresh remains keyboard reachable at narrow width");
        if(output!=null){using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(output,provider.ToLowerInvariant()+"-quota-narrow-dark.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
        refresh.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Until(()=>Text(window).Any(x=>x.Text?.StartsWith("Login required")==true),"Login failure visible");
        Check(Text(desktop).Any(x=>x.Text=="Login required")&&!Text(desktop).Any(x=>x.Text=="72.5% left"),"Desktop replaces stale quota with failure status");
        Check(!window.GetVisualDescendants().OfType<ProgressBar>().Any(),"Failure clears stale quota");
        refresh.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Until(()=>Volatile.Read(ref reads)==3,"Pending refresh started");
        enable.IsChecked=false;Until(()=>canceled.IsSet,"Disable cancels request");
        Check(Text(window).Any(x=>x.Text=="Off")&&!refresh.IsEnabled,"Disabled UI clears readings");
        Check(!desktop.GetVisualDescendants().OfType<Grid>().Any(x=>x.Name?.StartsWith("DesktopMetricquota"+provider)==true),"Disabling removes Desktop quota immediately");
        release.Set();enable.IsChecked=true;
        Until(()=>Volatile.Read(ref reads)==4&&Text(window).Any(x=>x.Text=="72.5% left"),"Re-enable recovers after canceled request");
        Check(!Text(window).Any(x=>x.Text=="STALE RESULT"),"Prior generation must not publish");
        panel.Dispose();window.Close();desktop.Close();

        using var started=new ManualResetEventSlim();using var stopped=new ManualResetEventSlim();
        var closing=new QuotaPanel(false,cancel=>{started.Set();cancel.WaitHandle.WaitOne(TimeSpan.FromSeconds(5));if(cancel.IsCancellationRequested)stopped.Set();cancel.ThrowIfCancellationRequested();return Result(provider);},provider:provider);
        var closingWindow=new Window{Content=closing};closingWindow.Show();
        closingWindow.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked=true;
        Until(()=>started.IsSet,"Close test request started");
        closing.Dispose();closingWindow.Close();Until(()=>stopped.IsSet,"Dispose cancels active quota IO");
        Freshness(provider);
        Console.WriteLine("PASS "+provider+" UI opt-in, complete windows, missing values, retry, stale result rejection and cancellation; synthetic reader only");
        if(provider=="Codex"){Run(output,"Claude");Run(output,"Antigravity");}
    }
    static void MonitorVisibility(string provider) {
        int reads=0;
        using var panel=new QuotaPanel(false,_=>{Interlocked.Increment(ref reads);return Result(provider);},inlineSettings:false,provider:provider);
        var monitor=new Window{Content=panel};
        var settings=new Window{Content=panel.SettingsContent};
        monitor.Show();settings.Show();Dispatcher.UIThread.RunJobs();
        try {
            var toggle=settings.GetVisualDescendants().OfType<CheckBox>().Single();
            Check(!panel.IsVisible&&reads==0,"Disabled Monitor quota must occupy no card space or start IO");
            Check(toggle.IsEffectivelyVisible,"Hidden Monitor card must retain its Settings toggle");
            toggle.IsChecked=true;
            Until(()=>panel.CurrentReading?.Status=="Live","Settings enables Monitor quota");
            Check(panel.IsEffectivelyVisible&&reads==1,"Enabled quota card appears with one reader");
            toggle.IsChecked=false;Dispatcher.UIThread.RunJobs();
            Check(!panel.IsVisible&&panel.CurrentReading==null&&toggle.IsEffectivelyVisible,"Disabling removes the card while Settings remains reachable");
        } finally {monitor.Close();settings.Close();}
    }
    static void Freshness(string provider) {
        var now=DateTimeOffset.UtcNow;int reads=0;
        using var release=new ManualResetEventSlim();
        using var panel=new QuotaPanel(false,cancel=>{
            if(Interlocked.Increment(ref reads)>1)release.Wait(cancel);
            return new QuotaReading{Provider=provider,Status="Live",Observed=now,Windows=[new(){Label="Weekly",Remaining=54}]};
        },provider:provider,utcNow:()=>now);
        var window=new Window{Content=panel};var desktop=new FloatingMonitorWindow(new UiLanguage("en"));
        var snapshot=new MonitorSnapshot("1%","1 GiB","—","—",true,true);
        panel.ReadingChanged+=()=>desktop.Present(snapshot with {CodexQuota=provider=="Codex"?panel.CurrentReading:null,ClaudeQuota=provider=="Claude"?panel.CurrentReading:null,AntigravityQuota=provider=="Antigravity"?panel.CurrentReading:null},false);
        window.Show();desktop.Show();panel.QuotaEnabled=true;
        Until(()=>Text(window).Any(x=>x.Text=="54.0% left"),"Fresh sample visible");
        var refresh=window.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="Refresh"+provider+"Quota");
        now=now.AddMinutes(10);refresh.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(panel.CurrentReading?.Status=="Live","Exactly ten minutes retains original freshness boundary");
        now=now.AddTicks(1);refresh.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Dispatcher.UIThread.RunJobs();
        Check(Text(window).Any(x=>x.Text?.StartsWith("Quota stale")==true)&&!Text(window).Any(x=>x.Text=="54.0% left"),"Same reading ages out while refresh is pending");
        Check(!window.GetVisualDescendants().OfType<ProgressBar>().Any(),"Expired sample has no live progress bar");
        Check(Text(desktop).Any(x=>x.Text=="Quota stale")&&!Text(desktop).Any(x=>x.Text=="54.0% left"),"Desktop receives stale transition without new provider data");
        release.Set();Until(()=>Text(window).Any(x=>x.Text=="54.0% left"),"Fresh completion restores percentages");
        Check(reads==2,"Freshness rendering does not duplicate provider reads");
        window.Close();desktop.Close();
    }
}
