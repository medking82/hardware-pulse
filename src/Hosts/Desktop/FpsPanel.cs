using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Styling;
using Avalonia.Controls.Templates;

namespace HardwarePulse.Desktop;

public sealed record DesktopFpsSnapshot(bool Enabled,string Status,string Current="—",string Average="—",string Minimum="—",string Low="—") {
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
    readonly TextBlock values=new(){Name="FpsValues",TextWrapping=TextWrapping.Wrap};
    readonly bool inlineSettings;
    public Control SettingsContent {get;}
    CancellationTokenSource? cancel;
    long reset;
    string target="";
    bool disposed,loading;
    int discovery;
    public Task Sampling {get;private set;}=Task.CompletedTask;
    public DesktopFpsSnapshot Current {get;private set;}=new(false,"FPS capture stopped");
    public event Action<DesktopFpsSnapshot>? ReadingChanged;
    public event Action<bool,string>? PreferenceChanged;
    public bool Enabled {get=>enabled.IsChecked==true;set=>enabled.IsChecked=value;}
    public string Target {get=>target;set {target=value;SetTargets([]);}}
    public FpsPanel(UiLanguage language,bool demo=false,Func<IDesktopFpsSource>? factory=null,Func<string[]>? discover=null,bool inlineSettings=true) {
        this.inlineSettings=inlineSettings;
        this.language=language;this.factory=factory??(()=>new WindowsFpsSource());this.discover=discover??WindowsFpsSource.Targets;
        targets.ItemTemplate=new FuncDataTemplate<string>((name,_)=>string.IsNullOrEmpty(name)?language.Set(new TextBlock{TextWrapping=TextWrapping.Wrap},"Auto (foreground app)"):new TextBlock{Text=name,TextWrapping=TextWrapping.Wrap});
        supported=factory!=null||(!demo&&OperatingSystem.IsWindows());
        Name="FpsPanel";Padding=new Thickness(12);CornerRadius=new CornerRadius(14);BorderThickness=new Thickness(1);BorderBrush=Brush.Parse("#426D8B9F");IsVisible=inlineSettings;
        void Palette()=>Background=Brush.Parse(ActualThemeVariant==ThemeVariant.Light?"#DDEEF1F4":"#3031485B");
        PropertyChanged+=(_,e)=>{if(e.Property==ThemeVariantScope.ActualThemeVariantProperty)Palette();};Palette();
        var body=new StackPanel{Spacing=8};Child=body;
        body.Children.Add(language.Set(new TextBlock{FontWeight=FontWeight.SemiBold},"FPS"));
        var settings=new StackPanel{Spacing=10};SettingsContent=settings;
        settings.Children.Add(language.Set(enabled,"Enable FPS"));enabled.IsEnabled=supported;
        settings.Children.Add(targets);var buttons=new WrapPanel{Orientation=Avalonia.Layout.Orientation.Horizontal};
        var refresh=language.Set(new Button{Name="RefreshFpsTargets"},"Refresh apps");var restart=language.Set(new Button{Name="ResetFps"},"Reset FPS");
        refresh.Margin=restart.Margin=new Thickness(0,0,6,6);buttons.Children.Add(refresh);buttons.Children.Add(restart);settings.Children.Add(buttons);
        if(inlineSettings)body.Children.Add(settings);
        body.Children.Add(values);body.Children.Add(status);
        if(!supported)settings.Children.Add(language.Set(new TextBlock{TextWrapping=TextWrapping.Wrap},"FPS is unavailable on this platform."));
        void Controls(){targets.IsEnabled=refresh.IsEnabled=restart.IsEnabled=supported&&Enabled;}
        enabled.IsCheckedChanged+=(_,_)=>{Stop();Controls();if(Enabled&&supported&&!disposed)Start();PreferenceChanged?.Invoke(Enabled,target);};
        targets.SelectionChanged+=(_,_)=>{if(loading)return;target=targets.SelectedItem as string??"";Reset();PreferenceChanged?.Invoke(Enabled,target);};
        refresh.Click+=async (_,_)=>await RefreshTargets();restart.Click+=(_,_)=>Reset();
        language.Changed+=Localize;SetTargets([]);Controls();Localize();
    }
    void SetTargets(string[] names) {
        loading=true;try {
            var items=new[]{""}.Concat(names.Append(target).Where(x=>x.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).Order()).ToArray();
            targets.ItemsSource=items;targets.SelectedItem=items.First(x=>string.Equals(x,target,StringComparison.OrdinalIgnoreCase));
        }finally{loading=false;}
    }
    async Task RefreshTargets(){if(!Enabled||!supported||disposed)return;int generation=++discovery;try{var names=await Task.Run(discover);if(!disposed&&Enabled&&generation==discovery)SetTargets(names);}catch{if(!disposed&&Enabled&&generation==discovery)language.Set(status,"Could not refresh apps.");}}
    void Localize(){language.Set(status,!supported?"FPS is unavailable on this platform.":Current.Status);values.Text=$"FPS {Current.Current} · AVG {Current.Average} · MIN {Current.Minimum} · 1% LOW {Current.Low}";}
    void Publish(DesktopFpsSnapshot snapshot){Current=snapshot;IsVisible=inlineSettings||snapshot.Enabled;Localize();ReadingChanged?.Invoke(snapshot);}
    void Reset(){Interlocked.Increment(ref reset);if(Enabled)Publish(new(true,"Waiting for frames"));}
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
                    string selected=target;long requested=Interlocked.Read(ref reset);
                    var snapshot=await Task.Run(()=>{if(generation!=requested){source.Reset();generation=requested;}return DesktopFpsSnapshot.Capture(source.Poll(selected));});
                    if(cancellation.IsCancellationRequested)break;
                    if(selected==target&&requested==Interlocked.Read(ref reset))Publish(snapshot);
                    await Task.Delay(500,cancellation.Token);
                }
            }catch(OperationCanceledException){}catch{if(!cancellation.IsCancellationRequested)Publish(new(true,"FPS capture failed"));}
            finally {if(ReferenceEquals(cancel,cancellation))cancel=null;cancellation.Dispose();}
        }
    }
    void Stop(){discovery++;cancel?.Cancel();cancel=null;Publish(new(false,"FPS capture stopped"));}
    public void Dispose(){if(disposed)return;disposed=true;Stop();language.Changed-=Localize;}
}
