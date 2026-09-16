using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class SamplingRecoveryTests {
    sealed class IntermittentSource : IMonitorSource {
        public bool IsDemo=>true;
        public int Calls;
        public bool Persistent;
        public string[] Interfaces()=>["Synthetic interface"];
        public MonitorSnapshot Poll(string? name) {
            int call=Interlocked.Increment(ref Calls);
            if(call==2||(Persistent&&call>2))throw new IOException("Synthetic transient reader failure");
            return new(call==1?"21.0%":"31.0%","4.0 / 16.0 GiB · 25.0%","10.0 B/s","2.0 B/s",true,true){PeakCpu="41.0%"};
        }
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Until(Func<bool> done) {
        var end=DateTime.UtcNow.AddSeconds(5);
        while(!done()&&DateTime.UtcNow<end){using var slice=new CancellationTokenSource(TimeSpan.FromMilliseconds(20));Dispatcher.UIThread.MainLoop(slice.Token);}
        Check(done(),"Sampling recovery timed out");
    }
    static bool Has(Window window,string text)=>window.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text==text);
    public static void Visibility() {
        var window=new MonitorWindow(new MonitorSource(true),start:false);
        try {
            var first=new MonitorSnapshot("17.0%","4 GiB","10 B/s","2 B/s",true,true){PeakCpu="41.0%"};
            window.Present(first);window.Show();Until(()=>Has(window,"17.0%"));
            window.OpenFloatingMonitor();var floating=window.FloatingMonitor!;
            window.Hide();window.Present(first with{Cpu="29.0%"});
            Check(Has(window,"17.0%")&&!Has(window,"29.0%"),"Hidden Monitor must defer control updates");
            Check(Has(floating,"29.0%"),"Hidden Monitor must continue updating floating readings");
            window.Show();Until(()=>Has(window,"29.0%"));
            window.WindowState=WindowState.Minimized;Until(()=>window.WindowState==WindowState.Minimized);
            window.Present(first with{Cpu="33.0%"});
            Check(Has(window,"29.0%")&&Has(floating,"33.0%"),"Minimized Monitor must defer only its own controls");
            window.RestoreMain();Until(()=>Has(window,"33.0%"));
            window.Hide();
            var mode=window.GetVisualDescendants().OfType<ComboBox>().Single(x=>x.Name=="ReadingMode");mode.SelectedIndex=1;
            Check(Has(floating,"41.0%"),"Hidden Monitor must propagate Session Max to floating readings");
            window.Show();Until(()=>Has(window,"41.0%"));
            window.Hide();mode.SelectedIndex=0;window.Present(first with{Cpu="—",CpuReady=false});
            Check(!Has(floating,"33.0%"),"Hidden Monitor must clear unavailable floating readings");
            window.Show();Check(!Has(window,"33.0%")&&!Has(window,"41.0%"),"Restore must not show stale readings");
        } finally {window.Close();}
        Console.WriteLine("PASS hidden/minimized Monitor defers controls, preserves floating live/max and restores latest readings");
    }
    public static void Reopen() {
        var source=new IntermittentSource();var window=new MonitorWindow(source);window.Show();
        try {
            Until(()=>Has(window,"21.0%"));
            var firstWorker=window.Sampling;
            window.Hide();Until(()=>source.Calls>=3);
            Check(Has(window,"21.0%"),"Background polling must leave hidden Monitor controls unchanged");
            window.Show();Until(()=>Has(window,"31.0%"));
            for(int i=0;i<3;i++){window.Hide();window.Show();}
            Check(ReferenceEquals(firstWorker,window.Sampling),"Reopening Monitor must retain one sampling worker");
        } finally {window.Close();Until(()=>window.Sampling.IsCompleted);}
        Console.WriteLine("PASS Monitor hide/show retains a single sampling worker and closes cleanly");
    }
    public static void Run() {
        Visibility();
        Reopen();
        var source=new IntermittentSource();var window=new MonitorWindow(source);window.Show();
        try {
            Until(()=>Has(window,"21.0%"));
            Until(()=>source.Calls>=2);Dispatcher.UIThread.RunJobs();
            Until(()=>Has(window,"Monitoring unavailable. Retrying…"));
            Check(!Has(window,"21.0%")&&!window.Sampling.IsCompleted,"Failure clears live values without terminating worker");
            var mode=window.GetVisualDescendants().OfType<ComboBox>().Single(x=>x.Name=="ReadingMode");mode.SelectedIndex=1;
            Check(Has(window,"41.0%")&&Has(window,"Monitoring unavailable. Retrying…"),"History survives but does not disguise unavailable state");
            mode.SelectedIndex=0;
            Until(()=>Has(window,"31.0%"));
            Check(!Has(window,"Monitoring unavailable. Retrying…"),"Successful sample restores live status");
        } finally {window.Close();Until(()=>window.Sampling.IsCompleted);}
        Check(window.Sampling.IsCompletedSuccessfully,"Recovery worker closes cleanly");
        var failing=new IntermittentSource{Persistent=true};var retry=new MonitorWindow(failing);retry.Show();
        var elapsed=System.Diagnostics.Stopwatch.StartNew();
        try {
            Until(()=>failing.Calls>=4);
            Check(elapsed.Elapsed>=TimeSpan.FromSeconds(2)&&failing.Calls<=5,"Repeated failures retain bounded timer cadence");
        } finally {retry.Close();Until(()=>retry.Sampling.IsCompleted);}
        foreach(bool smoke in new[]{true,false}) {
            int exit=Environment.ExitCode;
            var strictSource=new IntermittentSource();var strict=new MonitorWindow(strictSource,smoke:smoke,measure:!smoke);strict.Show();
            try {
                Until(()=>strict.Sampling.IsCompleted);
                Check(Environment.ExitCode==3&&strictSource.Calls==2&&!strict.IsVisible,"Smoke/measurement must report reader failure and stop");
            } finally {strict.Close();Environment.ExitCode=exit;}
        }
        Console.WriteLine("PASS transient sampling recovery: unavailable live values, retained peaks, resumed sampling and close");
    }
}
