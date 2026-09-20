using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace HardwarePulse.Desktop;

// UI owns cadence, regions and presentation; the adapter owns capture and Core owns analysis.
public sealed class WindowsLocalContrast : IDisposable {
    readonly FloatingMonitorWindow window;
    readonly Control content;
    readonly Func<PreviewSettings> preferences;
    readonly Action reset;
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(100)};
    readonly DispatcherTimer screenshot=new(){Interval=TimeSpan.FromSeconds(15)};
    WindowsBackgroundCapture? capture;
    ContrastAnalysis? analysis;
    bool busy,disposed;
    int generation;
    PixelPoint? previousPosition;
    public bool Available {get;private set;}
    public bool ScreenshotActive {get;private set;}
    public event Action? Changed;
    readonly record struct Region(Control Control,Rect Bounds,byte Previous);
    readonly record struct Shade(byte Value,double Minority);
    public WindowsLocalContrast(FloatingMonitorWindow window,Control content,Func<PreviewSettings> preferences,Action reset) {
        this.window=window;this.content=content;this.preferences=preferences;this.reset=reset;
        timer.Tick+=(_,_)=>Refresh();
        screenshot.Tick+=(_,_)=>{screenshot.Stop();ScreenshotActive=false;Update();Changed?.Invoke();};
    }
    public void Update() {
        if(disposed)return;
        if(!preferences().DesktopLocalContrast||WindowsBackgroundCapture.HighContrastActive){screenshot.Stop();ScreenshotActive=false;Stop();return;}
        if(!window.IsVisible||ScreenshotActive){Stop(!ScreenshotActive);return;}
        if(capture==null) {
            var handle=window.TryGetPlatformHandle();if(handle?.HandleDescriptor!="HWND")return;
            capture=new WindowsBackgroundCapture(handle.Handle);
            if(!capture.Enable()){capture.Dispose();capture=null;Status(false);return;}
            analysis=new();previousPosition=null;generation++;
        }
        timer.Start();Refresh();
    }
    void Status(bool value){if(Available==value)return;Available=value;Changed?.Invoke();}
    public void BeginScreenshot() {
        if(disposed||!preferences().DesktopLocalContrast||!window.IsVisible)return;ScreenshotActive=true;Stop(false);screenshot.Stop();screenshot.Start();Changed?.Invoke();
    }
    void Stop(bool restore=true) {timer.Stop();generation++;capture?.Dispose();capture=null;analysis=null;previousPosition=null;Status(false);if(restore)reset();}
    async void Refresh() {
        if(disposed||busy||capture==null||!window.IsVisible)return;
        if(WindowsBackgroundCapture.HighContrastActive){Stop();return;}
        var current=capture;var analyzer=analysis!;int epoch=generation;
        bool iconsFollow=preferences().DesktopAppIconColors;
        var regions=new List<Region>();
        foreach(var control in content.GetVisualDescendants().OfType<Control>()) {
            if(!control.IsEffectivelyVisible||control is not TextBlock and not Avalonia.Controls.Shapes.Path)continue;
            var start=control.TranslatePoint(default,window);var end=control.TranslatePoint(new Point(control.Bounds.Width,control.Bounds.Height),window);
            if(start==null||end==null||end.Value.X<=start.Value.X||end.Value.Y<=start.Value.Y)continue;
            var old=control is TextBlock text?text.Foreground:control is Avalonia.Controls.Shapes.Path icon?icon.Stroke??icon.Fill:null;
            byte prior=old is ISolidColorBrush brush&&brush.Color.R<128?(byte)20:(byte)245;
            regions.Add(new(control,new Rect(start.Value,end.Value),prior));
        }
        var size=window.ClientSize;var position=window.Position;
        if(size.Width<=0||size.Height<=0)return;
        bool moved=previousPosition!=position;previousPosition=position;
        var backing=window.ContrastBackground;
        double radius=window.FontSize*.4;
        busy=true;
        try {
            var shades=await Task.Run(()=>{
                Shade[]? result=null;
                current.Read(frame=>{
                    double sx=frame.Width/size.Width,sy=frame.Height/size.Height;
                    analyzer.Analyze(frame.Pixels,frame.Width,frame.Height,Math.Max(1,(int)Math.Round(radius*sx)),backing.R,backing.G,backing.B,backing.A,moved);
                    result=new Shade[regions.Count];
                    for(int i=0;i<regions.Count;i++) {
                        var r=regions[i];byte shade=analyzer.RegionColor((int)(r.Bounds.X*sx),(int)(r.Bounds.Y*sy),Math.Max(1,(int)Math.Ceiling(r.Bounds.Width*sx)),Math.Max(1,(int)Math.Ceiling(r.Bounds.Height*sy)),r.Previous,out double minority);
                        result[i]=new(shade,minority);
                    }
                });return result;
            });
            if(disposed||generation!=epoch||capture!=current||!window.IsVisible||window.Position!=position||window.ClientSize!=size||window.ContrastBackground!=backing||preferences().DesktopAppIconColors!=iconsFollow)return;
            Status(shades!=null);
            if(shades==null){reset();return;}
            for(int i=0;i<regions.Count;i++) {
                var c=regions[i].Control;var s=shades[i];
                // The metric container applies the saved text opacity exactly once.
                var color=Color.FromRgb(s.Value,s.Value,s.Value);
                bool palette=c is Avalonia.Controls.Shapes.Path&&iconsFollow;
                if(c is TextBlock text) {if(text.Foreground is not ISolidColorBrush old||old.Color!=color)text.Foreground=new SolidColorBrush(color);}
                else if(c is Avalonia.Controls.Shapes.Path icon&&!iconsFollow) {
                    var old=(icon.Stroke??icon.Fill) as ISolidColorBrush;
                    if(old?.Color!=color){if(icon.Stroke!=null)icon.Stroke=new SolidColorBrush(color);else icon.Fill=new SolidColorBrush(color);}
                }
                bool edge=palette||s.Minority>(c.Effect==null?.12:.06);
                var edgeColor=s.Value==20?Colors.White:Colors.Black;
                if(palette&&c is Avalonia.Controls.Shapes.Path artwork&&(artwork.Stroke??artwork.Fill) is ISolidColorBrush ink) {
                    static double Linear(byte value){double v=value/255d;return v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4);}
                    double luminance=.2126*Linear(ink.Color.R)+.7152*Linear(ink.Color.G)+.0722*Linear(ink.Color.B);
                    edgeColor=luminance<.18?Colors.White:Colors.Black;
                }
                if(!edge)c.Effect=null;
                else if(c.Effect is not DropShadowEffect effect||effect.Color!=edgeColor)c.Effect=new DropShadowEffect{BlurRadius=1.5,Opacity=.85,OffsetX=0,OffsetY=0,Color=edgeColor};
            }
        }catch(System.ComponentModel.Win32Exception){Status(false);reset();}
        finally{busy=false;}
    }
    public void Dispose(){if(disposed)return;disposed=true;screenshot.Stop();Stop();}
}
