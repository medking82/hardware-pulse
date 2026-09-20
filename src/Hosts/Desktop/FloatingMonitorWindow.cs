using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Platform;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace HardwarePulse.Desktop;

// Presentation only. The owning Monitor supplies snapshots and controls lifetime.
public sealed class FloatingMonitorWindow : Window {
    readonly UiLanguage language;
    readonly Border surface=new(){Name="DesktopSurface",CornerRadius=new CornerRadius(16)};
    readonly IPlatformSettings? materialPlatform=Application.Current?.PlatformSettings;
    double backgroundOpacity=86,overlayOpacity=55,textOpacity=100;
    string textColor="#F5F7FA";
    bool appIconColors=true,appLight;
    ReadingPalette palette=new();
    PreviewSettings contrastSettings=new();
    readonly WindowsLocalContrast? contrast;
    WindowsDesktopLayer? desktopLayer;
    string automaticText="#F5F7FA";
    long lastBackgroundSample;
    string EffectiveText=>contrastSettings.DesktopAutoContrast?(Topmost?"#101820":IsLocked?automaticText:"#F5F7FA"):textColor;
    static double Luminance(Color color){static double Linear(byte value){double x=value/255d;return x<=.04045?x/12.92:Math.Pow((x+.055)/1.055,2.4);}return .2126*Linear(color.R)+.7152*Linear(color.G)+.0722*Linear(color.B);}
    public event Action? ContrastChanged;
    public bool CanBeginScreenshot=>contrast!=null&&contrastSettings.DesktopLocalContrast&&IsVisible;
    public Color ContrastBackground=>(surface.Background as ISolidColorBrush)?.Color??Colors.Transparent;
    public string ContrastStatus=>contrast==null?"Local Contrast is unavailable in this session.":contrast.ScreenshotActive?"Screenshot mode · 15 seconds":!contrastSettings.DesktopLocalContrast?"Local Contrast is off.":contrast.Available?"Local contrast active":"Local contrast unavailable; using standard text color";
    public void BeginScreenshot()=>contrast?.BeginScreenshot();
    public void UpdateLocalContrast()=>contrast?.Update();
    readonly DockPanel rows=new(){Margin=new Thickness(16)};
    readonly Dictionary<string,(Grid Row,TextBlock Label,TextBlock Value)> readings=new();
    MonitorSnapshot? snapshot;
    bool peaks;
    string[] metricOrder=PreviewSettings.DesktopKeys;
    Dictionary<string,bool> metricVisibility=new();
    readonly TextBlock empty=new(){Name="DesktopEmpty",IsVisible=false,TextWrapping=TextWrapping.Wrap};
    readonly Grid sensors=new(){ColumnSpacing=ColumnLayout.Gap,RowSpacing=14};
    readonly CheckBox topmost;
    readonly StackPanel editor=new(){Name="DesktopEditor",Spacing=8};
    public event Action? ReturnRequested;
    public event Action<bool>? LockedChanged;
    int requestedColumns,layoutColumns;
    public event Action<bool>? TopmostChanged;
    readonly TextBlock lockStatus=new(){TextWrapping=TextWrapping.Wrap,IsVisible=false};
    Action<bool>? input;
    IDisposable? inputLifetime;
    public bool IsLocked {get;private set;}
    public bool CanLock=>input!=null;
    public FloatingMonitorWindow(UiLanguage language,bool nativeEffectsAllowed=true) {
        this.language=language;
        RequestedThemeVariant=ThemeVariant.Dark;
        Width=466;Height=400;MinWidth=280;MinHeight=140;FontSize=16;
        WindowDecorations=WindowDecorations.None;
        language.Set(this,"Floating monitor");
        topmost=language.Set(new CheckBox{Name="FloatingTopmost"},"Always on top");
        topmost.IsCheckedChanged+=(_,_)=>{Topmost=topmost.IsChecked==true;ApplyMaterial();RefreshDesktopLayer();TopmostChanged?.Invoke(Topmost);};
        var lockButton=language.Set(new Button{Name="LockFloatingMonitor",IsVisible=false},"Done");
        lockButton.Click+=(_,_)=>SetLocked(true);
        var back=language.Set(new Button{Name="ReturnToApp"},"Return to App");
        back.Click+=(_,_)=>ReturnRequested?.Invoke();
        var actions=new WrapPanel();lockButton.Margin=new Thickness(0,0,8,0);actions.Children.Add(lockButton);actions.Children.Add(back);
        editor.Children.Add(language.Set(new TextBlock{Name="DesktopMoveHint",FontSize=12,TextWrapping=TextWrapping.Wrap},"Drag the center to move. Drag any edge or corner to resize."));
        editor.Children.Add(language.Set(empty,"No metrics shown. Choose metrics in Settings."));
        editor.Children.Add(topmost);editor.Children.Add(actions);editor.Children.Add(lockStatus);
        language.Set(lockStatus,"Reopen from Monitor or the tray to unlock.");
        DockPanel.SetDock(editor,Dock.Top);editor.Margin=new Thickness(0,0,0,12);rows.Children.Add(editor);
        Opened+=(_,_)=>{
            var handle=TryGetPlatformHandle();
            if(OperatingSystem.IsWindows()&&handle?.HandleDescriptor=="HWND") {
                input??=new WindowsWindowInput(handle.Handle).SetPassThrough;
                if(nativeEffectsAllowed)desktopLayer??=new WindowsDesktopLayer(handle.Handle,action=>Avalonia.Threading.Dispatcher.UIThread.Post(action),RefreshDesktopLayer);
            }
            else if(OperatingSystem.IsMacOS()&&handle?.HandleDescriptor=="NSWindow"&&input==null) {
                var adapter=new MacWindowInput(handle.Handle);input=adapter.SetPassThrough;inputLifetime=adapter;
            }
            else if(OperatingSystem.IsLinux()&&input==null) {
                var adapter=X11WindowInput.TryCreate(this);
                if(adapter!=null){input=adapter.SetPassThrough;inputLifetime=adapter;}
            }
            lockButton.IsVisible=lockStatus.IsVisible=input!=null;
        };

        rows.Children.Add(new ScrollViewer{Content=sensors,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled});
        SizeChanged+=(_,_)=>LayoutReadings();
        surface.Child=rows;
        var frame=new Grid();frame.Children.Add(surface);WindowChrome.AddResizeEdges(this,frame);Content=frame;
        surface.AddHandler(InputElement.PointerPressedEvent,(_,e)=>{
            if(IsLocked||!e.GetCurrentPoint(surface).Properties.IsLeftButtonPressed)return;
            for(var node=e.Source as Visual;node!=null&&node!=surface;node=node.GetVisualParent())
                if(node is Button or Avalonia.Controls.Primitives.ScrollBar or Avalonia.Controls.Primitives.Thumb)return;
            BeginMoveDrag(e);e.Handled=true;
        },Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Background=Brushes.Transparent;TransparencyLevelHint=[WindowTransparencyLevel.Transparent];
        void ColorsChanged(object? sender,PlatformColorValues colors)=>ApplyMaterial();
        if(materialPlatform!=null)materialPlatform.ColorValuesChanged+=ColorsChanged;
        Opened+=(_,_)=>ApplyMaterial();
        PropertyChanged+=(_,e)=>{if(e.Property==ActualTransparencyLevelProperty)ApplyMaterial();};
        Closed+=(_,_)=>{if(materialPlatform!=null)materialPlatform.ColorValuesChanged-=ColorsChanged;};
        ApplyMaterial();
        language.Changed+=Localize;Localize();
        PropertyChanged+=(_,e)=>{if(e.Property==ActualThemeVariantProperty)foreach(var item in readings.Values)ColorIcon(item.Row);};
        if(nativeEffectsAllowed&&WindowsBackgroundCapture.Supported) {
            contrast=new WindowsLocalContrast(this,sensors,()=>contrastSettings,ResetContrast);
            contrast.Changed+=()=>ContrastChanged?.Invoke();
            Opened+=(_,_)=>UpdateLocalContrast();
            PropertyChanged+=(_,e)=>{if(e.Property==IsVisibleProperty)UpdateLocalContrast();};
        }
        Closed+=(_,_)=>{contrast?.Dispose();desktopLayer?.Dispose();desktopLayer=null;};
        Closed+=(_,_)=>{language.Changed-=Localize;input=null;inputLifetime?.Dispose();inputLifetime=null;};
    }
    public void RestoreGeometry(PreviewSettings settings) {
        Width=settings.DesktopWidth;Height=settings.DesktopHeight;
        var position=settings.DesktopX is int x&&settings.DesktopY is int y?new PixelPoint(x,y):WindowGeometry.LegacyPosition(this,settings.LegacyDesktopLeft,settings.LegacyDesktopTop);
        if(position is PixelPoint saved)Position=saved;
        KeepOnScreen(position==null);
    }
    public void KeepOnScreen(bool reset=false) {
        var screen=reset?Screens.Primary:Screens.ScreenFromWindow(this)??Screens.Primary;
        if(screen==null)return;
        var area=screen.WorkingArea;double scale=screen.Scaling;
        Width=Math.Max(MinWidth,Math.Min(Width,area.Width/scale-32));
        Height=Math.Max(MinHeight,Math.Min(Height,area.Height/scale-32));
        var position=reset?new PixelPoint(area.X+(int)(40*scale),area.Y+(int)(100*scale)):Position;
        Position=new PixelPoint(Math.Clamp(position.X,area.X,Math.Max(area.X,area.Right-(int)Math.Ceiling(Width*scale))),
            Math.Clamp(position.Y,area.Y,Math.Max(area.Y,area.Bottom-(int)Math.Ceiling(Height*scale))));
    }
    public void ApplyPreferences(PreviewSettings settings,bool fitColumns=false) {
        contrastSettings=settings;
        textColor=settings.DesktopColor;appIconColors=settings.DesktopAppIconColors;
        palette=new(settings.UnifiedReadingColors,settings.ReadingColor);
        metricOrder=settings.DesktopOrder.ToArray();metricVisibility=new(settings.DesktopVisible);
        FontSize=settings.DesktopFontSize;sensors.RowSpacing=settings.DesktopSpacing;
        backgroundOpacity=settings.DesktopBackgroundOpacity;overlayOpacity=settings.DesktopOverlayOpacity;textOpacity=settings.DesktopTextOpacity;
        requestedColumns=settings.DesktopColumns;topmost.IsChecked=settings.DesktopTopmost;
        ApplyMaterial();
        if(fitColumns&&requestedColumns>0) {
            double wanted=Math.Max(280,24*FontSize)*requestedColumns+ColumnLayout.Gap*(requestedColumns-1)+34;
            var screen=Screens.ScreenFromWindow(this)??Screens.Primary;
            double scale=screen?.Scaling??RenderScaling;
            Width=screen==null?wanted:Math.Min(wanted,Math.Max(MinWidth,screen.WorkingArea.Width/scale-32));
            if(screen!=null) {
                int right=screen.WorkingArea.Right-(int)Math.Ceiling(Width*scale);
                Position=new PixelPoint(Math.Clamp(Position.X,screen.WorkingArea.X,Math.Max(screen.WorkingArea.X,right)),Position.Y);
            }
        }
        if(snapshot!=null)Present(snapshot,peaks);else LayoutReadings();
    }
    void ApplyMaterial() {
        bool highContrast=materialPlatform?.GetColorValues().ContrastPreference==ColorContrastPreference.High;
        bool supported=ActualTransparencyLevel!=WindowTransparencyLevel.None;
        double alpha=highContrast||!supported?100:Topmost?overlayOpacity:backgroundOpacity;
        // DesktopView.SetTextOpacity uses a dark backing for its original light text.
        // Keep controls readable when the user deliberately fades the metric layer.
        var backing=highContrast||Luminance(Color.Parse(EffectiveText))>.4?Color.FromRgb(20,29,38):Color.FromRgb(245,247,250);
        surface.Background=new SolidColorBrush(Color.FromArgb((byte)Math.Round(255*Math.Clamp(alpha,0,100)/100),backing.R,backing.G,backing.B));
        bool lightBacking=backing.R>200;
        RequestedThemeVariant=lightBacking?ThemeVariant.Light:ThemeVariant.Dark;
        Foreground=Brush.Parse(lightBacking?"#17202B":"#F5F7FA");sensors.Opacity=highContrast?1:Math.Clamp(textOpacity/100,0,1);
        foreach(var item in readings.Values)ColorRow(item.Row);
        contrast?.Update();
    }
    void LayoutReadings() {
        var layout=new ColumnLayout(Math.Max(1,Bounds.Width-32),Math.Max(280,24*FontSize),requestedColumns,layoutColumns);
        if(layout.Columns!=layoutColumns){layoutColumns=layout.Columns;sensors.ColumnDefinitions=new(string.Join(",",Enumerable.Repeat("*",layoutColumns)));}
        var visible=sensors.Children.OfType<Grid>().Where(row=>row.IsVisible).ToArray();
        // Hidden rows remain reusable but must not retain indices into removed definitions.
        foreach(var row in sensors.Children.OfType<Grid>().Where(row=>!row.IsVisible)){Grid.SetRow(row,0);Grid.SetColumn(row,0);}
        int count=(visible.Length+layoutColumns-1)/layoutColumns;
        while(sensors.RowDefinitions.Count<count)sensors.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        while(sensors.RowDefinitions.Count>count)sensors.RowDefinitions.RemoveAt(sensors.RowDefinitions.Count-1);
        for(int i=0;i<visible.Length;i++) {
            var row=visible[i];Grid.SetRow(row,i/layoutColumns);Grid.SetColumn(row,i%layoutColumns);
            var icon=(Viewbox)row.Children[0];icon.Width=icon.Height=FontSize*1.2;
        }
    }
    public bool SetLocked(bool locked) {
        if(input==null)return !locked;
        if(IsLocked==locked)return true;
        try {
            // Avalonia rewrites extended styles when changing chrome. Configure it
            // before enabling native pass-through; remove pass-through before restore.
            if(locked)SetEditorChrome(false);
            input(locked);IsLocked=locked;
            if(!locked)SetEditorChrome(true);
            editor.IsVisible=!locked;
            language.Set(lockStatus,locked?"Locked · Reopen from Monitor or the tray to unlock.":"Reopen from Monitor or the tray to unlock.");
            LockedChanged?.Invoke(locked);
            ApplyMaterial();
            RefreshDesktopLayer();
            return true;
        } catch(Exception e) when(e is System.ComponentModel.Win32Exception or InvalidOperationException) {
            if(!IsLocked)SetEditorChrome(true);
            language.Set(lockStatus,"Could not change window lock. Reopen the floating monitor and try again.");return false;
        }
    }
    void SetEditorChrome(bool editing) {
        CanResize=editing;ShowInTaskbar=editing;
        WindowDecorations=WindowDecorations.None;
    }
    void Localize() {
        if(snapshot!=null)Present(snapshot,peaks);

        var family=DesktopFonts.ForLanguage(language.EffectiveLanguage);
        if(family is null)ClearValue(FontFamilyProperty);else FontFamily=family;
    }
    void ColorIcon(Grid row) {
        var icon=(Avalonia.Controls.Shapes.Path)((Viewbox)row.Children[0]).Child!;
        bool highContrast=materialPlatform?.GetColorValues().ContrastPreference==ColorContrastPreference.High;
        var brush=Brush.Parse(highContrast?"#F5F7FA":appIconColors?palette.ForIcon(row.Tag as string??"",appLight,EffectiveText):EffectiveText);
        if(icon.Stroke!=null)icon.Stroke=brush;if(icon.Fill!=null)icon.Fill=brush;
    }
    void ColorRow(Grid row) {
        bool highContrast=materialPlatform?.GetColorValues().ContrastPreference==ColorContrastPreference.High;
        var brush=Brush.Parse(highContrast?"#F5F7FA":EffectiveText);
        foreach(var text in row.Children.OfType<TextBlock>())text.Foreground=brush;
        ColorIcon(row);
    }
    void ResetContrast() {
        foreach(var item in readings.Values) {
            foreach(var control in item.Row.GetVisualDescendants().OfType<Control>())if(control is TextBlock or Avalonia.Controls.Shapes.Path)control.Effect=null;
            ColorRow(item.Row);
        }
    }
    public void ApplyAppPalette(ReadingPalette value,bool light){palette=value;appLight=light;foreach(var item in readings.Values)ColorIcon(item.Row);}
    public void Present(MonitorSnapshot snapshot,bool peaks=false) {
        this.snapshot=snapshot;this.peaks=peaks;
        var metrics=DesktopReadings.Create(snapshot,peaks,language,contrastSettings.Names).OrderBy(metric=>DesktopQuotaPreferences.Order(metricOrder,metric.Key)).ToArray();
        var active=metrics.Select(x=>x.Key).ToHashSet();
        foreach(string key in readings.Keys.Where(key=>!active.Contains(key)).ToArray()){sensors.Children.Remove(readings[key].Row);readings.Remove(key);}
        for(int i=0;i<metrics.Length;i++) {
            var metric=metrics[i];
            if(!readings.TryGetValue(metric.Key,out var item)) {
                var row=new Grid{Name="DesktopMetric"+metric.Key,Tag=metric.Icon,ColumnDefinitions=new("Auto,*,Auto"),ColumnSpacing=8};
                var label=new TextBlock{VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap};
                var value=new TextBlock{VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Right};
                var icon=AppIcon.Create(metric.Icon);
                icon.Tag=metric.Icon;
                row.Children.Add(new Viewbox{Width=18,Height=18,Child=icon,VerticalAlignment=VerticalAlignment.Center});
                Grid.SetColumn(label,1);row.Children.Add(label);Grid.SetColumn(value,2);row.Children.Add(value);
                ColorRow(row);
                row.SizeChanged+=(_,_)=>value.MaxWidth=Math.Max(1,(row.Bounds.Width-34)*.65);
                item=(row,label,value);readings.Add(metric.Key,item);sensors.Children.Add(row);
            }
            item.Row.IsVisible=metric.Key=="status"||DesktopQuotaPreferences.Visible(metricVisibility,metric.Key);item.Label.Text=metric.Title;item.Value.Text=metric.Value;
            ToolTip.SetTip(item.Label,metric.Title);ToolTip.SetTip(item.Value,metric.Value);
            int old=sensors.Children.IndexOf(item.Row);if(old!=i){sensors.Children.RemoveAt(old);sensors.Children.Insert(i,item.Row);}
        }
        empty.IsVisible=!readings.Values.Any(item=>item.Row.IsVisible);
        LayoutReadings();
        RefreshDesktopLayer();
    }
    void RefreshDesktopLayer(){
        if(!IsLocked||desktopLayer==null)return;
        if(desktopLayer.Refresh(Topmost)){if(!IsVisible){Show();desktopLayer.Refresh(Topmost);}}
        else if(IsVisible)Hide(); // the existing snapshot/foreground refresh retries after Explorer returns
        if(contrastSettings.DesktopAutoContrast&&!Topmost&&contrast?.ScreenshotActive!=true) {
            long now=System.Diagnostics.Stopwatch.GetTimestamp();
            if(now-lastBackgroundSample<System.Diagnostics.Stopwatch.Frequency*2)return;
            lastBackgroundSample=now;
            double? luminance=desktopLayer.SampleBackground();
            string next=luminance==null?"#F5F7FA":ContrastAnalysis.Select(luminance.Value,automaticText=="#152127"?(byte)20:(byte)245)==20?"#152127":"#F5F7FA";
            if(next!=automaticText){automaticText=next;ApplyMaterial();}
        }
    }
}
