using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace HardwarePulse.Desktop;

// UI-thread owner; QuotaSession owns scheduling/cancellation and the adapter owns IO.
public sealed class CodexQuotaPanel : UserControl,IDisposable {
    readonly QuotaSession session;
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(1)};
    readonly CheckBox enabled=new(){Name="EnableCodexQuota",Content="Show Codex quota"};
    readonly Button refresh=new(){Name="RefreshCodexQuota",Content="Refresh",IsEnabled=false};
    readonly TextBlock status=new(){Text="Off",TextWrapping=TextWrapping.Wrap};
    readonly StackPanel windows=new(){Spacing=16};
    readonly bool demo;
    readonly string provider;
    readonly UiLanguage language;
    QuotaReading? shown;
    bool disposed;
    public Control SettingsContent {get;}
    public event Action<bool>? EnabledChanged;
    public event Action<QuotaReading?>? ReadingChanged;
    public QuotaReading? CurrentReading=>shown;
    public string Provider=>provider;
    public bool QuotaEnabled {get=>enabled.IsChecked==true;set=>enabled.IsChecked=value;}
    public CodexQuotaPanel(bool demo,Func<CancellationToken,QuotaReading> read,bool inlineSettings=true,UiLanguage? language=null,string provider="Codex") {
        if(!QuotaSession.Providers.Contains(provider))throw new ArgumentException("Unsupported quota provider",nameof(provider));
        ArgumentNullException.ThrowIfNull(read);
        this.provider=provider;enabled.Name="Enable"+provider+"Quota";refresh.Name="Refresh"+provider+"Quota";
        this.demo=demo;
        this.language=language??new UiLanguage();
        this.language.Set(enabled,"Show "+provider+" quota");this.language.Set(refresh,"Refresh");this.language.Set(status,"Off");
        this.language.Changed+=OnLanguageChanged;
        session=new QuotaSession((_,cancel)=>read(cancel));
        var body=new StackPanel{Spacing=12};
        var header=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10};
        header.Children.Add(AppIcon.Create(provider.ToLowerInvariant()));
        header.Children.Add(new TextBlock{Text=provider,FontSize=21,FontWeight=FontWeight.SemiBold});
        body.Children.Add(header);
        var settings=new StackPanel{Spacing=12};settings.Children.Add(enabled);
        settings.Children.Add(this.language.Set(new TextBlock{TextWrapping=TextWrapping.Wrap,Opacity=.75},demo?"Demo · Sample quota only. No account is accessed.":provider=="Antigravity"?"Reads the current user's running Antigravity on Windows. Refreshes every five minutes while enabled. Other platforms are unavailable.":provider=="Claude"?"Uses your existing Claude Code login, including macOS Keychain. Refreshes every five minutes while enabled. Pulse never signs in or refreshes tokens.":"Uses your existing Codex file login. Refreshes every five minutes while enabled. This choice is remembered. Login and token refresh stay in Codex."));
        SettingsContent=settings;if(inlineSettings)body.Children.Add(settings);
        if(!inlineSettings)this.language.Set(status,"Off · Enable in Settings → AI Quota");
        body.Children.Add(refresh);body.Children.Add(status);body.Children.Add(windows);
        Content=ReadingCard.Apply(new Border{Child=body});
        enabled.PropertyChanged+=(_,e)=>{if(e.Property==ToggleButton.IsCheckedProperty)SetEnabled();};
        refresh.Click+=(_,_)=>{if(!disposed&&enabled.IsChecked==true){session.Refresh();Tick();}};
        timer.Tick+=(_,_)=>Tick();
    }
    void SetEnabled() {
        if(disposed)return;
        bool on=enabled.IsChecked==true;
        session.Enable(provider,on);refresh.IsEnabled=on;shown=null;windows.Children.Clear();
        ReadingChanged?.Invoke(null);
        if(on){timer.Start();Tick();}else{timer.Stop();language.Set(status,"Off");}
        EnabledChanged?.Invoke(on);
    }
    void Tick() {
        if(disposed||enabled.IsChecked!=true)return;
        session.Tick(DateTimeOffset.UtcNow);
        var reading=session.Readings.Single();
        if(ReferenceEquals(reading,shown))return;
        Render(reading);
    }
    void OnLanguageChanged(){if(shown!=null)Render(shown);}
    void Render(QuotaReading reading) {
        shown=reading;windows.Children.Clear();
        status.Text=(demo?language.T("Demo")+" · ":"")+language.T(reading.Status);
        if(reading.Observed!=default)status.Text+=" · "+string.Format(language.T("Updated {0}"),reading.Observed.ToLocalTime().ToString("t"));
        if(reading.Status=="Login required")status.Text+=" · "+language.T(provider=="Antigravity"?"Open Antigravity to read quota":provider=="Claude"?"Sign in through Claude Code, then refresh here.":"Sign in through Codex, then refresh here. This preview requires file-based login.");
        var rows=reading.AllWindows.Count>0?reading.AllWindows:reading.Windows;
        foreach(var row in rows) {
            var item=new StackPanel{Spacing=6};
            item.Children.Add(new TextBlock{Text=language.T(row.Label),FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.Wrap});
            item.Children.Add(new TextBlock{Text=row.Remaining.HasValue?string.Format(language.T("{0}% left"),row.Remaining.Value.ToString("F1")):"—",FontSize=20});
            if(row.Remaining.HasValue)item.Children.Add(new ProgressBar{Minimum=0,Maximum=100,Value=row.Remaining.Value,Height=4});
            item.Children.Add(new TextBlock{Text=row.Reset.HasValue?string.Format(language.T("Resets {0}"),row.Reset.Value.ToLocalTime().ToString("g")):language.T("Reset time unavailable"),Opacity=.75,TextWrapping=TextWrapping.Wrap});
            windows.Children.Add(item);
        }
        ReadingChanged?.Invoke(reading);
    }
    public void Dispose() {
        if(disposed)return;disposed=true;timer.Stop();session.Dispose();
        language.Changed-=OnLanguageChanged;
        enabled.IsEnabled=false;refresh.IsEnabled=false;
    }
}
