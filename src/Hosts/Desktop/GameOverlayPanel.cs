using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace HardwarePulse.Desktop;

public sealed class GameOverlayOptions {
    public bool Enabled {get;set;}
    public bool Detailed {get;set;}
    public bool Fps {get;set;}=true;
    public bool Cpu {get;set;}=true;
    public bool Gpu {get;set;}=true;
    public bool Memory {get;set;}=true;
    public bool Fans {get;set;}
    public bool Storage {get;set;}
    public string Position {get;set;}="top-left";
    public string Background {get;set;}="#111923";
    public double Opacity {get;set;}=80;
    public static readonly string[] Positions=["top-left","top-center","top-right","bottom-left","bottom-center","bottom-right"];
    public static GameOverlayOptions Read(JsonElement data) {
        GameOverlayOptions value;
        try{value=data.Deserialize<GameOverlayOptions>()??new();}catch(JsonException){return new();}
        if(!Positions.Contains(value.Position))value.Position="top-left";
        if(value.Background==null||value.Background.Length!=7||!PreviewSettings.IsTextColor(value.Background))value.Background="#111923";
        value.Opacity=double.IsFinite(value.Opacity)?Math.Clamp(value.Opacity,0,100):80;
        return value;
    }
    public string Message(MonitorSnapshot? hardware,DesktopFpsSnapshot fps,UiLanguage language) {
        var lines=new List<string>();
        string Value(string id)=>hardware?.WindowsHardware.FirstOrDefault(x=>x.Id==id)?.Value??"—";
        if(Detailed&&fps.TargetName.Length>0)lines.Add(fps.TargetName);
        if(Fps)lines.Add($"FPS {fps.Current} · AVG {fps.Average} · MIN {fps.Minimum} · 1% LOW {fps.Low}"+(fps.Current=="—"?" · "+language.T(fps.Status):""));
        if(Cpu){lines.Add("CPU "+(hardware?.Cpu??"—")+" · "+Value("cpu"));if(Detailed)lines.Add("Vcore "+Value("vcore"));}
        if(Gpu){lines.Add("GPU "+Value("gpuLoad")+" · "+Value("gpu"));if(Detailed)lines.Add("VRAM "+Value("vram")+" · "+Value("gpuVolt"));}
        if(Memory){lines.Add(language.T("Memory")+" "+(hardware?.Memory??"—"));lines.Add(language.T("GPU memory")+" "+Value("vramUsage"));}
        if(Fans)lines.Add(language.T("CPU Fan")+" "+Value("cpuFan")+" · "+language.T("GPU Fan 1")+" "+Value("gpuFan"));
        if(Storage)lines.Add("NVMe "+Value("diskC")+" / "+Value("diskD"));
        return lines.Count==0?"Pulse":string.Join(Detailed?Environment.NewLine:"   |   ",lines);
    }
}

// Reads the foreign target only; input policy is restricted to this owned HWND.
public sealed class GameOverlayWindow : Window {
    readonly TextBlock text=new(){Foreground=Brushes.White,FontSize=14,TextWrapping=TextWrapping.Wrap};
    readonly Border surface=new(){CornerRadius=new CornerRadius(8),Padding=new Thickness(12,8)};
    WindowsWindowInput? input;
    public bool InputReady {get;private set;}
    public GameOverlayWindow() {
        Title="Pulse Game Overlay";WindowDecorations=WindowDecorations.None;CanResize=false;
        ShowInTaskbar=false;ShowActivated=false;Focusable=false;Topmost=true;
        Background=Brushes.Transparent;TransparencyLevelHint=[WindowTransparencyLevel.Transparent];
        SizeToContent=SizeToContent.WidthAndHeight;surface.Child=text;Content=surface;
        Opened+=(_,_)=>{
            try{input??=new WindowsWindowInput(TryGetPlatformHandle()!.Handle);input.SetPassThrough(true);InputReady=true;}
            catch{InputReady=false;Hide();}
        };
        Closed+=(_,_)=>{input=null;InputReady=false;};
    }
    public static PixelPoint Anchor(PixelRect bounds,int width,int height,string position) {
        int x=position.EndsWith("left",StringComparison.Ordinal)?bounds.X+12:position.EndsWith("right",StringComparison.Ordinal)?bounds.Right-width-12:bounds.X+(bounds.Width-width)/2;
        int y=position.StartsWith("bottom",StringComparison.Ordinal)?bounds.Bottom-height-12:bounds.Y+12;
        return new(Math.Max(bounds.X,x),Math.Max(bounds.Y,y));
    }
    public static bool TargetBounds(nint target,out PixelRect bounds) {
        bounds=default;
        if(!OperatingSystem.IsWindows()||target==0||target!=GetForegroundWindow()||IsIconic(target)||!GetClientRect(target,out var rect)||rect.Right<100||rect.Bottom<100)return false;
        var origin=new NativePoint();if(!ClientToScreen(target,ref origin))return false;
        bounds=new(origin.X,origin.Y,rect.Right,rect.Bottom);return true;
    }
    public void Display(nint target,string message,GameOverlayOptions options) {
        if(!TargetBounds(target,out var bounds)){Hide();return;}
        var scale=Screens.ScreenFromPoint(bounds.Position)?.Scaling??1;
        MaxWidth=Math.Max(50,(bounds.Width-24)/scale);MaxHeight=Math.Max(50,(bounds.Height-24)/scale);
        var color=Color.Parse(options.Background);
        surface.Background=new SolidColorBrush(Color.FromArgb((byte)Math.Round(options.Opacity*255/100),color.R,color.G,color.B));
        text.Text=message;
        // Establish the target monitor before measuring DIP content.
        if(!IsVisible){Position=bounds.Position;Show();}
        if(!InputReady){Hide();return;}
        UpdateLayout();Position=Anchor(bounds,(int)Math.Ceiling(Bounds.Width*RenderScaling),(int)Math.Ceiling(Bounds.Height*RenderScaling),options.Position);
    }
    [StructLayout(LayoutKind.Sequential)] struct NativeRect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] struct NativePoint {public int X,Y;}
    [DllImport("user32.dll")] static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] static extern bool GetClientRect(nint window,out NativeRect rect);
    [DllImport("user32.dll")] static extern bool ClientToScreen(nint window,ref NativePoint point);
}

