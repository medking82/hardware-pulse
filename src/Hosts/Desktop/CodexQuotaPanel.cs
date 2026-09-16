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
    QuotaReading? shown;
    bool disposed;
    public Control SettingsContent {get;}
    public event Action<bool>? EnabledChanged;
    public bool QuotaEnabled {get=>enabled.IsChecked==true;set=>enabled.IsChecked=value;}
    public CodexQuotaPanel(bool demo,Func<CancellationToken,QuotaReading>? read=null,bool inlineSettings=true) {
        this.demo=demo;
        session=new QuotaSession((_,cancel)=>read!=null?read(cancel):Read(demo,cancel));
        var body=new StackPanel{Spacing=12};
        var header=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10};
        header.Children.Add(AppIcon.Create("codex"));
        header.Children.Add(new TextBlock{Text="Codex",FontSize=21,FontWeight=FontWeight.SemiBold});
        body.Children.Add(header);
        var settings=new StackPanel{Spacing=12};settings.Children.Add(enabled);
        settings.Children.Add(new TextBlock{Text=demo?"Demo · Sample quota only. No account is accessed.":"Uses your existing Codex file login. Refreshes every five minutes while enabled. This choice is remembered. Login and token refresh stay in Codex.",TextWrapping=TextWrapping.Wrap,Opacity=.75});
        SettingsContent=settings;if(inlineSettings)body.Children.Add(settings);
        if(!inlineSettings)status.Text="Off · Enable in Settings → Codex";
        body.Children.Add(refresh);body.Children.Add(status);body.Children.Add(windows);
        Content=new Border{Child=body,Padding=new Thickness(20),CornerRadius=new CornerRadius(14),BorderBrush=Brushes.Gray,BorderThickness=new Thickness(1)};
        enabled.PropertyChanged+=(_,e)=>{if(e.Property==ToggleButton.IsCheckedProperty)SetEnabled();};
        refresh.Click+=(_,_)=>{if(!disposed&&enabled.IsChecked==true){session.Refresh();Tick();}};
        timer.Tick+=(_,_)=>Tick();
    }
    static QuotaReading Read(bool demo,CancellationToken cancel) {
        cancel.ThrowIfCancellationRequested();
        if(demo) {
            var rows=new List<QuotaWindow>{new(){Label="5-hour",Remaining=72.5,Reset=DateTimeOffset.UtcNow.AddHours(2)},new(){Label="Weekly",Remaining=54.0,Reset=DateTimeOffset.UtcNow.AddDays(4)}};
            return new(){Provider="Codex",Status="Live",Observed=DateTimeOffset.UtcNow,Windows=rows,AllWindows=rows};
        }
        using FileCodexQuota adapter=OperatingSystem.IsLinux()?new LinuxCodexQuota():OperatingSystem.IsMacOS()?new MacCodexQuota():OperatingSystem.IsWindows()?new WindowsFileCodexQuota():throw new PlatformNotSupportedException();
        return adapter.Read(cancel);
    }
    void SetEnabled() {
        if(disposed)return;
        bool on=enabled.IsChecked==true;
        session.Enable("Codex",on);refresh.IsEnabled=on;shown=null;windows.Children.Clear();
        if(on){timer.Start();Tick();}else{timer.Stop();status.Text="Off";}
        EnabledChanged?.Invoke(on);
    }
    void Tick() {
        if(disposed||enabled.IsChecked!=true)return;
        session.Tick(DateTimeOffset.UtcNow);
        var reading=session.Readings.Single();
        if(ReferenceEquals(reading,shown))return;
        shown=reading;windows.Children.Clear();
        status.Text=(demo?"Demo · ":"")+reading.Status;
        if(reading.Observed!=default)status.Text+=$" · Updated {reading.Observed.ToLocalTime():t}";
        if(reading.Status=="Login required")status.Text+=" · Sign in through Codex, then refresh here. This preview requires file-based login.";
        var rows=reading.AllWindows.Count>0?reading.AllWindows:reading.Windows;
        foreach(var row in rows) {
            var item=new StackPanel{Spacing=6};
            item.Children.Add(new TextBlock{Text=row.Label,FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.Wrap});
            item.Children.Add(new TextBlock{Text=row.Remaining.HasValue?$"{row.Remaining.Value:F1}% left":"—",FontSize=20});
            if(row.Remaining.HasValue)item.Children.Add(new ProgressBar{Minimum=0,Maximum=100,Value=row.Remaining.Value,Height=4});
            item.Children.Add(new TextBlock{Text=row.Reset.HasValue?$"Resets {row.Reset.Value.ToLocalTime():g}":"Reset time unavailable",Opacity=.75,TextWrapping=TextWrapping.Wrap});
            windows.Children.Add(item);
        }
    }
    public void Dispose() {
        if(disposed)return;disposed=true;timer.Stop();session.Dispose();
        enabled.IsEnabled=false;refresh.IsEnabled=false;
    }
}
