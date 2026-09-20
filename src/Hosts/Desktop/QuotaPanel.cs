using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Styling;

namespace HardwarePulse.Desktop;

// UI-thread owner; QuotaSession owns scheduling/cancellation and the adapter owns IO.
public sealed class QuotaPanel : UserControl,IDisposable {
    readonly QuotaSession session;
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(1)};
    readonly CheckBox enabled=new(){Name="EnableCodexQuota",Content="Show Codex quota"};
    readonly Button refresh=new(){Name="RefreshCodexQuota",Content="Refresh",IsEnabled=false};
    readonly TextBlock status=new(){Text="Off",TextWrapping=TextWrapping.Wrap};
    readonly StackPanel windows=new(){Spacing=8};
    readonly TextBlock title=new(){FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};
    readonly Border card=new(){Padding=new Thickness(12),CornerRadius=new CornerRadius(14),BorderThickness=new Thickness(1),BorderBrush=Brush.Parse("#426D8B9F")};
    readonly Avalonia.Controls.Shapes.Path icon;
    readonly bool demo;
    readonly string provider;
    readonly UiLanguage language;
    readonly Func<DateTimeOffset> utcNow;
    string? renderedStatus;
    QuotaReading? shown;
    bool disposed;
    bool showAll;
    public bool ShowAll {get=>showAll;set{if(showAll==value)return;showAll=value;if(shown!=null)Render(shown);}}
    public Control SettingsContent {get;}
    public event Action<bool>? EnabledChanged;
    public event Action? ReadingChanged;
    public QuotaReading? CurrentReading {
        get {
            if(!QuotaEnabled||shown==null)return null;
            string state=QuotaPresentation.Status(shown,utcNow());
            return state==shown.Status?shown:new QuotaReading{Provider=shown.Provider,Status=state,Observed=shown.Observed};
        }
    }
    public bool QuotaEnabled {get=>enabled.IsChecked==true;set=>enabled.IsChecked=value;}
    public QuotaPanel(bool demo,Func<CancellationToken,QuotaReading> read,bool inlineSettings=true,UiLanguage? language=null,string provider="Codex",Func<DateTimeOffset>? utcNow=null) {
        ArgumentNullException.ThrowIfNull(read);
        if(provider is not ("Codex" or "Claude" or "Antigravity"))throw new ArgumentOutOfRangeException(nameof(provider));
        this.provider=provider;enabled.Name="Enable"+provider+"Quota";refresh.Name="Refresh"+provider+"Quota";
        this.demo=demo;
        this.utcNow=utcNow??(()=>DateTimeOffset.UtcNow);
        this.language=language??new UiLanguage();
        this.language.Set(enabled,"Show "+provider+" quota");this.language.Set(refresh,"Refresh");this.language.Set(status,"Off");
        this.language.Changed+=OnLanguageChanged;
        session=new QuotaSession((_,cancel)=>read(cancel));
        var body=new StackPanel{Spacing=8};
        var header=new Grid{ColumnDefinitions=new("Auto,*"),ColumnSpacing=8};
        icon=AppIcon.Create(provider.ToLowerInvariant());
        header.Children.Add(new Viewbox{Width=19,Height=19,Child=icon});
        title.Text=provider;title.FontSize=FontSize+3;Grid.SetColumn(title,1);header.Children.Add(title);
        body.Children.Add(header);
        var settings=new StackPanel{Spacing=12};settings.Children.Add(enabled);
        settings.Children.Add(this.language.Set(new TextBlock{TextWrapping=TextWrapping.Wrap},demo?"Demo · Sample quota only. No account is accessed.":provider=="Antigravity"?"Reads quota from your running Antigravity app on Windows. Refreshes every five minutes while enabled. Login stays in Antigravity.":provider=="Claude"?"Uses your existing Claude Code login. Refreshes every five minutes while enabled. Login and token refresh stay in Claude Code.":"Uses your existing Codex file login. Refreshes every five minutes while enabled. This choice is remembered. Login and token refresh stay in Codex."));
        SettingsContent=settings;if(inlineSettings)body.Children.Add(settings);
        if(!inlineSettings)this.language.Set(status,"Off · Enable in Settings → AI Quota");
        refresh.Padding=new Thickness(8,4);refresh.MinHeight=28;
        status.Opacity=.8;
        body.Children.Add(status);body.Children.Add(windows);body.Children.Add(refresh);
        card.Child=body;Content=card;
        PropertyChanged+=(_,e)=>{
            if(e.Property==ThemeVariantScope.ActualThemeVariantProperty)ApplyPalette();
            if(e.Property==FontSizeProperty)title.FontSize=FontSize+3;
        };
        ApplyPalette();
        enabled.PropertyChanged+=(_,e)=>{if(e.Property==ToggleButton.IsCheckedProperty)SetEnabled();};
        refresh.Click+=(_,_)=>{if(!disposed&&enabled.IsChecked==true){session.Refresh();Tick();}};
        timer.Tick+=(_,_)=>Tick();
    }
    void ApplyPalette() {
        bool light=ActualThemeVariant==ThemeVariant.Light;
        card.Background=Brush.Parse(light?"#DDEEF1F4":"#3031485B");
        var accent=Brush.Parse(light?"#17202B":provider=="Claude"?"#E7B497":provider=="Antigravity"?"#A7CBFF":"#A5E7D5");
        icon.Stroke=icon.Stroke==null?null:accent;icon.Fill=icon.Fill==null?null:accent;
        foreach(var bar in windows.Children.OfType<StackPanel>().SelectMany(x=>x.Children).OfType<ProgressBar>())bar.Foreground=accent;
    }
    void SetEnabled() {
        if(disposed)return;
        bool on=enabled.IsChecked==true;
        session.Enable(provider,on);refresh.IsEnabled=on;shown=null;windows.Children.Clear();
        if(on){timer.Start();Tick();}else{timer.Stop();language.Set(status,"Off");ReadingChanged?.Invoke();}
        EnabledChanged?.Invoke(on);
    }
    void Tick() {
        if(disposed||enabled.IsChecked!=true)return;
        session.Tick(utcNow());
        var reading=session.Readings.Single();
        if(ReferenceEquals(reading,shown)&&renderedStatus==QuotaPresentation.Status(reading,utcNow()))return;
        Render(reading);
    }
    void OnLanguageChanged(){if(shown!=null)Render(shown);}
    void Render(QuotaReading reading) {
        shown=reading;windows.Children.Clear();
        renderedStatus=QuotaPresentation.Status(reading,utcNow());
        bool live=renderedStatus=="Live";
        status.Text=(demo?language.T("Demo")+" · ":"")+language.T(renderedStatus);
        if(reading.Observed!=default)status.Text+=" · "+string.Format(language.T("Updated {0}"),reading.Observed.ToLocalTime().ToString("t"));
        if(reading.Status=="Login required")status.Text+=" · "+language.T(provider=="Antigravity"?"Sign in through Antigravity, then refresh here.":provider=="Claude"?"Sign in through Claude Code, then refresh here.":"Sign in through Codex, then refresh here. This preview requires file-based login.");
        var rows=showAll&&reading.AllWindows.Count>0?reading.AllWindows:reading.Windows;
        foreach(var row in rows) {
            var item=new StackPanel{Spacing=3};
            var line=new Grid{ColumnDefinitions=new("*,Auto"),ColumnSpacing=8};
            line.Children.Add(new TextBlock{Text=language.T(row.Label),TextWrapping=TextWrapping.Wrap});
            var value=new TextBlock{Text=live&&row.Remaining.HasValue?string.Format(language.T("{0}% left"),row.Remaining.Value.ToString("F1")):"—",FontWeight=FontWeight.SemiBold};
            Grid.SetColumn(value,1);line.Children.Add(value);item.Children.Add(line);
            if(live&&row.Remaining.HasValue)item.Children.Add(new ProgressBar{Minimum=0,Maximum=100,Value=row.Remaining.Value,Height=3,MinHeight=0,IsHitTestVisible=false,Background=Brush.Parse("#304F6B7C")});
            item.Children.Add(new TextBlock{Text=row.Reset.HasValue?string.Format(language.T("Resets {0}"),row.Reset.Value.ToLocalTime().ToString("g")):language.T("Reset time unavailable"),TextWrapping=TextWrapping.Wrap,Opacity=.75});
            windows.Children.Add(item);
        }
        ApplyPalette();ReadingChanged?.Invoke();
    }
    public void Dispose() {
        if(disposed)return;disposed=true;timer.Stop();session.Dispose();
        language.Changed-=OnLanguageChanged;
        enabled.IsEnabled=false;refresh.IsEnabled=false;
    }
}