public sealed class GameOverlayPanel : StackPanel,IDisposable {
    readonly UiLanguage language;
    readonly GameOverlayOptions options;
    readonly FpsPanel fps;
    readonly Action changed;
    readonly bool supported;
    MonitorSnapshot? hardware;
    GameOverlayWindow? window;
    bool disposed;
    readonly TextBlock status=new(){Name="GameOverlayStatus",TextWrapping=TextWrapping.Wrap};
    public GameOverlayPanel(UiLanguage language,GameOverlayOptions options,FpsPanel fps,Action changed,bool isolated=false) {
        this.language=language;this.options=options;this.fps=fps;this.changed=changed;
        supported=OperatingSystem.IsWindows()&&!isolated;Name="GameOverlaySettings";Spacing=12;Margin=new Thickness(20);
        void Toggle(string name,string label,bool value,Action<bool> set) {
            var box=language.Set(new CheckBox{Name=name,IsChecked=value,IsEnabled=supported},label);Children.Add(box);
            box.IsCheckedChanged+=(_,_)=>{set(box.IsChecked==true);Apply();};
        }
        Toggle("GameOverlayEnabled","Enable game overlay",options.Enabled,value=>options.Enabled=value);
        Toggle("GameOverlayDetailed","Detailed overlay",options.Detailed,value=>options.Detailed=value);
        Toggle("GameOverlayFps","FPS",options.Fps,value=>options.Fps=value);
        Toggle("GameOverlayCpu","CPU",options.Cpu,value=>options.Cpu=value);
        Toggle("GameOverlayGpu","GPU",options.Gpu,value=>options.Gpu=value);
        Toggle("GameOverlayMemory","Memory",options.Memory,value=>options.Memory=value);
        Toggle("GameOverlayFans","Fans",options.Fans,value=>options.Fans=value);
        Toggle("GameOverlayStorage","Storage",options.Storage,value=>options.Storage=value);
        Children.Add(language.Set(new TextBlock{TextWrapping=TextWrapping.Wrap},"Uses the FPS app selection. Enable FPS separately to capture frame metrics."));
        var position=new ComboBox{Name="GameOverlayPosition",ItemsSource=GameOverlayOptions.Positions,SelectedItem=options.Position,IsEnabled=supported,ItemTemplate=language.Choices()};Children.Add(position);
        position.SelectionChanged+=(_,_)=>{options.Position=position.SelectedItem as string??"top-left";Apply();};
        Children.Add(language.Set(new TextBlock(),"Background color"));
        var background=new TextBox{Name="GameOverlayBackground",Text=options.Background,IsEnabled=supported};Children.Add(background);
        background.TextChanged+=(_,_)=>{if(background.Text is {Length:7} value&&PreviewSettings.IsTextColor(value)){options.Background=value;Apply();}};
        Children.Add(language.Set(new TextBlock(),"Background opacity"));
        var opacity=new Slider{Name="GameOverlayOpacity",Minimum=0,Maximum=100,Value=options.Opacity,IsEnabled=supported};Children.Add(opacity);
        opacity.ValueChanged+=(_,_)=>{options.Opacity=opacity.Value;Apply();};
        var reset=language.Set(new Button{Name="ResetGameOverlayAppearance",IsEnabled=supported},"Reset overlay appearance");Children.Add(reset);
        reset.Click+=(_,_)=>{options.Background="#111923";options.Opacity=80;background.Text=options.Background;opacity.Value=80;Apply();};
        Children.Add(status);fps.ReadingChanged+=OnFps;language.Changed+=Refresh;
        fps.TrackGame=supported&&options.Enabled;Refresh();
    }
    void Apply(){if(disposed)return;fps.TrackGame=supported&&options.Enabled;changed();Refresh();}
    void OnFps(DesktopFpsSnapshot snapshot)=>Refresh();
    public void Present(MonitorSnapshot snapshot){hardware=snapshot;Refresh();}
    void Refresh() {
        if(disposed)return;
        if(!supported||!options.Enabled){window?.Close();window=null;language.Set(status,!supported?"Game overlay is unavailable in this session.":"Game overlay is off.");return;}
        if(!GameOverlayWindow.TargetBounds(fps.Current.TargetWindow,out _)){window?.Hide();language.Set(status,"Waiting for foreground target app");return;}
        window??=new GameOverlayWindow();
        window.FontFamily=DesktopFonts.ForLanguage(language.EffectiveLanguage)??FontFamily.Default;
        window.Display(fps.Current.TargetWindow,options.Message(hardware,fps.Current,language),options);
        language.Set(status,window.InputReady?"Game overlay active":"Game overlay input unavailable");
    }
    public void Dispose(){if(disposed)return;disposed=true;fps.ReadingChanged-=OnFps;language.Changed-=Refresh;fps.TrackGame=false;window?.Close();window=null;}
}
