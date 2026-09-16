using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace HardwarePulse.Desktop;

public sealed record DesktopFpsSnapshot(bool Enabled,string Status,string Current="—",string Average="—",string Minimum="—",string Low="—") {
    public nint TargetWindow {get;init;}
    public string TargetName {get;init;}="";
    public static DesktopFpsSnapshot Capture(FrameMetrics metrics) {
        string Format(double value)=>metrics.Ready&&double.IsFinite(value)&&value>=0?value.ToString("F0"):"—";
        return new(true,metrics.Status??"Waiting for frames",Format(metrics.Current),Format(metrics.Average),Format(metrics.Minimum),Format(metrics.Low));
    }
}

// A single session publishes to both views. Disabled means no timer, process
// discovery or pipe connection. Cancellation rejects already-queued results.
public sealed class FpsPanel : Border,IDisposable {
    readonly UiLanguage language;
    readonly Func<IDesktopFpsSource> factory;
    readonly Func<string[]> discover;
    readonly bool supported;
    readonly CheckBox enabled=new(){Name="FpsEnabled"};
    readonly ComboBox targets=new(){Name="FpsTarget",HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Stretch};
    readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap};
    readonly TextBlock values=new(){Name="FpsValues",TextWrapping=TextWrapping.Wrap,FontSize=20};
    CancellationTokenSource? cancel;
    long reset;
    string target="";
    bool disposed,loading;
    bool trackGame;
    Action updateControls=()=>{};
    public bool TrackGame {
        get=>trackGame;
        set {if(trackGame==value||disposed)return;trackGame=value;updateControls();if(!Enabled){Stop();if(trackGame&&supported)Start();}}
    }
    int discovery;
    public Task Sampling {get;private set;}=Task.CompletedTask;
    public DesktopFpsSnapshot Current {get;private set;}=new(false,"FPS capture stopped");
    public event Action<DesktopFpsSnapshot>? ReadingChanged;
    public event Action<bool,string>? PreferenceChanged;
    public bool Enabled {get=>enabled.IsChecked==true;set=>enabled.IsChecked=value;}
    public string Target {get=>target;set {target=value;SetTargets([]);}}
    public FpsPanel(UiLanguage language,bool demo=false,Func<IDesktopFpsSource>? factory=null,Func<string[]>? discover=null) {
        this.language=language;this.factory=factory??(()=>new WindowsFpsSource());this.discover=discover??WindowsFpsSource.Targets;
        supported=factory!=null||(!demo&&OperatingSystem.IsWindows());
        Name="FpsPanel";Padding=new Thickness(20);CornerRadius=new CornerRadius(14);BorderThickness=new Thickness(1);BorderBrush=Brushes.Gray;
        var body=new StackPanel{Spacing=12};Child=body;
        body.Children.Add(language.Set(new TextBlock{FontWeight=FontWeight.SemiBold},"FPS"));
        body.Children.Add(language.Set(enabled,"Enable FPS"));enabled.IsEnabled=supported;
        body.Children.Add(targets);var buttons=new StackPanel{Orientation=Avalonia.Layout.Orientation.Horizontal,Spacing=12};
        var refresh=language.Set(new Button{Name="RefreshFpsTargets"},"Refresh apps");var restart=language.Set(new Button{Name="ResetFps"},"Reset FPS");
        buttons.Children.Add(refresh);buttons.Children.Add(restart);body.Children.Add(buttons);body.Children.Add(values);body.Children.Add(status);
        void Controls(){targets.IsEnabled=refresh.IsEnabled=supported&&(Enabled||TrackGame);restart.IsEnabled=supported&&Enabled;}
        updateControls=Controls;
        enabled.IsCheckedChanged+=(_,_)=>{Stop();Controls();if((Enabled||TrackGame)&&supported&&!disposed)Start();PreferenceChanged?.Invoke(Enabled,target);};
        targets.SelectionChanged+=(_,_)=>{if(loading)return;target=(targets.SelectedItem as ComboBoxItem)?.Tag as string??"";Reset();PreferenceChanged?.Invoke(Enabled,target);};
        refresh.Click+=async (_,_)=>await RefreshTargets();restart.Click+=(_,_)=>Reset();
        language.Changed+=Localize;SetTargets([]);Controls();Localize();
    }
    void SetTargets(string[] names) {
        loading=true;try {
            var items=new List<ComboBoxItem>{new(){Content=language.T("Auto (foreground app)"),Tag=""}};
            foreach(var name in names.Append(target).Where(x=>x.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).Order())items.Add(new(){Content=name,Tag=name});
            targets.ItemsSource=items;targets.SelectedItem=items.First(x=>string.Equals(x.Tag as string,target,StringComparison.OrdinalIgnoreCase));
        }finally{loading=false;}
    }
    async Task RefreshTargets(){if(!(Enabled||TrackGame)||!supported||disposed)return;int generation=++discovery;try{var names=await Task.Run(discover);if(!disposed&&(Enabled||TrackGame)&&generation==discovery)SetTargets(names);}catch{if(!disposed&&(Enabled||TrackGame)&&generation==discovery)language.Set(status,"Could not refresh apps.");}}
    void Localize(){language.Set(status,!supported?"FPS is unavailable on this platform.":Current.Status);values.Text=$"FPS {Current.Current} · AVG {Current.Average} · MIN {Current.Minimum} · 1% LOW {Current.Low}";if(targets.ItemsSource is IEnumerable<ComboBoxItem> items)items.First().Content=language.T("Auto (foreground app)");}
    void Publish(DesktopFpsSnapshot snapshot){Current=snapshot;Localize();ReadingChanged?.Invoke(snapshot);}
    void Reset(){Interlocked.Increment(ref reset);if(Enabled||TrackGame)Publish(new(Enabled,"Waiting for frames"));}
    void Start() {
        var cancellation=new CancellationTokenSource();cancel=cancellation;
        var previous=Sampling;
        Sampling=Run();
        async Task Run() {
            await previous;
            if(cancellation.IsCancellationRequested){cancellation.Dispose();return;}
            try {
                using var source=await Task.Run(factory);
                long generation=Interlocked.Read(ref reset);
                while(!cancellation.IsCancellationRequested) {
                    string selected=target;bool capture=Enabled;long requested=Interlocked.Read(ref reset);
                    var snapshot=await Task.Run(()=>{
                        if(generation!=requested){source.Reset();generation=requested;}
                        var frame=source is IDesktopGameSource game?game.PollGame(selected,capture):new DesktopGameFrame(capture?source.Poll(selected):new(){Status="FPS capture stopped"});
                        return DesktopFpsSnapshot.Capture(frame.Metrics) with {Enabled=capture,TargetWindow=frame.Window,TargetName=frame.ProcessName};
                    });
                    if(cancellation.IsCancellationRequested)break;
                    if(selected==target&&capture==Enabled&&requested==Interlocked.Read(ref reset))Publish(snapshot);
                    await Task.Delay(500,cancellation.Token);
                }
            }catch(OperationCanceledException){}catch{if(!cancellation.IsCancellationRequested)Publish(new(true,"FPS capture failed"));}
            finally {if(ReferenceEquals(cancel,cancellation))cancel=null;cancellation.Dispose();}
        }
    }
    void Stop(){discovery++;cancel?.Cancel();cancel=null;Publish(new(false,"FPS capture stopped"));}
    public void Dispose(){if(disposed)return;disposed=true;Stop();language.Changed-=Localize;}
}
