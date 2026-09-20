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
        var desktop=new FloatingMonitorWindow(new UiLanguage("en")){Height=850};
        var snapshot=new MonitorSnapshot("1%","1 GiB","—","—",true,true);
        panel.ReadingChanged+=()=>desktop.Present(snapshot with {CodexQuota=panel.CurrentReading},true);
        desktop.Show();
        Check(desktop.ActualThemeVariant==Avalonia.Styling.ThemeVariant.Dark,"Desktop editor theme matches its dark background");
        var window=new Window{Width=360,Height=850,Content=new ScrollViewer{Content=panel}};
        window.Show();Dispatcher.UIThread.RunJobs();
        var enable=window.GetVisualDescendants().OfType<CheckBox>().Single();
        var refresh=window.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="RefreshCodexQuota");
        Check(reads==0&&!refresh.IsEnabled,"Disabled quota must not read credentials");
        enable.Focus();window.KeyPress(Key.Space,RawInputModifiers.None,PhysicalKey.Space," ");window.KeyRelease(Key.Space,RawInputModifiers.None,PhysicalKey.Space," ");
        Until(()=>Text(window).Any(x=>x.Text=="72.5% left"),"AllWindows must include additional quota pools");
        Check(Text(desktop).Any(x=>x.Text=="72.5% left")&&Text(desktop).Any(x=>x.Text=="—"),"Desktop includes additional pools and unavailable quota even in Session Max");
        Check(reads==1,"Desktop consumes existing result without a second reader");
        var hidden=new PreviewSettings();hidden.DesktopVisible["quotaCodex"]=false;desktop.ApplyPreferences(hidden);Dispatcher.UIThread.RunJobs();
        Check(desktop.GetVisualDescendants().OfType<Grid>().Where(x=>x.Name?.StartsWith("DesktopMetricquotaCodex")==true).All(x=>!x.IsVisible),"Desktop quota visibility covers all pools");
        hidden.DesktopVisible["quotaCodex"]=true;desktop.ApplyPreferences(hidden);
        Check(Text(window).Any(x=>x.Text=="—"),"Unknown quota is unavailable, not zero");
        Check(window.GetVisualDescendants().OfType<ProgressBar>().Count()==2,"Only known quota has bars");
        if(output!=null){using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"codex-quota.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);Dispatcher.UIThread.RunJobs();using var desktopFrame=desktop.CaptureRenderedFrame();desktopFrame!.Save(Path.Combine(output,"desktop-codex-quota.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
        refresh.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Until(()=>Text(window).Any(x=>x.Text?.StartsWith("Login required")==true),"Login failure visible");
        Check(Text(desktop).Any(x=>x.Text=="Login required")&&!Text(desktop).Any(x=>x.Text=="72.5% left"),"Desktop replaces stale quota with failure status");
        Check(!window.GetVisualDescendants().OfType<ProgressBar>().Any(),"Failure clears stale quota");
        refresh.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Until(()=>Volatile.Read(ref reads)==3,"Pending refresh started");
        enable.IsChecked=false;Until(()=>canceled.IsSet,"Disable cancels request");
        Check(Text(window).Any(x=>x.Text=="Off")&&!refresh.IsEnabled,"Disabled UI clears readings");
        Check(!desktop.GetVisualDescendants().OfType<Grid>().Any(x=>x.Name?.StartsWith("DesktopMetricquotaCodex")==true),"Disabling removes Desktop quota immediately");
        release.Set();enable.IsChecked=true;
        Until(()=>Volatile.Read(ref reads)==4&&Text(window).Any(x=>x.Text=="72.5% left"),"Re-enable recovers after canceled request");
        Check(!Text(window).Any(x=>x.Text=="STALE RESULT"),"Prior generation must not publish");
        panel.Dispose();window.Close();desktop.Close();

        using var started=new ManualResetEventSlim();using var stopped=new ManualResetEventSlim();
        var closing=new CodexQuotaPanel(false,cancel=>{started.Set();cancel.WaitHandle.WaitOne(TimeSpan.FromSeconds(5));if(cancel.IsCancellationRequested)stopped.Set();cancel.ThrowIfCancellationRequested();return Result();});
        var closingWindow=new Window{Content=closing};closingWindow.Show();
        closingWindow.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked=true;
        Until(()=>started.IsSet,"Close test request started");
        closing.Dispose();closingWindow.Close();Until(()=>stopped.IsSet,"Dispose cancels active quota IO");
        Console.WriteLine("PASS Codex UI opt-in, complete windows, missing values, retry, stale result rejection and cancellation; synthetic reader only");
    }
}
