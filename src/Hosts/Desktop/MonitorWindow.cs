using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Styling;
using Avalonia.Platform;
using Avalonia.Input;

namespace HardwarePulse.Desktop;

public sealed class MonitorWindow : Window {
    readonly IMonitorSource source;
    readonly bool smoke;
    readonly bool measure;
    readonly CancellationTokenSource stop=new();
    readonly TextBlock status=new(){Text="Starting…",TextWrapping=TextWrapping.Wrap};
    readonly TextBlock hardwareStatus=new(){Name="HardwareStatus",TextWrapping=TextWrapping.Wrap,IsVisible=false};
    readonly Grid cards=new(){Name="ReadingCards",ColumnSpacing=10};
    readonly DeviceCard[] panels;
    readonly TextBlock cardsEmpty=new(){Name="CardsEmpty",TextWrapping=TextWrapping.Wrap,IsVisible=false};
    readonly ToggleButton details=new(){Name="Details",Content="Details",Padding=new Thickness(7,5)};
    int cardColumns,cardVisibility=-1;
    readonly ComboBox interfaces=new(){HorizontalAlignment=HorizontalAlignment.Stretch,PlaceholderText="Select network interface"};
    readonly Button refreshInterfaces=new(){Name="RefreshInterfaces",Content="Refresh interfaces"};
    readonly TextBlock networkStatus=new(){Name="NetworkStatus",TextWrapping=TextWrapping.Wrap};
    readonly CheckBox pause=new(){Name="PauseHardware",Content="Pause hardware monitoring"};
    readonly ToggleButton liveMode=new(){Name="Live",IsChecked=true,Padding=new Thickness(7,5)};
    readonly ToggleButton maxMode=new(){Name="SessionMax",Padding=new Thickness(7,5)};
    bool sessionMax;
    bool settingsVisible;
    Border? titleDrag;
    ScrollViewer? monitorScroll;
    bool measuringDensity;
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
        FontSize=settings.FontSize;
        WindowDecorations=WindowDecorations.None;
        var heading=Language.Set(new TextBlock{FontSize=22,FontWeight=FontWeight.SemiBold},"Pulse");
        panels=settings.CardOrder.Select(key=>new DeviceCard(key,Language)).ToArray();
        cards.RowDefinitions=new(string.Join(",",Enumerable.Repeat("Auto",panels.Length)));
        foreach(var panel in panels)cards.Children.Add(panel);
        var body=new StackPanel{Spacing=16,Margin=new Thickness(24)};
        var modes=new WrapPanel{Name="MonitorControls",Orientation=Orientation.Horizontal};
        foreach(var control in new[]{Language.Set(liveMode,"Live"),Language.Set(maxMode,"Session Max"),Language.Set(details,"Details")}){control.Margin=new Thickness(0,0,6,6);modes.Children.Add(control);}
        body.Children.Add(hardwareStatus);body.Children.Add(cards);body.Children.Add(Language.Set(cardsEmpty,"No cards shown. Choose cards in Settings."));body.Children.Add(pause);
        details.IsChecked=settings.Details;
        details.IsCheckedChanged+=(_,_)=>{settings.Details=details.IsChecked==true;if(latestSnapshot!=null)Render(latestSnapshot);SaveLater();};
        Language.Changed+=()=>{if(latestSnapshot!=null)Render(latestSnapshot);};
        var floating=Language.Set(new Button{Name="OpenFloatingMonitor"},"Open floating monitor");
        floating.Click+=(_,_)=>OpenFloatingMonitor();body.Children.Add(floating);
        liveMode.Click+=(_,_)=>SelectMode(false);maxMode.Click+=(_,_)=>SelectMode(true);
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
        appearance.Children.Add(Language.Set(new TextBlock(),"Font size"));
        var font=new Slider{Name="AppFontSize",Minimum=10,Maximum=16,TickFrequency=1,IsSnapToTickEnabled=true,Value=settings.FontSize};
        var fontValue=new TextBlock{Text=settings.FontSize.ToString("0")+" DIP"};appearance.Children.Add(font);appearance.Children.Add(fontValue);
        font.ValueChanged+=(_,_)=>{settings.FontSize=font.Value;FontSize=font.Value;fontValue.Text=font.Value.ToString("0")+" DIP";LayoutCards();ApplyCardDensity();SaveLater();};
        var pin=Language.Set(new CheckBox{Name="AppTopmost",IsChecked=settings.Topmost},"Always on Top");
        var locked=Language.Set(new CheckBox{Name="AppLockPosition",IsChecked=settings.LockPosition},"Lock Position and Size");
        appearance.Children.Add(pin);appearance.Children.Add(locked);
        pin.IsCheckedChanged+=(_,_)=>{settings.Topmost=pin.IsChecked==true;ApplyWindowPreferences();SaveLater();};
        locked.IsCheckedChanged+=(_,_)=>{settings.LockPosition=locked.IsChecked==true;ApplyWindowPreferences();SaveLater();};
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
            new TabItem{Header="Codex",Content=new Border{Padding=new Thickness(20),Child=quota.SettingsContent}},
            Language.Set(new TabItem{Content=CreateCardSettings()},"App Cards")}};
        var settingsBody=new StackPanel{Spacing=12,Margin=new Thickness(12)};
        settingsBody.Children.Add(settingsTabs);settingsBody.Children.Add(saveStatus);
        var back=Language.Set(new Button{Name="Back",Padding=new Thickness(10,5)},"Back");
        var settingsTitle=Language.Set(new TextBlock{Name="SettingsTitle",FontWeight=FontWeight.SemiBold,VerticalAlignment=VerticalAlignment.Center},"Settings");
        var toolbar=new StackPanel{Name="SettingsToolbar",Orientation=Orientation.Horizontal,Spacing=10,Margin=new Thickness(14,8)};
        toolbar.Children.Add(back);toolbar.Children.Add(settingsTitle);
        var settingsPage=new DockPanel();DockPanel.SetDock(toolbar,Dock.Top);settingsPage.Children.Add(toolbar);settingsPage.Children.Add(Scroll(settingsBody));
        settingsSurface.Child=settingsPage;
        var openSettings=new Button{Name="OpenSettings",Width=36,Height=36,Padding=new Thickness(8),HorizontalAlignment=HorizontalAlignment.Right};
        var settingsIcon=AppIcon.Create("settings");
        settingsIcon.Bind(Avalonia.Controls.Shapes.Shape.FillProperty,this.GetObservable(ForegroundProperty));
        openSettings.Content=new Viewbox{Width=20,Height=20,Child=settingsIcon};
        void SettingsLabel(){ToolTip.SetTip(openSettings,Language.T("Settings"));Avalonia.Automation.AutomationProperties.SetName(openSettings,Language.T("Settings"));}
        Language.Changed+=SettingsLabel;SettingsLabel();
        var footer=new Border{Name="MonitorFooter",Margin=new Thickness(14,4,20,10),Child=openSettings};
        var monitorHeader=new StackPanel{Spacing=4,Margin=new Thickness(14,0,14,8)};monitorHeader.Children.Add(modes);monitorHeader.Children.Add(status);
        var monitorPage=new DockPanel{Name="MonitorPage"};
        DockPanel.SetDock(monitorHeader,Dock.Top);monitorPage.Children.Add(monitorHeader);
        DockPanel.SetDock(footer,Dock.Bottom);monitorPage.Children.Add(footer);monitorScroll=Scroll(body);monitorScroll.SizeChanged+=(_,_)=>ApplyCardDensity();monitorPage.Children.Add(monitorScroll);
        var pages=new ContentControl{Name="AppPage",HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch,Content=monitorPage};
        openSettings.Click+=(_,_)=>{settingsVisible=true;pages.Content=settingsSurface;RequestMaterial();back.Focus();};
        back.Click+=(_,_)=>{settingsVisible=false;pages.Content=monitorPage;RequestMaterial();openSettings.Focus();};
        var titlebar=CreateTitlebar(heading);DockPanel.SetDock(titlebar,Dock.Top);viewport.Children.Add(titlebar);viewport.Children.Add(pages);
        var frame=new Grid();frame.Children.Add(viewport);AddResizeEdges(frame);Content=frame;
        ApplyWindowPreferences();
        Language.Set(this,"Pulse · Desktop preview");Language.Set(status,"Starting…");Language.Set(pause,"Pause hardware monitoring");Language.Set(refreshInterfaces,"Refresh interfaces");
        theme.ItemTemplate=Language.Choices();
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
        SizeChanged+=(_,_)=>{LayoutCards();ApplyCardDensity();if(WindowState==WindowState.Normal){settings.Width=Width;settings.Height=Height;SaveLater();}};LayoutCards();
        Opened+=(_,_)=>{
            ApplyMaterial();
            var screen=Screens.ScreenFromWindow(this);
            if(screen!=null){Width=Math.Max(MinWidth,Math.Min(Width,screen.WorkingArea.Width/screen.Scaling));Height=Math.Max(MinHeight,Math.Min(Height,screen.WorkingArea.Height/screen.Scaling));}
        };
        if(start)Opened+=(_,_)=>Sampling=SampleAsync();
        Closed+=(_,_)=>{if(materialPlatform!=null)materialPlatform.ColorValuesChanged-=ColorsChanged;stop.Cancel();FloatingMonitor?.Close();quota.Dispose();SaveNow();};
    }
    void SelectMode(bool max) {
        sessionMax=max;liveMode.IsChecked=!max;maxMode.IsChecked=max;
        if(latestSnapshot!=null)Render(latestSnapshot);
    }
    public void OpenFloatingMonitor() {
        if(stop.IsCancellationRequested)return;
        if(FloatingMonitor==null) {
            FloatingMonitor=new FloatingMonitorWindow(Language){RequestedThemeVariant=RequestedThemeVariant};
            FloatingMonitor.Closed+=(_,_)=>FloatingMonitor=null;
        }
        if(latestSnapshot!=null)FloatingMonitor.Present(latestSnapshot,sessionMax);
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
            OperatingSystem.IsWindows()||settings.AppOpacity==0||(settings.LockPosition&&!settingsVisible)?[WindowTransparencyLevel.Transparent]:[WindowTransparencyLevel.Blur];
        ApplyMaterial();
    }
    Control CreateCardSettings() {
        var list=new StackPanel{Name="CardPreferences",Spacing=8,Margin=new Thickness(20)};
        var rows=new Dictionary<string,Grid>();
        var moves=new Dictionary<string,(Button Up,Button Down)>();
        void RefreshOrder() {
            list.Children.Clear();
            for(int i=0;i<settings.CardOrder.Count;i++) {
                string key=settings.CardOrder[i];list.Children.Add(rows[key]);
                moves[key].Up.IsEnabled=i>0&&!settings.LockPosition;moves[key].Down.IsEnabled=i<settings.CardOrder.Count-1&&!settings.LockPosition;
            }
        }
        void Move(string key,int delta) {
            if(settings.LockPosition)return;
            int index=settings.CardOrder.IndexOf(key),target=index+delta;if(target<0||target>=panels.Length)return;
            (settings.CardOrder[index],settings.CardOrder[target])=(settings.CardOrder[target],settings.CardOrder[index]);
            Array.Sort(panels,(a,b)=>settings.CardOrder.IndexOf(a.Key).CompareTo(settings.CardOrder.IndexOf(b.Key)));
            cards.Children.Clear();foreach(var panel in panels)cards.Children.Add(panel);
            cardVisibility=-1;LayoutCards();RefreshOrder();SaveLater();
            var action=delta<0?moves[key].Up:moves[key].Down;
            if(action.IsEnabled)action.Focus();else rows[key].Children[0].Focus();
        }
        foreach(string key in PreviewSettings.CardKeys) {
            var row=new Grid{ColumnDefinitions=new("*,Auto,Auto"),ColumnSpacing=6};rows[key]=row;
            var visible=Language.Set(new CheckBox{Name="ShowCard"+key,IsChecked=!settings.HiddenCards.Contains(key)},key);
            visible.IsCheckedChanged+=(_,_)=>{if(visible.IsChecked==true)settings.HiddenCards.Remove(key);else settings.HiddenCards.Add(key);if(latestSnapshot!=null)Render(latestSnapshot);SaveLater();};
            var up=new Button{Name="MoveCardUp"+key,Content="↑",Padding=new Thickness(8,3)};
            var down=new Button{Name="MoveCardDown"+key,Content="↓",Padding=new Thickness(8,3)};
            void Labels(){Avalonia.Automation.AutomationProperties.SetName(up,Language.T("Move up")+" · "+Language.T(key));Avalonia.Automation.AutomationProperties.SetName(down,Language.T("Move down")+" · "+Language.T(key));}
            Language.Changed+=Labels;Labels();up.Click+=(_,_)=>Move(key,-1);down.Click+=(_,_)=>Move(key,1);
            moves[key]=(up,down);row.Children.Add(visible);Grid.SetColumn(up,1);row.Children.Add(up);Grid.SetColumn(down,2);row.Children.Add(down);
        }
        list.AttachedToVisualTree+=(_,_)=>RefreshOrder();RefreshOrder();return list;
    }
    void ApplyWindowPreferences() {
        Topmost=settings.Topmost;CanResize=!settings.LockPosition;
        if(titleDrag!=null)titleDrag.Cursor=new Cursor(settings.LockPosition?StandardCursorType.Arrow:StandardCursorType.SizeAll);
        RequestMaterial();
    }
    Control CreateTitlebar(TextBlock heading) {
        var bar=new Grid{Name="Titlebar",ColumnDefinitions=new ColumnDefinitions("*,Auto,Auto"),Margin=new Thickness(14,10,14,8)};
        var brand=AppIcon.Create("live");brand.Stroke=Brush.Parse("#A5E7D5");
        var label=new StackPanel{Orientation=Orientation.Horizontal,Spacing=7,VerticalAlignment=VerticalAlignment.Center};
        label.Children.Add(new Viewbox{Width=22,Height=22,Child=brand});label.Children.Add(heading);
        var drag=titleDrag=new Border{Name="DragHandle",Background=Brushes.Transparent,Child=label,Cursor=new Cursor(StandardCursorType.SizeAll)};
        drag.PointerPressed+=(_,e)=>{if(!settings.LockPosition&&e.GetCurrentPoint(drag).Properties.IsLeftButtonPressed){BeginMoveDrag(e);e.Handled=true;}};
        bar.Children.Add(drag);
        Button Action(string name,string icon,string text,int column,Action action) {
            var glyph=AppIcon.Create(icon);glyph.Bind(Avalonia.Controls.Shapes.Shape.StrokeProperty,this.GetObservable(ForegroundProperty));
            var button=new Button{Name=name,Padding=new Thickness(10,3),VerticalAlignment=VerticalAlignment.Center,Content=new Viewbox{Width=14,Height=14,Child=glyph}};
            void Label(){ToolTip.SetTip(button,Language.T(text));Avalonia.Automation.AutomationProperties.SetName(button,Language.T(text));}
            Language.Changed+=Label;Label();button.Click+=(_,_)=>action();Grid.SetColumn(button,column);bar.Children.Add(button);return button;
        }
        Action("Minimize","minimize","Minimize",1,()=>WindowState=WindowState.Minimized);
        Action("Close","close","Close widget",2,Close);
        return bar;
    }
    void AddResizeEdges(Grid frame) {
        void Edge(WindowEdge edge,HorizontalAlignment horizontal,VerticalAlignment vertical,StandardCursorType cursor,bool corner=false) {
            var grip=new Border{Name="Resize"+edge,Background=Brushes.Transparent,HorizontalAlignment=horizontal,VerticalAlignment=vertical,Cursor=new Cursor(cursor)};
            if(horizontal!=HorizontalAlignment.Stretch)grip.Width=corner?10:5;
            if(vertical!=VerticalAlignment.Stretch)grip.Height=corner?10:5;
            grip.PointerPressed+=(_,e)=>{if(CanResize&&WindowState==WindowState.Normal&&e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed){BeginResizeDrag(edge,e);e.Handled=true;}};
            void State(){grip.IsVisible=CanResize&&WindowState==WindowState.Normal;}
            PropertyChanged+=(_,e)=>{if(e.Property==WindowStateProperty||e.Property==CanResizeProperty)State();};State();frame.Children.Add(grip);
        }
        Edge(WindowEdge.North,HorizontalAlignment.Stretch,VerticalAlignment.Top,StandardCursorType.SizeNorthSouth);
        Edge(WindowEdge.South,HorizontalAlignment.Stretch,VerticalAlignment.Bottom,StandardCursorType.SizeNorthSouth);
        Edge(WindowEdge.West,HorizontalAlignment.Left,VerticalAlignment.Stretch,StandardCursorType.SizeWestEast);
        Edge(WindowEdge.East,HorizontalAlignment.Right,VerticalAlignment.Stretch,StandardCursorType.SizeWestEast);
        Edge(WindowEdge.NorthWest,HorizontalAlignment.Left,VerticalAlignment.Top,StandardCursorType.TopLeftCorner,true);
        Edge(WindowEdge.NorthEast,HorizontalAlignment.Right,VerticalAlignment.Top,StandardCursorType.TopRightCorner,true);
        Edge(WindowEdge.SouthWest,HorizontalAlignment.Left,VerticalAlignment.Bottom,StandardCursorType.BottomLeftCorner,true);
        Edge(WindowEdge.SouthEast,HorizontalAlignment.Right,VerticalAlignment.Bottom,StandardCursorType.BottomRightCorner,true);
    }
    void ApplyMaterial() {
        bool highContrast=materialPlatform?.GetColorValues().ContrastPreference==ColorContrastPreference.High;
        var policy=new HardwarePulse.MaterialPolicy(settings.AppOpacity,settings.LockPosition,settingsVisible,settings.Solid,highContrast);
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
        int count=Math.Clamp((int)((ClientSize.Width-48)/(270*settings.FontSize/12)),1,3);
        int visibility=0;for(int i=0;i<panels.Length;i++)if(panels[i].IsVisible)visibility|=1<<i;
        if(cardColumns==count&&cardVisibility==visibility)return;
        cardColumns=count;cardVisibility=visibility;
        var visible=panels.Where(x=>x.IsVisible).ToArray();
        if(cards.ColumnDefinitions.Count!=count)cards.ColumnDefinitions=new(string.Join(",",Enumerable.Repeat("*",count)));
        for(int i=0;i<visible.Length;i++){Grid.SetRow(visible[i],i/count);Grid.SetColumn(visible[i],i%count);}
    }
    void ApplyCardDensity() {
        if(measuringDensity||monitorScroll==null||monitorScroll.Viewport.Height<=0)return;
        measuringDensity=true;
        try {
            double width=Math.Max(1,monitorScroll.Viewport.Width-48),cell=Math.Max(1,(width-10*(cardColumns-1))/Math.Max(1,cardColumns));
            for(int level=0;level<=3;level++) {
                foreach(var panel in panels){panel.Margin=new Thickness(0,0,0,level==0?6:3);panel.ApplyDensity(settings.FontSize,level,cell);}
                cards.Measure(new Size(width,double.PositiveInfinity));
                if(cards.DesiredSize.Height<=monitorScroll.Viewport.Height-48)break;
            }
        } finally {measuringDensity=false;}
    }
    public void Present(MonitorSnapshot snapshot) {
        latestSnapshot=snapshot;Render(snapshot);
    }
    void Render(MonitorSnapshot snapshot) {
        bool max=sessionMax;
        foreach(var panel in panels){panel.Present(snapshot,max,details.IsChecked==true);panel.IsVisible&=!settings.HiddenCards.Contains(panel.Key);}
        cardsEmpty.IsVisible=panels.All(x=>!x.IsVisible);
        LayoutCards();
        ApplyCardDensity();
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
