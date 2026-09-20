using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Styling;
using Avalonia.Platform;

namespace HardwarePulse.Desktop;

public sealed class MonitorWindow : Window {
    readonly IMonitorSource source;
    readonly bool smoke;
    readonly bool measure;
    readonly CancellationTokenSource stop=new();
    readonly TextBlock status=new(){Text="Starting…",TextWrapping=TextWrapping.Wrap};
    readonly TextBlock hardwareStatus=new(){Name="HardwareStatus",TextWrapping=TextWrapping.Wrap,IsVisible=false};
    readonly Grid cards=new(){Name="ReadingCards",ColumnSpacing=10,RowSpacing=6};
    readonly DeviceCard[] panels;
    readonly CheckBox details=new(){Name="Details",Content="Details"};
    int cardColumns,cardVisibility=-1;
    readonly ComboBox interfaces=new(){HorizontalAlignment=HorizontalAlignment.Stretch,PlaceholderText="Select network interface"};
    readonly Button refreshInterfaces=new(){Name="RefreshInterfaces",Content="Refresh interfaces"};
    readonly TextBlock networkStatus=new(){Name="NetworkStatus",TextWrapping=TextWrapping.Wrap};
    readonly CheckBox pause=new(){Name="PauseHardware",Content="Pause hardware monitoring"};
    readonly ComboBox readingMode=new(){Name="ReadingMode",ItemsSource=new[]{"Live","Session Max"},SelectedIndex=0,MinWidth=160};
    MonitorSnapshot? latestSnapshot;
    public FloatingMonitorWindow? FloatingMonitor {get;private set;}
    bool samplingFailed;
    readonly CodexQuotaPanel quota;
    readonly HardwareSensorPanel sensors;
    public UiLanguage Language {get;}
    readonly PreviewSettingsStore? store;
    readonly PreviewSettings settings;
    readonly DispatcherTimer saveTimer=new(){Interval=TimeSpan.FromMilliseconds(500)};
    readonly TextBlock saveStatus=new(){TextWrapping=TextWrapping.Wrap};
    readonly Slider appOpacity=new(){Name="AppOpacity",Minimum=0,Maximum=100};
    readonly TextBlock opacityValue=new(){Name="AppOpacityValue"};
    readonly IPlatformSettings? materialPlatform=Application.Current?.PlatformSettings;
    readonly Border settingsSurface=new(){Name="SettingsSurface"};
    readonly DockPanel viewport=new(){Name="Viewport"};
    bool loadingNetwork;
    public Task Sampling {get;private set;}=Task.CompletedTask;
    public MonitorWindow(IMonitorSource source,bool smoke=false,bool start=true,PreviewSettingsStore? store=null,bool measure=false) {
        this.source=source;this.smoke=smoke;this.measure=measure;
        this.store=store;settings=store?.Load()??new PreviewSettings();
        Language=new UiLanguage(settings.Language);sensors=new HardwareSensorPanel(Language);
        void ApplyLanguageFont(){var family=DesktopFonts.ForLanguage(Language.EffectiveLanguage);if(family is null)ClearValue(FontFamilyProperty);else FontFamily=family;}
        Language.Changed+=ApplyLanguageFont;ApplyLanguageFont();
        Title="Pulse · Desktop preview";Width=settings.Width;Height=settings.Height;MinWidth=360;MinHeight=400;
        FontSize=15;
        var heading=Language.Set(new TextBlock{FontSize=32,FontWeight=FontWeight.SemiBold},"Pulse");
        panels=new[]{"CPU","GPU","Memory","NVMe","Airflow","Network"}.Select(key=>new DeviceCard(key,Language)).ToArray();
        foreach(var panel in panels)cards.Children.Add(panel);
        var body=new StackPanel{Spacing=16,Margin=new Thickness(24)};
        var modes=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};modes.Children.Add(readingMode);modes.Children.Add(Language.Set(details,"Details"));
        body.Children.Add(modes);body.Children.Add(status);body.Children.Add(hardwareStatus);body.Children.Add(cards);body.Children.Add(pause);
        details.IsChecked=settings.Details;
        details.IsCheckedChanged+=(_,_)=>{settings.Details=details.IsChecked==true;if(latestSnapshot!=null)Render(latestSnapshot);SaveLater();};
        Language.Changed+=()=>{if(latestSnapshot!=null)Render(latestSnapshot);};
        var floating=Language.Set(new Button{Name="OpenFloatingMonitor"},"Open floating monitor");
        floating.Click+=(_,_)=>OpenFloatingMonitor();body.Children.Add(floating);
        readingMode.SelectionChanged+=(_,_)=>{if(latestSnapshot!=null)Render(latestSnapshot);};
        body.Children.Add(sensors);
        quota=new CodexQuotaPanel(source.IsDemo,inlineSettings:false,language:Language);body.Children.Add(quota);
        body.Children.Add(Language.Set(new TextBlock{TextWrapping=TextWrapping.Wrap},"Preview · FPS and Desktop overlay are not connected yet. Hardware support depends on the platform and device."));
        var network=new StackPanel{Spacing=12,Margin=new Thickness(20)};
        network.Children.Add(Language.Set(new TextBlock{FontSize=21,FontWeight=FontWeight.SemiBold},"Network interface"));network.Children.Add(interfaces);
        network.Children.Add(refreshInterfaces);network.Children.Add(networkStatus);
        refreshInterfaces.Click+=async (_,_)=>await RefreshInterfacesAsync();
        network.Children.Add(Language.Set(new TextBlock{TextWrapping=TextWrapping.Wrap},"Download and upload show the selected interface. A missing saved interface stays unselected until you choose another."));
        var appearance=new StackPanel{Spacing=12,Margin=new Thickness(20)};
        appearance.Children.Add(Language.Set(new TextBlock{FontSize=21,FontWeight=FontWeight.SemiBold},"Appearance"));
        var theme=new ComboBox{Name="PreviewTheme",ItemsSource=new[]{"System","Light","Dark"},SelectedItem=settings.Theme,HorizontalAlignment=HorizontalAlignment.Stretch};
        appearance.Children.Add(Language.Set(new TextBlock{},"Theme"));appearance.Children.Add(theme);
        appearance.Children.Add(Language.Set(new TextBlock{TextWrapping=TextWrapping.Wrap},"System follows your desktop theme. Window size is remembered automatically."));
        var solid=Language.Set(new CheckBox{Name="AppSolid",IsChecked=settings.Solid},"Solid background");
        appearance.Children.Add(solid);
        appearance.Children.Add(Language.Set(new TextBlock(),"App background opacity"));
        appOpacity.Value=settings.AppOpacity;appearance.Children.Add(appOpacity);appearance.Children.Add(opacityValue);
        appOpacity.ValueChanged+=(_,_)=>{settings.AppOpacity=appOpacity.Value;RequestMaterial();SaveLater();};
        solid.IsCheckedChanged+=(_,_)=>{settings.Solid=solid.IsChecked==true;RequestMaterial();SaveLater();};
        appearance.Children.Add(Language.Set(new TextBlock(),"Language"));
        var languageChoice=new ComboBox{Name="PreviewLanguage",ItemsSource=new[]{"Auto (System)","English","简体中文","繁體中文"},SelectedIndex=settings.Language=="en"?1:settings.Language=="zh-CN"?2:settings.Language=="zh-TW"?3:0,HorizontalAlignment=HorizontalAlignment.Stretch,ItemTemplate=Language.Choices()};
        appearance.Children.Add(languageChoice);
        languageChoice.SelectionChanged+=(_,_)=>{settings.Language=languageChoice.SelectedIndex==1?"en":languageChoice.SelectedIndex==2?"zh-CN":languageChoice.SelectedIndex==3?"zh-TW":"auto";Language.Select(settings.Language);SaveLater();};
        var settingsTabs=new TabControl{Name="SettingsTabs",ItemsSource=new[]{
            Language.Set(new TabItem{Content=network},"Network"),Language.Set(new TabItem{Content=appearance},"Appearance"),
            new TabItem{Header="Codex",Content=new Border{Padding=new Thickness(20),Child=quota.SettingsContent}}}};
        var settingsBody=new StackPanel{Spacing=12,Margin=new Thickness(12)};
        settingsBody.Children.Add(settingsTabs);settingsBody.Children.Add(saveStatus);
        settingsSurface.Child=settingsBody;
        var tabs=new TabControl{Name="MainTabs",ItemsSource=new[]{Language.Set(new TabItem{Content=Scroll(body)},"Monitor"),Language.Set(new TabItem{Content=Scroll(settingsSurface)},"Settings")}};
        DockPanel.SetDock(heading,Dock.Top);heading.Margin=new Thickness(24,20,24,12);viewport.Children.Add(heading);viewport.Children.Add(tabs);Content=viewport;
        Language.Set(this,"Pulse · Desktop preview");Language.Set(status,"Starting…");Language.Set(pause,"Pause hardware monitoring");Language.Set(refreshInterfaces,"Refresh interfaces");
        theme.ItemTemplate=Language.Choices();readingMode.ItemTemplate=Language.Choices();
        void Placeholder()=>interfaces.PlaceholderText=Language.T("Select network interface");
        Language.Changed+=Placeholder;Placeholder();
        Language.Set(saveStatus,store?.Error??(store==null?"Session only · Changes will not be saved.":"Changes save automatically."));
        theme.SelectionChanged+=(_,_)=>{settings.Theme=theme.SelectedItem as string??"System";ApplyTheme();SaveLater();};ApplyTheme();
        PropertyChanged+=(_,e)=>{if(e.Property==ActualTransparencyLevelProperty||e.Property==ActualThemeVariantProperty)ApplyMaterial();};
        void ColorsChanged(object? sender,PlatformColorValues colors)=>RequestMaterial();
        if(materialPlatform!=null)materialPlatform.ColorValuesChanged+=ColorsChanged;
        RequestMaterial();
        interfaces.SelectionChanged+=(_,_)=>{if(!loadingNetwork){settings.Network=interfaces.SelectedItem as string;SaveLater();}};
        quota.EnabledChanged+=on=>{settings.Codex=on;SaveLater();};
        quota.QuotaEnabled=settings.Codex;
        saveTimer.Tick+=(_,_)=>SaveNow();
        SizeChanged+=(_,_)=>{LayoutCards();if(WindowState==WindowState.Normal){settings.Width=Width;settings.Height=Height;SaveLater();}};LayoutCards();
        Opened+=(_,_)=>{
            ApplyMaterial();
            var screen=Screens.ScreenFromWindow(this);
            if(screen!=null){Width=Math.Max(MinWidth,Math.Min(Width,screen.WorkingArea.Width/screen.Scaling));Height=Math.Max(MinHeight,Math.Min(Height,screen.WorkingArea.Height/screen.Scaling));}
        };
        if(start)Opened+=(_,_)=>Sampling=SampleAsync();
        Closed+=(_,_)=>{if(materialPlatform!=null)materialPlatform.ColorValuesChanged-=ColorsChanged;stop.Cancel();FloatingMonitor?.Close();quota.Dispose();SaveNow();};
    }
    public void OpenFloatingMonitor() {
        if(stop.IsCancellationRequested)return;
        if(FloatingMonitor==null) {
            FloatingMonitor=new FloatingMonitorWindow(Language){RequestedThemeVariant=RequestedThemeVariant};
            FloatingMonitor.Closed+=(_,_)=>FloatingMonitor=null;
        }
        if(latestSnapshot!=null)FloatingMonitor.Present(latestSnapshot,readingMode.SelectedIndex==1);
        FloatingMonitor.Show();if(!FloatingMonitor.SetLocked(false))return;
        if(FloatingMonitor.WindowState==WindowState.Minimized)FloatingMonitor.WindowState=WindowState.Normal;
        FloatingMonitor.Activate();
    }
    static ScrollViewer Scroll(Control content)=>new(){Content=content,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled};
    void ApplyTheme(){RequestedThemeVariant=settings.Theme=="Dark"?ThemeVariant.Dark:settings.Theme=="Light"?ThemeVariant.Light:ThemeVariant.Default;if(FloatingMonitor!=null)FloatingMonitor.RequestedThemeVariant=RequestedThemeVariant;}
    void RequestMaterial() {
        bool highContrast=materialPlatform?.GetColorValues().ContrastPreference==ColorContrastPreference.High;
        // Native/Backdrop.cs deliberately avoids focus-dependent system Acrylic.
        TransparencyLevelHint=settings.Solid||highContrast?[WindowTransparencyLevel.None]:
            OperatingSystem.IsWindows()||settings.AppOpacity==0?[WindowTransparencyLevel.Transparent]:[WindowTransparencyLevel.Blur];
        ApplyMaterial();
    }
    void ApplyMaterial() {
        bool highContrast=materialPlatform?.GetColorValues().ContrastPreference==ColorContrastPreference.High;
        var policy=new HardwarePulse.MaterialPolicy(settings.AppOpacity,false,false,settings.Solid,highContrast);
        bool supported=ActualTransparencyLevel!=WindowTransparencyLevel.None;
        // Avalonia's Windows Blur hint can fall back to plain transparency. Reuse
        // the original WPF backdrop owner; a transparent surface alone is not blur.
        if(OperatingSystem.IsWindows()) {
            var handle=TryGetPlatformHandle();
            supported=supported&&handle?.HandleDescriptor=="HWND"&&handle.Handle!=IntPtr.Zero&&PulseBackdrop.ApplyStable(handle.Handle,policy.Solid,policy.Clear);
        }
        double opacity=policy.EffectiveOpacity(supported);
        bool light=ActualThemeVariant==ThemeVariant.Light;
        // Match WPF Controls.ApplyMaterial: tint alpha changes, never the whole window.
        var tint=Color.Parse(light?"#F4F6F8":"#35383B");
        Background=new SolidColorBrush(Color.FromArgb((byte)Math.Round(255*opacity),tint.R,tint.G,tint.B));
        Foreground=Brush.Parse(light?"#17202B":"#F0F5FA");
        viewport.Background=new RadialGradientBrush {
            Center=new RelativePoint(.1,0,RelativeUnit.Relative),GradientOrigin=new RelativePoint(0,0,RelativeUnit.Relative),
            RadiusX=new RelativeScalar(1.3,RelativeUnit.Relative),RadiusY=new RelativeScalar(1,RelativeUnit.Relative),Opacity=opacity,
            GradientStops=[new GradientStop(Color.Parse("#404C9DAD"),0),new GradientStop(Color.Parse("#05152136"),.6),new GradientStop(Color.Parse("#302C3C68"),1)]};
        // WPF keeps Settings opaque even when the monitoring surface is clear.
        settingsSurface.Background=Brush.Parse(light?"#F4F6F8":"#202831");
        appOpacity.IsEnabled=policy.CanAdjustOpacity(supported);
        opacityValue.Text=Math.Round(opacity*100)+"%";
    }
    void SaveLater(){if(store==null)return;saveTimer.Stop();saveTimer.Start();}
    void SaveNow(){saveTimer.Stop();if(store!=null)Language.Set(saveStatus,store.Save(settings)?"Changes saved.":store.Error);}
    void LayoutCards() {
        int count=Math.Clamp((int)((ClientSize.Width-48)/270),1,3);
        int visibility=0;for(int i=0;i<panels.Length;i++)if(panels[i].IsVisible)visibility|=1<<i;
        if(cardColumns==count&&cardVisibility==visibility)return;
        cardColumns=count;cardVisibility=visibility;
        var visible=panels.Where(x=>x.IsVisible).ToArray();
        cards.ColumnDefinitions=new(string.Join(",",Enumerable.Repeat("*",count)));
        cards.RowDefinitions=new(string.Join(",",Enumerable.Repeat("Auto",Math.Max(1,(visible.Length+count-1)/count))));
        for(int i=0;i<visible.Length;i++){Grid.SetRow(visible[i],i/count);Grid.SetColumn(visible[i],i%count);}
    }
    public void Present(MonitorSnapshot snapshot) {
        latestSnapshot=snapshot;Render(snapshot);
    }
    void Render(MonitorSnapshot snapshot) {
        bool max=readingMode.SelectedIndex==1;
        foreach(var panel in panels)panel.Present(snapshot,max,details.IsChecked==true);
        LayoutCards();
        hardwareStatus.IsVisible=snapshot.Hardware!=null&&snapshot.Hardware.state!="LIVE";
        Language.Set(hardwareStatus,"Hardware readings unavailable. Waiting for the collector.");
        sensors.IsVisible=snapshot.SensorsSupported;
        sensors.Present(max?snapshot.PeakSensors:snapshot.Sensors,snapshot.SensorsSupported);
        FloatingMonitor?.Present(snapshot,max);
        Language.Set(status,samplingFailed?"Monitoring unavailable. Retrying…":max?(source.IsDemo?"Demo · ":"")+"Session Max · Memory and quota remain current":source.IsDemo?"Demo · Sample values":snapshot.CpuReady&&snapshot.MemoryReady?"Live · Refreshes every second":"Waiting for available readings…");
    }
    public void PresentInterfaces(string[] names) {
        // Preserve both the active choice and a temporarily absent saved device.
        string? preferred=interfaces.SelectedItem as string??settings.Network;
        loadingNetwork=true;
        try {
            interfaces.ItemsSource=names;
            interfaces.SelectedItem=preferred==null?names.FirstOrDefault():names.FirstOrDefault(name=>name==preferred);
            if(preferred!=null&&settings.Network==null)settings.Network=preferred;
        } finally {loadingNetwork=false;}
        Language.Set(networkStatus,names.Length==0?"No network interfaces available. Connect a device and refresh.":
            preferred!=null&&interfaces.SelectedItem==null?"Saved interface unavailable. Reconnect and refresh, or choose another interface.":"Interface list refreshed.");
    }
    async Task RefreshInterfacesAsync() {
        if(!refreshInterfaces.IsEnabled||stop.IsCancellationRequested)return;
        refreshInterfaces.IsEnabled=false;interfaces.IsEnabled=false;
        Language.Set(networkStatus,"Refreshing interfaces…");
        try {
            var names=await Task.Run(source.Interfaces,stop.Token);
            if(!stop.IsCancellationRequested)PresentInterfaces(names);
        } catch(OperationCanceledException) when(stop.IsCancellationRequested){}
        catch(Exception) {if(!stop.IsCancellationRequested)Language.Set(networkStatus,"Could not refresh interfaces. Try again.");}
        finally {refreshInterfaces.IsEnabled=true;interfaces.IsEnabled=true;}
    }
    async Task SampleAsync() {
        try {
            await RefreshInterfacesAsync();
            if(stop.IsCancellationRequested)return;
            int samples=0;
            var measurement=measure?new AppMeasurement(source.IsDemo):null;
            using var timer=new PeriodicTimer(TimeSpan.FromSeconds(1));
            do {
                if(pause.IsChecked==true){Language.Set(status,"Paused");continue;}
                string? name=interfaces.SelectedItem as string;
                MonitorSnapshot snapshot;
                try {snapshot=await Task.Run(()=>source.Poll(name),stop.Token);}
                catch(OperationCanceledException) when(stop.IsCancellationRequested){throw;}
                catch(Exception) when(!smoke&&!measure) {
                    if(stop.IsCancellationRequested)return;
                    samplingFailed=true;
                    var previous=latestSnapshot??new MonitorSnapshot("—","—","—","—",false,false);
                    Present(previous with {Cpu="—",Memory="—",Download="—",Upload="—",CpuReady=false,MemoryReady=false,
                        Hardware=previous.Hardware==null?null:new Reading{available=previous.Hardware.available,names=previous.Hardware.names,gpuFanCount=previous.Hardware.gpuFanCount},
                        Sensors=previous.Sensors.Select(sensor=>sensor with {Value="—"}).ToArray()});
                    continue;
                }
                if(stop.IsCancellationRequested)return;
                samplingFailed=false;
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
        catch(Exception) {if(!stop.IsCancellationRequested)Language.Set(status,"Monitoring unavailable. Close and reopen to retry.");if(smoke||measure){Environment.ExitCode=3;Console.WriteLine("FAIL native Desktop monitoring");Close();}}
    }
}
