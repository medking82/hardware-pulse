using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace HardwarePulse.Desktop;

// Presentation only. The owning Monitor supplies snapshots and controls lifetime.
public sealed class FloatingMonitorWindow : Window {
    readonly UiLanguage language;
    readonly PreviewSettings appearance;
    readonly StackPanel rows=new(){Spacing=8,Margin=new Thickness(16)};
    readonly AdaptiveReadingsPanel readings=new(){Name="DesktopReadings",Spacing=8};
    readonly Dictionary<string,Control> readingControls=new();
    DesktopFpsSnapshot? fpsSnapshot;
    MonitorSnapshot? lastSnapshot;
    bool lastPeaks;
    readonly Dictionary<string,QuotaReading> quotaReadings=new();
    readonly TextBlock lockStatus=new(){TextWrapping=TextWrapping.Wrap,IsVisible=false};
    readonly StackPanel toolbar=new(){Spacing=8};
    readonly ScrollViewer scroll=new(){HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled};
    bool fitQueued;
    Action<bool>? input;
    WindowDecorations windowsEditingDecorations;
    IDisposable? inputLifetime;
    WindowsDesktopLayer? desktopLayer;
    WindowsLocalContrast? localContrast;
    public string ContrastStatus=>localContrast?.ScreenshotActive==true?"Screenshot mode: visible to capture for 15 seconds.":!appearance.FloatingLocalContrast?"Local Contrast is off.":localContrast?.Available==true?"Local contrast active":"Local contrast unavailable; using standard text color";
    public void UpdateLocalContrast(){localContrast?.Update();MaterialChanged?.Invoke();}
    public void BeginScreenshot(){localContrast?.BeginScreenshot();}
    public bool DesktopLayerAvailable=>desktopLayer?.Attached==true;
    double backgroundOpacity=100;
    public double BackgroundOpacity=>backgroundOpacity;
    public bool BackgroundBlur {get;private set;}
    public event Action? MaterialChanged;
    public string MaterialStatus=>!BackgroundBlur?"Background blur is off.":ActualTransparencyLevel==WindowTransparencyLevel.Blur||ActualTransparencyLevel==WindowTransparencyLevel.AcrylicBlur?
        (backgroundOpacity>=100?"Blur is active but hidden by the opaque background.":"Background blur is active."):"Background blur is unavailable. Using the supported background instead.";
    public bool IsLocked {get;private set;}
    public bool CanLock=>input!=null;
    public FloatingMonitorWindow(UiLanguage language,PreviewSettings? saved=null,Action? changed=null) {
        this.language=language;
        saved??=new PreviewSettings();
        appearance=saved;
        Width=saved.FloatingWidth;Height=saved.FloatingHeight;MinWidth=360;MinHeight=240;FontSize=saved.FloatingFontSize;
        Topmost=saved.FloatingTopmost;
        SetBackgroundBlur(saved.FloatingBackgroundBlur);
        PropertyChanged+=(_,e)=>{if(e.Property==ActualTransparencyLevelProperty||e.Property==ActualThemeVariantProperty){ApplyBackground();ApplyTextAppearance();}};
        SetBackgroundOpacity(saved.FloatingBackgroundOpacity);
        bool restored=false;
        void Remember(){
            if(!restored||WindowState!=WindowState.Normal)return;
            saved.FloatingWidth=Width;saved.FloatingHeight=Height;
            saved.FloatingX=Position.X;saved.FloatingY=Position.Y;saved.FloatingPositionSet=true;
            saved.FloatingTopmost=Topmost;changed?.Invoke();
        }
        SizeChanged+=(_,_)=>Remember();PositionChanged+=(_,_)=>Remember();
        language.Set(this,"Floating monitor");
        var topmost=language.Set(new CheckBox{Name="FloatingTopmost"},"Always on top");
        topmost.IsChecked=Topmost;
        topmost.IsCheckedChanged+=(_,_)=>{Topmost=topmost.IsChecked==true;Remember();};
        PropertyChanged+=(_,e)=>{if(e.Property==TopmostProperty){topmost.IsChecked=Topmost;Remember();desktopLayer?.Refresh();}};
        var lockButton=language.Set(new Button{Name="LockFloatingMonitor",IsVisible=false},"Lock floating monitor");
        lockButton.Click+=(_,_)=>SetLocked(true);
        toolbar.Name="FloatingEditControls";toolbar.Children.Add(topmost);toolbar.Children.Add(lockButton);toolbar.Children.Add(lockStatus);
        language.Set(lockStatus,"Reopen from Monitor or the tray to unlock.");
        rows.Children.Add(toolbar);
        Opened+=(_,_)=>{
            var requested=saved.FloatingPositionSet?new PixelPoint(saved.FloatingX,saved.FloatingY):Position;
            var screen=Screens.ScreenFromPoint(requested)??Screens.Primary;
            if(screen!=null){
                var area=screen.WorkingArea;
                Width=Math.Max(MinWidth,Math.Min(Width,area.Width/screen.Scaling));
                Height=Math.Max(MinHeight,Math.Min(Height,area.Height/screen.Scaling));
                Position=ConstrainPosition(requested,area,(int)Math.Ceiling(Width*screen.Scaling),(int)Math.Ceiling(Height*screen.Scaling));
            }
            restored=true;Remember();
            var handle=TryGetPlatformHandle();
            if(OperatingSystem.IsWindows()&&handle?.HandleDescriptor=="HWND") {
                input??=new WindowsWindowInput(handle.Handle).SetPassThrough;
                desktopLayer??=new WindowsDesktopLayer(this,()=>IsLocked);
                if(localContrast==null){localContrast=new WindowsLocalContrast(this,readings,appearance,ApplyTextAppearance);localContrast.Changed+=()=>MaterialChanged?.Invoke();}
                localContrast.Update();
            }
            else if(OperatingSystem.IsMacOS()&&handle?.HandleDescriptor=="NSWindow"&&input==null) {
                var adapter=new MacWindowInput(handle.Handle);input=adapter.SetPassThrough;inputLifetime=adapter;
            }
            else if(OperatingSystem.IsLinux()&&input==null) {
                var adapter=X11WindowInput.TryCreate(this);
                if(adapter!=null){input=adapter.SetPassThrough;inputLifetime=adapter;}
            }
            lockButton.IsVisible=lockStatus.IsVisible=input!=null;
            QueueLockedFit();
            desktopLayer?.Refresh();
        };
        rows.Children.Add(readings);
        ApplyTextAppearance();
        scroll.Content=rows;Content=scroll;
        scroll.PropertyChanged+=(_,e)=>{if(e.Property==ScrollViewer.ExtentProperty||e.Property==ScrollViewer.ViewportProperty)QueueLockedFit();};
        language.Changed+=Localize;Localize();
        PropertyChanged+=(_,e)=>{if(e.Property==IsVisibleProperty)localContrast?.Update();};
        Closed+=(_,_)=>{language.Changed-=Localize;localContrast?.Dispose();localContrast=null;desktopLayer?.Dispose();desktopLayer=null;input=null;inputLifetime?.Dispose();inputLifetime=null;};
    }
    public static PixelPoint ConstrainPosition(PixelPoint requested,PixelRect area,int width,int height)=>new(
        Math.Clamp(requested.X,area.X,Math.Max(area.X,area.Right-width)),
        Math.Clamp(requested.Y,area.Y,Math.Max(area.Y,area.Bottom-height)));
    public void SetBackgroundOpacity(double value) {
        backgroundOpacity=double.IsFinite(value)?Math.Clamp(value,0,100):100;ApplyBackground();
    }
    public void SetBackgroundBlur(bool enabled) {
        BackgroundBlur=enabled;
        // Avalonia.Native maps AcrylicBlur to AppKit's behind-window NSVisualEffectView.
        // Its macOS backend does not map the plain Blur value.
        TransparencyLevelHint=enabled?[WindowTransparencyLevel.Blur,WindowTransparencyLevel.AcrylicBlur,WindowTransparencyLevel.Transparent,WindowTransparencyLevel.None]:[WindowTransparencyLevel.Transparent,WindowTransparencyLevel.None];
        MaterialChanged?.Invoke();
    }
    void ApplyBackground() {
        var color=ActualThemeVariant==ThemeVariant.Dark?Color.Parse("#202830"):Color.Parse("#F4F6F8");
        byte alpha=ActualTransparencyLevel==WindowTransparencyLevel.None?(byte)255:(byte)Math.Round(backgroundOpacity*2.55);
        Background=new SolidColorBrush(Color.FromArgb(alpha,color.R,color.G,color.B));
        TransparencyBackgroundFallback=new SolidColorBrush(color);
        MaterialChanged?.Invoke();
    }
    void SetWindowsDecorations(WindowDecorations decorations) {
        // Win32 frame changes can resize the client area. Preserve the user's
        // reading area rather than storing that temporary resize as new geometry.
        double width=Width,height=Height;
        WindowDecorations=decorations;Width=width;Height=height;
    }
    public bool SetLocked(bool locked) {
        if(input==null)return !locked;
        if(IsLocked==locked)return true;
        var previousDecorations=WindowDecorations;
        try {
            if(OperatingSystem.IsWindows()&&locked)SetWindowsDecorations(WindowDecorations.None);
            input(locked);
            if(OperatingSystem.IsWindows()) {
                if(locked)windowsEditingDecorations=previousDecorations;
                else SetWindowsDecorations(windowsEditingDecorations);
            }
            IsLocked=locked;
            desktopLayer?.Refresh();
            toolbar.IsVisible=!locked;
            language.Set(lockStatus,locked?"Locked · Reopen from Monitor or the tray to unlock.":"Reopen from Monitor or the tray to unlock.");
            QueueLockedFit();
            return true;
        } catch(Exception e) when(e is System.ComponentModel.Win32Exception or InvalidOperationException) {
            if(OperatingSystem.IsWindows())SetWindowsDecorations(previousDecorations);
            language.Set(lockStatus,"Could not change window lock. Reopen the floating monitor and try again.");return false;
        }
    }
    public void PresentFps(DesktopFpsSnapshot snapshot){fpsSnapshot=snapshot;RenderReadings();}
    void QueueLockedFit() {
        if(fitQueued||!IsLocked||!IsVisible)return;
        fitQueued=true;
        Avalonia.Threading.Dispatcher.UIThread.Post(()=>{fitQueued=false;FitLockedHeight();},Avalonia.Threading.DispatcherPriority.Background);
    }
    void FitLockedHeight() {
        if(!IsLocked||!IsVisible||WindowState!=WindowState.Normal||scroll.Viewport.Height<=0)return;
        double overflow=scroll.Extent.Height-scroll.Viewport.Height;
        if(overflow<=1)return;
        var screen=Screens.ScreenFromWindow(this);if(screen==null)return;
        double chrome=Math.Max(0,(FrameSize?.Height??ClientSize.Height)-ClientSize.Height);
        double maximum=Math.Max(MinHeight,screen.WorkingArea.Height/screen.Scaling-chrome-16);
        if(Height+overflow>maximum&&appearance.DesktopColumns==0&&readings.Columns<3) {
            double side=Math.Max(0,(FrameSize?.Width??ClientSize.Width)-ClientSize.Width);
            double maxWidth=Math.Max(MinWidth,screen.WorkingArea.Width/screen.Scaling-side-16);
            double wider=Math.Min(maxWidth,(readings.Columns+1)*readings.MinimumColumnWidth+readings.Columns*ColumnLayout.Gap+64);
            if(wider>Width+.5) {
                Width=wider;
                Position=ConstrainPosition(Position,screen.WorkingArea,(int)Math.Ceiling((wider+side)*screen.Scaling),(int)Math.Ceiling((FrameSize?.Height??Height)*screen.Scaling));
                return; // The acknowledged viewport width schedules the next fit.
            }
        }
        double next=Math.Min(maximum,Math.Ceiling(Height+overflow+1));
        if(next<=Height+.5)return;
        Height=next;
        Position=ConstrainPosition(Position,screen.WorkingArea,(int)Math.Ceiling((FrameSize?.Width??Width)*screen.Scaling),(int)Math.Ceiling((next+chrome)*screen.Scaling));
    }
    public void ApplyTextAppearance() {
        rows.Spacing=readings.Spacing=appearance.FloatingRowSpacing;
        readings.RequestedColumns=appearance.DesktopColumns;
        readings.MinimumColumnWidth=Math.Max(280,24*FontSize);
        foreach(var row in readings.Children){if(row is DesktopFpsRow fps)fps.SetReadingSize(FontSize);StyleReading(row);}
    }
    public void ApplyReadingLayout()=>appearance.DesktopRows.Apply(readings,readingControls);
    void StyleReading(Control control) {
        control.Effect=null;
        Color color=appearance.FloatingTextColor.Length>0?Color.Parse(appearance.FloatingTextColor):
            ActualThemeVariant==ThemeVariant.Dark?Color.Parse("#F4F6F8"):Color.Parse("#202830");
        var brush=new SolidColorBrush(Color.FromArgb((byte)Math.Round(appearance.FloatingTextOpacity*2.55),color.R,color.G,color.B));
        if(control is TextBlock text)text.Foreground=brush;
        if(control is Avalonia.Controls.Shapes.Path icon) {
            var iconBrush=appearance.FloatingIconsFollowApp?AppIcon.Brush((string)icon.Tag!):brush;
            if(icon.Stroke!=null)icon.Stroke=iconBrush;else icon.Fill=iconBrush;
        }
        if(control is Panel panel)foreach(var child in panel.Children)StyleReading(child);
        if(control is Viewbox box&&box.Child!=null)StyleReading(box.Child);
    }
    Grid Row(string label,TextBlock value,string? icon=null) {
        var grid=new Grid{ColumnDefinitions=new("*,Auto"),ColumnSpacing=12};
        grid.Children.Add(new TextBlock{Text=label,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(icon==null?0:32,0,0,0)});
        value.Text="—";value.TextWrapping=TextWrapping.Wrap;value.MaxWidth=180;value.TextAlignment=TextAlignment.Right;value.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(value,1);grid.Children.Add(value);
        if(icon!=null){var artwork=AppIcon.Create(icon);artwork.HorizontalAlignment=HorizontalAlignment.Left;artwork.VerticalAlignment=VerticalAlignment.Center;artwork.IsHitTestVisible=false;grid.Children.Add(artwork);}
        StyleReading(grid);return grid;
    }
    void Localize() {
        var family=DesktopFonts.ForLanguage(language.EffectiveLanguage);
        if(family is null)ClearValue(FontFamilyProperty);else FontFamily=family;
        RenderReadings();
    }
    public void PresentQuota(QuotaReading? reading,string provider="Codex") {
        if(!QuotaSession.Providers.Contains(provider))throw new ArgumentException("Unsupported quota provider",nameof(provider));
        if(reading==null)quotaReadings.Remove(provider);else quotaReadings[provider]=reading;
        RenderReadings();
    }
    public void Present(MonitorSnapshot snapshot,bool peaks=false){lastSnapshot=snapshot;lastPeaks=peaks;RenderReadings();desktopLayer?.Refresh();if(appearance.FloatingLocalContrast)localContrast?.Update();}
    void RenderReadings() {
        var current=DesktopRows.Capture(lastSnapshot,lastPeaks,language,quotaReadings,fpsSnapshot);
        var ids=current.Select(x=>x.Id).ToHashSet();
        foreach(string old in readingControls.Keys.Where(x=>!ids.Contains(x)).ToArray()) {
            readings.Children.Remove(readingControls[old]);readingControls.Remove(old);
        }
        foreach(var item in current) {
            if(!readingControls.TryGetValue(item.Id,out var control)) {
                control=item.Id=="FPS"?new DesktopFpsRow():Row(item.Label,new TextBlock(),item.Icon);control.Tag=item.Id;readingControls.Add(item.Id,control);
                StyleReading(control);
            }
            if(control is DesktopFpsRow fps){fps.SetReadingSize(FontSize);fps.Present(fpsSnapshot!,language);continue;}
            var row=(Grid)control;((TextBlock)row.Children[0]).Text=item.Label;((TextBlock)row.Children[1]).Text=item.Value;
            Avalonia.Automation.AutomationProperties.SetName(row,item.EditorLabel);ToolTip.SetTip(row,item.EditorLabel);
        }
        ApplyReadingLayout();
    }
}
