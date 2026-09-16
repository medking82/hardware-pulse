using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Media;
using Avalonia.Platform;
using System.Xml.Linq;

namespace HardwarePulse.Desktop;

// Repository-owned single-path icons with a 24x24 viewBox, not a general SVG loader.
static class AppIcon {
    public static IBrush Brush(string name)=>new SolidColorBrush(Color.Parse(name switch {"memory"=>"#B78B57","codex"=>"#38877E","claude"=>"#C27855",_=>"#38877E"}));
    public static Path Create(string name) {
        using var stream=AssetLoader.Open(new Uri($"avares://Pulse.Desktop/Assets/{name}.svg"));
        var path=XDocument.Load(stream).Descendants().Single(x=>x.Name.LocalName=="path");
        var brush=Brush(name);
        bool stroked=path.Attribute("stroke")!=null;
        return new Path{Tag=name,Data=Geometry.Parse(path.Attribute("d")!.Value),Width=24,Height=24,
            Stroke=stroked?brush:null,Fill=stroked?null:brush,StrokeThickness=1.7,
            StrokeLineCap=PenLineCap.Round,StrokeJoin=PenLineJoin.Round};
    }
}
