using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse;
using HardwarePulse.Desktop;

static class DesktopStartupTests {
    sealed class Source : IDesktopStartupManagement {
        public DesktopStartupState State=DesktopStartupState.Disabled;
        public DesktopStartupResult Result=DesktopStartupResult.Success;
        public int Reads,Changes,Starts;
        public bool Mutate=true,Throw;
        public TaskCompletionSource<DesktopStartupResult>? Pending;
        public Task<DesktopStartupState> ReadAsync(){Reads++;return Task.FromResult(State);}
        public async Task<DesktopStartupResult> SetEnabledAsync(bool value) {
            Changes++;if(Throw)throw new InvalidOperationException("fixture");
            var result=Pending==null?Result:await Pending.Task;
            if(result==DesktopStartupResult.Success&&Mutate)State=value?DesktopStartupState.Enabled:DesktopStartupState.Disabled;
            return result;
        }
        public Task<DesktopStartupResult> StartCollectorAsync(){Starts++;return Task.FromResult(Result);}
    }
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
    static void Wait(Task task) {
        var end=DateTime.UtcNow.AddSeconds(5);
        while(!task.IsCompleted&&DateTime.UtcNow<end){using var slice=new CancellationTokenSource(20);Dispatcher.UIThread.MainLoop(slice.Token);}
        Check(task.IsCompleted,"Startup operation timed out");task.GetAwaiter().GetResult();
    }
    public static void Run() {
        var source=new Source();var language=new UiLanguage("en");
        using var panel=new DesktopStartupPanel(language,source:source);
        var window=new Window{Width=360,Height=460,Content=panel};window.Show();Dispatcher.UIThread.RunJobs();Wait(panel.Operation);
        try {
            Check(panel.State==DesktopStartupState.Disabled&&source.Changes==0&&source.Starts==0,"Loading changed startup tasks");
            var enabled=panel.GetVisualDescendants().OfType<CheckBox>().Single(x=>x.Name=="StartWithWindows");
            enabled.IsChecked=true;Wait(panel.Operation);
            Check(panel.State==DesktopStartupState.Enabled&&enabled.IsChecked==true&&source.Changes==1,"UI toggle did not verify enabled state");
            source.Result=DesktopStartupResult.Canceled;Wait(panel.ChangeAsync(false));
            Check(panel.State==DesktopStartupState.Enabled&&enabled.IsChecked==true&&panel.StatusKey=="Startup change canceled.","Canceled UAC changed displayed state");
            source.Result=DesktopStartupResult.Success;source.Mutate=false;Wait(panel.ChangeAsync(false));
            Check(panel.StatusKey=="Startup change could not be verified.","Successful exit bypassed state readback");
            source.Mutate=true;Wait(panel.ChangeAsync(false));
            Check(panel.State==DesktopStartupState.Disabled,"Could not disable startup");
            Wait(panel.StartCollectorAsync());Check(source.Starts==1&&panel.StatusKey=="Hardware collector start requested.","Collector action was not explicit");
            foreach(string locale in new[]{"en","zh-CN","zh-TW"}) {
                language.Select(locale);Dispatcher.UIThread.RunJobs();
                Check(panel.GetVisualDescendants().OfType<Button>().All(x=>x.Bounds.Width<=panel.Bounds.Width),"Startup buttons overflow the narrow settings panel");
            }
            source.Throw=true;Wait(panel.ChangeAsync(true));Check(panel.State==DesktopStartupState.Unavailable&&!enabled.IsEnabled,"Failure did not disable uncertain state");
            source.Throw=false;Wait(panel.RefreshAsync());
            source.Pending=new(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending=panel.ChangeAsync(true);int count=source.Changes;
            Wait(panel.ChangeAsync(false));Check(source.Changes==count&&!enabled.IsEnabled,"Concurrent operation was admitted");
            panel.Dispose();var state=panel.State;int reads=source.Reads;
            source.Pending.SetResult(DesktopStartupResult.Success);Wait(pending);
            Check(panel.State==state&&source.Reads==reads,"Disposed panel accepted late management result");
        }finally{window.Close();}
        using var demo=new DesktopStartupPanel(language,demo:true);Wait(demo.RefreshAsync());Check(demo.State==DesktopStartupState.Unavailable,"Demo allowed startup management");
        // The actual test process is dotnet, never the protected installed AppHost.
        var actual=new WindowsStartupManagement();
        var read=actual.ReadAsync();Wait(read);Check(read.Result==DesktopStartupState.Unavailable,"Development launch admitted installed management");
        var change=actual.SetEnabledAsync(true);Wait(change);Check(change.Result==DesktopStartupResult.Failed,"Development launch attempted startup mutation");
        var start=actual.StartCollectorAsync();Wait(start);Check(start.Result==DesktopStartupResult.Failed,"Development launch attempted collector start");
        Console.WriteLine("PASS startup controls: explicit actions, readback, canceled UAC, failure, serialization, late disposal and development-path rejection");
    }
}
