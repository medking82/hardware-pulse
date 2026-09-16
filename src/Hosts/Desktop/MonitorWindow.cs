using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Styling;

namespace HardwarePulse.Desktop;

public sealed class MonitorWindow : Window {
    readonly IMonitorSource source;
    readonly bool smoke;
    readonly bool measure;
    readonly CancellationTokenSource stop=new();
    readonly TextBlock cpu=Value(),ram=Value(),down=Value(),up=Value();
    readonly TextBlock status=new(){Text="Starting…",TextWrapping=TextWrapping.Wrap};
    readonly Grid cards=new(){ColumnDefinitions=new("*,*"),RowDefinitions=new("Auto,Auto")};
    readonly Border[] panels;
    readonly ComboBox interfaces=new(){HorizontalAlignment=HorizontalAlignment.Stretch,PlaceholderText="Select network interface"};
    readonly Button refreshInterfaces=new(){Name="RefreshInterfaces",Content="Refresh interfaces"};
    readonly TextBlock networkStatus=new(){Name="NetworkStatus",TextWrapping=TextWrapping.Wrap};
    readonly CheckBox pause=new(){Name="PauseHardware",Content="Pause hardware monitoring"};
    readonly ComboBox readingMode=new(){Name="ReadingMode",ItemsSource=new[]{"Live","Session Max"},SelectedIndex=0,MinWidth=160};
    MonitorSnapshot? latestSnapshot;
    public FloatingMonitorWindow? FloatingMonitor {get;private set;}
    bool samplingFailed;
    readonly CodexQuotaPanel quota;
    readonly CodexQuotaPanel claudeQuota;
    readonly CodexQuotaPanel antigravityQuota;
    readonly HardwareSensorPanel sensors;
    readonly HardwareSensorPanel gpus;
    readonly TextBlock cpuIdentityText=new(){Name="CpuIdentity",Opacity=.75,TextWrapping=TextWrapping.Wrap,IsVisible=false};
    public UiLanguage Language {get;}
    readonly PreviewSettingsStore? store;
    readonly PreviewSettings settings;
    readonly DispatcherTimer saveTimer=new(){Interval=TimeSpan.FromMilliseconds(500)};
    readonly TextBlock saveStatus=new(){TextWrapping=TextWrapping.Wrap};
    readonly TextBlock materialStatus=new(){TextWrapping=TextWrapping.Wrap};
    readonly CheckBox desktopTopmost=new(){Name="DesktopTopmost"};
    bool loadingNetwork;
    public Task Sampling {get;private set;}=Task.CompletedTask;
    public MonitorWindow(IMonitorSource source,bool smoke=false,bool start=true,PreviewSettingsStore? store=null,bool measure=false) {
        this.source=source;this.smoke=smoke;this.measure=measure;
        this.store=store;settings=store?.Load()??new PreviewSettings();
        Language=new UiLanguage(settings.Language);sensors=new HardwareSensorPanel(Language);
        gpus=new HardwareSensorPanel(Language,"GPU","No GPU statistics exposed by this device."){Name="GpuReadings",IsVisible=false};
        void ApplyLanguageFont(){var family=DesktopFonts.ForLanguage(Language.EffectiveLanguage);if(family is null)ClearValue(FontFamilyProperty);else FontFamily=family;}
        Language.Changed+=ApplyLanguageFont;ApplyLanguageFont();
        Title="Pulse · Desktop preview";Width=settings.Width;Height=settings.Height;MinWidth=360;MinHeight=400;
        FontSize=15;
        var heading=Language.Set(new TextBlock{FontSize=32,FontWeight=FontWeight.SemiBold},"Pulse");
        panels=[Card("CPU",cpu,"System load","cpu"),Card("Memory",ram,OperatingSystem.IsMacOS()?"Used memory estimate":"Host memory","memory"),Card("Download",down,"Selected interface","down"),Card("Upload",up,"Selected interface","up")];
        ((StackPanel)panels[0].Child!).Children.Add(cpuIdentityText);
        foreach(var panel in panels)cards.Children.Add(panel);
        var body=new StackPanel{Spacing=16,Margin=new Thickness(24)};
        body.Children.Add(readingMode);body.Children.Add(status);body.Children.Add(cards);body.Children.Add(pause);
        var floating=Language.Set(new Button{Name="OpenFloatingMonitor"},"Open floating monitor");
        floating.Click+=(_,_)=>OpenFloatingMonitor();body.Children.Add(floating);
        readingMode.SelectionChanged+=(_,_)=>{if(latestSnapshot!=null)Render(latestSnapshot);};
        body.Children.Add(gpus);body.Children.Add(sensors);
        quota=new CodexQuotaPanel(source.IsDemo,cancel=>DesktopQuotaReaders.Read(source.IsDemo,"Codex",cancel),inlineSettings:false,language:Language);body.Children.Add(quota);
        quota.ReadingChanged+=reading=>FloatingMonitor?.PresentQuota(reading);
        claudeQuota=new CodexQuotaPanel(source.IsDemo,cancel=>DesktopQuotaReaders.Read(source.IsDemo,"Claude",cancel),inlineSettings:false,language:Language,provider:"Claude");body.Children.Add(claudeQuota);
        claudeQuota.ReadingChanged+=reading=>FloatingMonitor?.PresentQuota(reading,"Claude");
        antigravityQuota=new CodexQuotaPanel(source.IsDemo,cancel=>DesktopQuotaReaders.Read(source.IsDemo,"Antigravity",cancel),inlineSettings:false,language:Language,provider:"Antigravity");body.Children.Add(antigravityQuota);
        antigravityQuota.ReadingChanged+=reading=>FloatingMonitor?.PresentQuota(reading,"Antigravity");
        Language.Changed+=()=>{if(latestSnapshot!=null)Render(latestSnapshot);};
        body.Children.Add(Language.Set(new TextBlock{TextWrapping=TextWrapping.Wrap,Opacity=.75},"Preview · FPS and Desktop overlay are not connected yet. Hardware support depends on the platform and device."));
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
        appearance.Children.Add(Language.Set(new TextBlock(),"Language"));
        var languageChoice=new ComboBox{Name="PreviewLanguage",ItemsSource=new[]{"Auto (System)","English","简体中文","繁體中文"},SelectedIndex=settings.Language=="en"?1:settings.Language=="zh-CN"?2:settings.Language=="zh-TW"?3:0,HorizontalAlignment=HorizontalAlignment.Stretch,ItemTemplate=Language.Choices()};
        appearance.Children.Add(languageChoice);
        languageChoice.SelectionChanged+=(_,_)=>{settings.Language=languageChoice.SelectedIndex==1?"en":languageChoice.SelectedIndex==2?"zh-CN":languageChoice.SelectedIndex==3?"zh-TW":"auto";Language.Select(settings.Language);SaveLater();};
        var desktop=new StackPanel{Spacing=12,Margin=new Thickness(20)};
        desktop.Children.Add(Language.Set(new TextBlock{FontSize=21,FontWeight=FontWeight.SemiBold},"Floating monitor"));
        var desktopOpen=Language.Set(new Button(),"Open floating monitor");desktopOpen.Click+=(_,_)=>OpenFloatingMonitor();desktop.Children.Add(desktopOpen);
        desktopTopmost.IsChecked=settings.FloatingTopmost;
        desktopTopmost.IsCheckedChanged+=(_,_)=>{settings.FloatingTopmost=desktopTopmost.IsChecked==true;if(FloatingMonitor!=null)FloatingMonitor.Topmost=settings.FloatingTopmost;SaveLater();};
        desktop.Children.Add(Language.Set(desktopTopmost,"Always on top"));
        desktop.Children.Add(Language.Set(new TextBlock(),"Font size"));
        var fontValue=new TextBlock{Text=settings.FloatingFontSize.ToString("F0")};desktop.Children.Add(fontValue);
        var fontSize=new Slider{Name="FloatingFontSize",Minimum=10,Maximum=32,TickFrequency=1,IsSnapToTickEnabled=true,Value=settings.FloatingFontSize};
        Avalonia.Automation.AutomationProperties.SetName(fontSize,Language.T("Font size"));
        Language.Changed+=()=>Avalonia.Automation.AutomationProperties.SetName(fontSize,Language.T("Font size"));
        fontSize.ValueChanged+=(_,_)=>{settings.FloatingFontSize=fontSize.Value;fontValue.Text=fontSize.Value.ToString("F0");if(FloatingMonitor!=null)FloatingMonitor.FontSize=fontSize.Value;SaveLater();};
        desktop.Children.Add(fontSize);
        desktop.Children.Add(Language.Set(new TextBlock(),"Background opacity"));
        var opacityValue=new TextBlock{Text=settings.FloatingBackgroundOpacity.ToString("F0")+"%"};desktop.Children.Add(opacityValue);
        var opacity=new Slider{Name="FloatingBackgroundOpacity",Minimum=0,Maximum=100,TickFrequency=1,IsSnapToTickEnabled=true,Value=settings.FloatingBackgroundOpacity};
        Avalonia.Automation.AutomationProperties.SetName(opacity,Language.T("Background opacity"));
        Language.Changed+=()=>Avalonia.Automation.AutomationProperties.SetName(opacity,Language.T("Background opacity"));
        opacity.ValueChanged+=(_,_)=>{settings.FloatingBackgroundOpacity=opacity.Value;opacityValue.Text=opacity.Value.ToString("F0")+"%";FloatingMonitor?.SetBackgroundOpacity(opacity.Value);SaveLater();};
        desktop.Children.Add(opacity);
        var blur=Language.Set(new CheckBox{Name="FloatingBackgroundBlur",IsChecked=settings.FloatingBackgroundBlur},"Background blur");
        blur.IsCheckedChanged+=(_,_)=>{settings.FloatingBackgroundBlur=blur.IsChecked==true;FloatingMonitor?.SetBackgroundBlur(settings.FloatingBackgroundBlur);UpdateMaterialStatus();SaveLater();};
        desktop.Children.Add(blur);desktop.Children.Add(materialStatus);UpdateMaterialStatus();
        desktop.Children.Add(Language.Set(new TextBlock{TextWrapping=TextWrapping.Wrap},"Blur strength is controlled by the system. Lower background opacity to reveal the effect."));
        desktop.Children.Add(Language.Set(new TextBlock{TextWrapping=TextWrapping.Wrap},"Background and text opacity are independent. Unsupported transparency uses a solid background."));
        void AppearanceChanged(){FloatingMonitor?.ApplyTextAppearance();SaveLater();}
        void AppearanceSlider(string key,string name,double value,double maximum,Action<double> assign) {
            var label=Language.Set(new TextBlock(),key);desktop.Children.Add(label);
            var readout=new TextBlock{Text=value.ToString("F0")};desktop.Children.Add(readout);
            var slider=new Slider{Name=name,Minimum=0,Maximum=maximum,TickFrequency=1,IsSnapToTickEnabled=true,Value=value};
            Avalonia.Automation.AutomationProperties.SetName(slider,Language.T(key));
            Language.Changed+=()=>Avalonia.Automation.AutomationProperties.SetName(slider,Language.T(key));
            slider.ValueChanged+=(_,_)=>{assign(slider.Value);readout.Text=slider.Value.ToString("F0");AppearanceChanged();};desktop.Children.Add(slider);
        }
        AppearanceSlider("Text opacity (%)","FloatingTextOpacity",settings.FloatingTextOpacity,100,value=>settings.FloatingTextOpacity=value);
        AppearanceSlider("Row spacing (px)","FloatingRowSpacing",settings.FloatingRowSpacing,24,value=>settings.FloatingRowSpacing=value);
        desktop.Children.Add(Language.Set(new TextBlock(),"Text color (#RRGGBB, blank follows theme)"));
        var color=new TextBox{Name="FloatingTextColor",Text=settings.FloatingTextColor,MaxLength=7};
        var colorError=Language.Set(new TextBlock{IsVisible=false,TextWrapping=TextWrapping.Wrap},"Enter a six-digit hex color, such as #FFFFFF.");
        Avalonia.Automation.AutomationProperties.SetName(color,Language.T("Text color (#RRGGBB, blank follows theme)"));
        Language.Changed+=()=>Avalonia.Automation.AutomationProperties.SetName(color,Language.T("Text color (#RRGGBB, blank follows theme)"));
        color.TextChanged+=(_,_)=>{string value=color.Text??"";bool valid=PreviewSettings.IsTextColor(value);colorError.IsVisible=!valid;if(valid){settings.FloatingTextColor=value;AppearanceChanged();}};
        desktop.Children.Add(color);desktop.Children.Add(colorError);
        var follow=Language.Set(new CheckBox{Name="FloatingIconsFollowApp",IsChecked=settings.FloatingIconsFollowApp},"Icons follow App colors");
        follow.IsCheckedChanged+=(_,_)=>{settings.FloatingIconsFollowApp=follow.IsChecked==true;AppearanceChanged();};desktop.Children.Add(follow);
        var quotaSettings=new StackPanel{Spacing=24};quotaSettings.Children.Add(quota.SettingsContent);quotaSettings.Children.Add(claudeQuota.SettingsContent);quotaSettings.Children.Add(antigravityQuota.SettingsContent);
        var settingsTabs=new TabControl{Name="SettingsTabs",ItemsSource=new[]{
            Language.Set(new TabItem{Content=network},"Network"),Language.Set(new TabItem{Content=appearance},"Appearance"),
            Language.Set(new TabItem{Content=new Border{Padding=new Thickness(20),Child=quotaSettings}},"AI Quota"),Language.Set(new TabItem{Content=desktop},"Desktop")}};
        var settingsBody=new StackPanel{Spacing=12,Margin=new Thickness(12)};
        settingsBody.Children.Add(settingsTabs);settingsBody.Children.Add(saveStatus);
        var tabs=new TabControl{Name="MainTabs",ItemsSource=new[]{Language.Set(new TabItem{Content=Scroll(body)},"Monitor"),Language.Set(new TabItem{Content=Scroll(settingsBody)},"Settings")}};
        var root=new DockPanel();DockPanel.SetDock(heading,Dock.Top);heading.Margin=new Thickness(24,20,24,12);root.Children.Add(heading);root.Children.Add(tabs);Content=root;
        Language.Set(this,"Pulse · Desktop preview");Language.Set(status,"Starting…");Language.Set(pause,"Pause hardware monitoring");Language.Set(refreshInterfaces,"Refresh interfaces");
        theme.ItemTemplate=Language.Choices();readingMode.ItemTemplate=Language.Choices();
        void Placeholder()=>interfaces.PlaceholderText=Language.T("Select network interface");
        Language.Changed+=Placeholder;Placeholder();
        Language.Set(saveStatus,store?.Error??(store==null?"Session only · Changes will not be saved.":"Changes save automatically."));
        theme.SelectionChanged+=(_,_)=>{settings.Theme=theme.SelectedItem as string??"System";ApplyTheme();SaveLater();};ApplyTheme();
        interfaces.SelectionChanged+=(_,_)=>{if(!loadingNetwork){settings.Network=interfaces.SelectedItem as string;SaveLater();}};
        quota.EnabledChanged+=on=>{settings.Codex=on;SaveLater();};
        quota.QuotaEnabled=settings.Codex;
        claudeQuota.EnabledChanged+=on=>{settings.Claude=on;SaveLater();};
        claudeQuota.QuotaEnabled=settings.Claude;
        antigravityQuota.EnabledChanged+=on=>{settings.Antigravity=on;SaveLater();};
        antigravityQuota.QuotaEnabled=settings.Antigravity;
        saveTimer.Tick+=(_,_)=>SaveNow();
        SizeChanged+=(_,_)=>{LayoutCards();if(WindowState==WindowState.Normal){settings.Width=Width;settings.Height=Height;SaveLater();}};LayoutCards();
        Opened+=(_,_)=>{
            var screen=Screens.ScreenFromWindow(this);
            if(screen!=null){Width=Math.Max(MinWidth,Math.Min(Width,screen.WorkingArea.Width/screen.Scaling));Height=Math.Max(MinHeight,Math.Min(Height,screen.WorkingArea.Height/screen.Scaling));}
        };
        if(start)Opened+=StartSampling;
        Closed+=(_,_)=>{stop.Cancel();FloatingMonitor?.Close();quota.Dispose();claudeQuota.Dispose();antigravityQuota.Dispose();SaveNow();};
    }
    void StartSampling(object? sender,EventArgs args) {
        // Hide/Show raises Opened again. A Monitor owns exactly one polling loop.
        Opened-=StartSampling;
        Sampling=SampleAsync();
    }
    public void OpenFloatingMonitor() {
        if(stop.IsCancellationRequested)return;
        if(FloatingMonitor==null) {
            FloatingMonitor=new FloatingMonitorWindow(Language,settings,()=>{desktopTopmost.IsChecked=settings.FloatingTopmost;SaveLater();}){RequestedThemeVariant=RequestedThemeVariant};
            FloatingMonitor.MaterialChanged+=UpdateMaterialStatus;
            FloatingMonitor.Closed+=(_,_)=>{FloatingMonitor!.MaterialChanged-=UpdateMaterialStatus;FloatingMonitor=null;UpdateMaterialStatus();};
        }
        if(latestSnapshot!=null)FloatingMonitor.Present(latestSnapshot,readingMode.SelectedIndex==1);
        FloatingMonitor.PresentQuota(quota.CurrentReading);
        FloatingMonitor.PresentQuota(claudeQuota.CurrentReading,"Claude");
        FloatingMonitor.PresentQuota(antigravityQuota.CurrentReading,"Antigravity");
        FloatingMonitor.Show();if(!FloatingMonitor.SetLocked(false))return;
        if(FloatingMonitor.WindowState==WindowState.Minimized)FloatingMonitor.WindowState=WindowState.Normal;
        FloatingMonitor.Activate();
        UpdateMaterialStatus();
    }
    void UpdateMaterialStatus()=>Language.Set(materialStatus,FloatingMonitor?.MaterialStatus??"Open the floating monitor to check background effects.");
    static ScrollViewer Scroll(Control content)=>new(){Content=content,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled};
    void ApplyTheme(){RequestedThemeVariant=settings.Theme=="Dark"?ThemeVariant.Dark:settings.Theme=="Light"?ThemeVariant.Light:ThemeVariant.Default;if(FloatingMonitor!=null)FloatingMonitor.RequestedThemeVariant=RequestedThemeVariant;}
    void SaveLater(){if(store==null)return;saveTimer.Stop();saveTimer.Start();}
    void SaveNow(){saveTimer.Stop();if(store!=null)Language.Set(saveStatus,store.Save(settings)?"Changes saved.":store.Error);}
    static TextBlock Value()=>new(){Text="—",FontSize=23,FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.Wrap};
    Border Card(string title,TextBlock value,string detail,string icon) {
        var stack=new StackPanel{Spacing=10};
        var header=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10};
        header.Children.Add(AppIcon.Create(icon));
        header.Children.Add(Language.Set(new TextBlock{FontWeight=FontWeight.SemiBold,VerticalAlignment=VerticalAlignment.Center},title));
        stack.Children.Add(header);stack.Children.Add(value);
        stack.Children.Add(Language.Set(new TextBlock{Opacity=.75,TextWrapping=TextWrapping.Wrap},detail));
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
        gpus.IsVisible=snapshot.GpusSupported;
        gpus.Present((max?snapshot.PeakGpus:snapshot.Gpus).Select(x=>x with {Label=x.GpuLabel(Language)}).ToArray(),snapshot.GpusSupported);
        cpuIdentityText.IsVisible=snapshot.CpuModel!=null||snapshot.CpuPhysicalCores.HasValue||snapshot.CpuLogicalCores.HasValue;
        cpuIdentityText.Text=(snapshot.CpuModel??"—")+"\n"+string.Format(Language.T("{0} physical cores · {1} logical cores"),snapshot.CpuPhysicalCores?.ToString()??"—",snapshot.CpuLogicalCores?.ToString()??"—");
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
                        Sensors=previous.Sensors.Select(sensor=>sensor with {Value="—"}).ToArray(),
                        Gpus=previous.Gpus.Select(gpu=>gpu with {Value="—"}).ToArray()});
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
