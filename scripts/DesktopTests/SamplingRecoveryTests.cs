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
    static bool Has(MonitorWindow window,string text)=>window.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text==text);
    public static void Run() {
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
