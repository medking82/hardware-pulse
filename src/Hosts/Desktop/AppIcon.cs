using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Media;
using Avalonia.Platform;
using System.Xml.Linq;

namespace HardwarePulse.Desktop;

// Repository-owned single-path icons with a 24x24 viewBox, not a general SVG loader.
static class AppIcon {
    public static Path Create(string name) {
        using var stream=AssetLoader.Open(new Uri($"avares://Pulse.Desktop/Assets/{name}.svg"));
        var path=XDocument.Load(stream).Descendants().Single(x=>x.Name.LocalName=="path");
        var brush=new SolidColorBrush(Color.Parse("#38877E"));
        bool stroked=path.Attribute("stroke")!=null;
        return new Path{Data=Geometry.Parse(path.Attribute("d")!.Value),Width=24,Height=24,
            Stroke=stroked?brush:null,Fill=stroked?null:brush,StrokeThickness=1.7,
            StrokeLineCap=PenLineCap.Round,StrokeJoin=PenLineJoin.Round};
    }
}
