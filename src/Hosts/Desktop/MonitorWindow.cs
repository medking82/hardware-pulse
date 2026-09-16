using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Styling;

namespace HardwarePulse.Desktop;

public sealed class MonitorWindow : Window {
    readonly MonitorSource source;
    readonly bool smoke;
    readonly bool measure;
    readonly CancellationTokenSource stop=new();
    readonly TextBlock cpu=Value(),ram=Value(),down=Value(),up=Value();
    readonly TextBlock status=new(){Text="Starting…",TextWrapping=TextWrapping.Wrap};
    readonly Grid cards=new(){ColumnDefinitions=new("*,*"),RowDefinitions=new("Auto,Auto")};
    readonly Border[] panels;
    readonly ComboBox interfaces=new(){HorizontalAlignment=HorizontalAlignment.Stretch,PlaceholderText="Select network interface"};
    readonly CheckBox pause=new(){Name="PauseHardware",Content="Pause hardware monitoring"};
    readonly ComboBox readingMode=new(){Name="ReadingMode",ItemsSource=new[]{"Live","Session Max"},SelectedIndex=0,MinWidth=160};
    MonitorSnapshot? latestSnapshot;
    readonly CodexQuotaPanel quota;
    readonly HardwareSensorPanel sensors=new();
    readonly PreviewSettingsStore? store;
    readonly PreviewSettings settings;
    readonly DispatcherTimer saveTimer=new(){Interval=TimeSpan.FromMilliseconds(500)};
    readonly TextBlock saveStatus=new(){TextWrapping=TextWrapping.Wrap};
    bool loadingNetwork;
    public Task Sampling {get;private set;}=Task.CompletedTask;
    public MonitorWindow(MonitorSource source,bool smoke=false,bool start=true,PreviewSettingsStore? store=null,bool measure=false) {
        this.source=source;this.smoke=smoke;this.measure=measure;
        this.store=store;settings=store?.Load()??new PreviewSettings();
        Title="Pulse · Desktop preview";Width=settings.Width;Height=settings.Height;MinWidth=360;MinHeight=400;
        FontSize=15;
        var heading=new TextBlock{Text="Pulse",FontSize=32,FontWeight=FontWeight.SemiBold};
        panels=[Card("CPU",cpu,"System load","cpu"),Card("Memory",ram,OperatingSystem.IsMacOS()?"Used memory estimate":"Host memory","memory"),Card("Download",down,"Selected interface","down"),Card("Upload",up,"Selected interface","up")];
        foreach(var panel in panels)cards.Children.Add(panel);
        var body=new StackPanel{Spacing=16,Margin=new Thickness(24)};
        body.Children.Add(readingMode);body.Children.Add(status);body.Children.Add(cards);body.Children.Add(pause);
        readingMode.SelectionChanged+=(_,_)=>{if(latestSnapshot!=null)Render(latestSnapshot);};
        body.Children.Add(sensors);
        quota=new CodexQuotaPanel(source.IsDemo,inlineSettings:false);body.Children.Add(quota);
        body.Children.Add(new TextBlock{Text="Preview · FPS and Desktop overlay are not connected yet. Hardware support depends on the platform and device.",TextWrapping=TextWrapping.Wrap,Opacity=.75});
        var network=new StackPanel{Spacing=12,Margin=new Thickness(20)};
        network.Children.Add(new TextBlock{Text="Network interface",FontSize=21,FontWeight=FontWeight.SemiBold});network.Children.Add(interfaces);
        network.Children.Add(new TextBlock{Text="Download and upload show the selected interface. A missing saved interface stays unselected until you choose another.",TextWrapping=TextWrapping.Wrap});
        var appearance=new StackPanel{Spacing=12,Margin=new Thickness(20)};
        appearance.Children.Add(new TextBlock{Text="Appearance",FontSize=21,FontWeight=FontWeight.SemiBold});
        var theme=new ComboBox{Name="PreviewTheme",ItemsSource=new[]{"System","Light","Dark"},SelectedItem=settings.Theme,HorizontalAlignment=HorizontalAlignment.Stretch};
        appearance.Children.Add(new TextBlock{Text="Theme"});appearance.Children.Add(theme);
        appearance.Children.Add(new TextBlock{Text="System follows your desktop theme. Window size is remembered automatically.",TextWrapping=TextWrapping.Wrap});
        var settingsTabs=new TabControl{Name="SettingsTabs",ItemsSource=new[]{
            new TabItem{Header="Network",Content=network},new TabItem{Header="Appearance",Content=appearance},
            new TabItem{Header="Codex",Content=new Border{Padding=new Thickness(20),Child=quota.SettingsContent}}}};
        var settingsBody=new StackPanel{Spacing=12,Margin=new Thickness(12)};
        settingsBody.Children.Add(settingsTabs);settingsBody.Children.Add(saveStatus);
        var tabs=new TabControl{Name="MainTabs",ItemsSource=new[]{new TabItem{Header="Monitor",Content=Scroll(body)},new TabItem{Header="Settings",Content=Scroll(settingsBody)}}};
        var root=new DockPanel();DockPanel.SetDock(heading,Dock.Top);heading.Margin=new Thickness(24,20,24,12);root.Children.Add(heading);root.Children.Add(tabs);Content=root;
        saveStatus.Text=store?.Error??(store==null?"Session only · Changes will not be saved.":"Changes save automatically.");
        theme.SelectionChanged+=(_,_)=>{settings.Theme=theme.SelectedItem as string??"System";ApplyTheme();SaveLater();};ApplyTheme();
        interfaces.SelectionChanged+=(_,_)=>{if(!loadingNetwork){settings.Network=interfaces.SelectedItem as string;SaveLater();}};
        quota.EnabledChanged+=on=>{settings.Codex=on;SaveLater();};
        quota.QuotaEnabled=settings.Codex;
        saveTimer.Tick+=(_,_)=>SaveNow();
        SizeChanged+=(_,_)=>{LayoutCards();if(WindowState==WindowState.Normal){settings.Width=Width;settings.Height=Height;SaveLater();}};LayoutCards();
        Opened+=(_,_)=>{
            var screen=Screens.ScreenFromWindow(this);
            if(screen!=null){Width=Math.Max(MinWidth,Math.Min(Width,screen.WorkingArea.Width/screen.Scaling));Height=Math.Max(MinHeight,Math.Min(Height,screen.WorkingArea.Height/screen.Scaling));}
        };
        if(start)Opened+=(_,_)=>Sampling=SampleAsync();
        Closed+=(_,_)=>{stop.Cancel();quota.Dispose();SaveNow();};
    }
    static ScrollViewer Scroll(Control content)=>new(){Content=content,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled};
    void ApplyTheme()=>RequestedThemeVariant=settings.Theme=="Dark"?ThemeVariant.Dark:settings.Theme=="Light"?ThemeVariant.Light:ThemeVariant.Default;
    void SaveLater(){if(store==null)return;saveTimer.Stop();saveTimer.Start();}
    void SaveNow(){saveTimer.Stop();if(store!=null)saveStatus.Text=store.Save(settings)?"Changes saved.":store.Error;}
    static TextBlock Value()=>new(){Text="—",FontSize=23,FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.Wrap};
    static Border Card(string title,TextBlock value,string detail,string icon) {
        var stack=new StackPanel{Spacing=10};
        var header=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10};
        header.Children.Add(AppIcon.Create(icon));
        header.Children.Add(new TextBlock{Text=title,FontWeight=FontWeight.SemiBold,VerticalAlignment=VerticalAlignment.Center});
        stack.Children.Add(header);stack.Children.Add(value);
        stack.Children.Add(new TextBlock{Text=detail,Opacity=.75,TextWrapping=TextWrapping.Wrap});
        return new Border{Child=stack,Padding=new Thickness(20),Margin=new Thickness(0,0,12,12),CornerRadius=new CornerRadius(14),BorderThickness=new Thickness(1),BorderBrush=Brushes.Gray};
    }
    void LayoutCards() {
        int count=ClientSize.Width>=660?2:1;
        cards.ColumnDefinitions=new(count==2?"*,*":"*");
        cards.RowDefinitions=new(count==2?"Auto,Auto":"Auto,Auto,Auto,Auto");
        for(int i=0;i<panels.Length;i++){Grid.SetRow(panels[i],i/count);Grid.SetColumn(panels[i],i%count);}
    }
    public void Present(MonitorSnapshot snapshot) {
        latestSnapshot=snapshot;Render(snapshot);
    }
    void Render(MonitorSnapshot snapshot) {
        bool max=readingMode.SelectedIndex==1;
        cpu.Text=max?snapshot.PeakCpu:snapshot.Cpu;ram.Text=snapshot.Memory;
        down.Text=max?snapshot.PeakDownload:snapshot.Download;up.Text=max?snapshot.PeakUpload:snapshot.Upload;
        sensors.Present(max?snapshot.PeakSensors:snapshot.Sensors,snapshot.SensorsSupported);
        status.Text=max?(source.IsDemo?"Demo · ":"")+"Session Max · Memory and quota remain current":source.IsDemo?"Demo · Sample values":snapshot.CpuReady&&snapshot.MemoryReady?"Live · Refreshes every second":"Waiting for available readings…";
    }
    async Task SampleAsync() {
        try {
            var names=await Task.Run(source.Interfaces,stop.Token);
            if(stop.IsCancellationRequested)return;
            loadingNetwork=true;interfaces.ItemsSource=names;
            if(settings.Network!=null)interfaces.SelectedItem=names.FirstOrDefault(name=>name==settings.Network);
            else if(names.Length>0)interfaces.SelectedIndex=0;
            loadingNetwork=false;
            int samples=0;
            var measurement=measure?new AppMeasurement(source.IsDemo):null;
            using var timer=new PeriodicTimer(TimeSpan.FromSeconds(1));
            do {
                if(pause.IsChecked==true){status.Text="Paused";continue;}
                string? name=interfaces.SelectedItem as string;
                var snapshot=await Task.Run(()=>source.Poll(name),stop.Token);
                if(stop.IsCancellationRequested)return;
                Present(snapshot);
                var report=measurement?.Observe(snapshot);
                if(report!=null)Console.WriteLine("BENCH_DESKTOP "+System.Text.Json.JsonSerializer.Serialize(report));
                if((smoke&&++samples==3)||report!=null) {
                    if(!snapshot.CpuReady||!snapshot.MemoryReady)Environment.ExitCode=3;
                    Console.WriteLine(snapshot.CpuReady&&snapshot.MemoryReady?(source.IsDemo?"PASS native Desktop UI with explicit demo values":"PASS native Desktop UI and live CPU/RAM"):"FAIL native Desktop telemetry");
                    Close();return;
                }
            } while(await timer.WaitForNextTickAsync(stop.Token));
        } catch(OperationCanceledException) when(stop.IsCancellationRequested){}
        catch(Exception) {if(!stop.IsCancellationRequested)status.Text="Monitoring unavailable. Close and reopen to retry.";if(smoke||measure){Environment.ExitCode=3;Console.WriteLine("FAIL native Desktop monitoring");Close();}}
    }
}
