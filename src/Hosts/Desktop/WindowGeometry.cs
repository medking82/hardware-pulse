using Avalonia;
using Avalonia.Controls;

namespace HardwarePulse.Desktop;

public static class WindowGeometry {
    // WPF stores window location in DIPs, Avalonia Position uses physical pixels.
    // Match the converted point to its monitor before applying the saved bounds.
    public static PixelPoint? FromLegacy(double? left,double? top,IEnumerable<(PixelRect Bounds,double Scaling)> monitors,double fallbackScale=1) {
        if(left is not double x||top is not double y||!double.IsFinite(x)||!double.IsFinite(y)||Math.Abs(x)>100000||Math.Abs(y)>100000)return null;
        PixelPoint Convert(double scale)=>new((int)Math.Round(x*scale),(int)Math.Round(y*scale));
        foreach(var (bounds,scale) in monitors)if(double.IsFinite(scale)&&scale is >0 and <=8) {
            var point=Convert(scale);if(bounds.Contains(point))return point;
        }
        return Convert(double.IsFinite(fallbackScale)&&fallbackScale is >0 and <=8?fallbackScale:1);
    }
    public static PixelPoint? LegacyPosition(Window window,double? left,double? top)=>FromLegacy(left,top,
        window.Screens.All.Select(screen=>(screen.Bounds,screen.Scaling)),window.Screens.Primary?.Scaling??1);
    public static void RestoreApp(Window window,PreviewSettings settings) {
        var position=settings.AppX is int x&&settings.AppY is int y?new PixelPoint(x,y):LegacyPosition(window,settings.LegacyLeft,settings.LegacyTop);
        var screen=window.Screens.ScreenFromPoint(position??window.Position)??window.Screens.Primary;
        if(screen==null){if(position is PixelPoint saved)window.Position=saved;return;}
        var area=screen.WorkingArea;double scale=screen.Scaling;
        window.Width=Math.Max(window.MinWidth,Math.Min(window.Width,area.Width/scale));
        window.Height=Math.Max(window.MinHeight,Math.Min(window.Height,area.Height/scale));
        // A mapped X11 window reports its old Position until ConfigureNotify.
        // Clamp the requested point before sending one move, not a stale getter.
        if(position is PixelPoint target)window.Position=new PixelPoint(Math.Clamp(target.X,area.X,Math.Max(area.X,area.Right-(int)Math.Ceiling(window.Width*scale))),
            Math.Clamp(target.Y,area.Y,Math.Max(area.Y,area.Bottom-(int)Math.Ceiling(window.Height*scale))));
    }
}
