using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace HardwarePulse.Desktop;

// UI-thread owner. Reuse text controls during normal polling; rebuild only on topology change.
public sealed class HardwareSensorPanel : Border {
    readonly StackPanel rows=new(){Spacing=12};
    readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Opacity=.75};
    readonly Dictionary<string,(TextBlock Label,TextBlock Value)> controls=new();
    string[] order=[];
    readonly UiLanguage language;
    readonly string emptyStatus;
    public HardwareSensorPanel(UiLanguage? language=null,string title="Temperature & fans",string emptyStatus="No temperature or fan sensors exposed by this device.") {
        this.language=language??new UiLanguage();
        this.emptyStatus=emptyStatus;
        Name="HardwareSensors";Padding=new Thickness(20);CornerRadius=new CornerRadius(14);
        BorderThickness=new Thickness(1);BorderBrush=Brushes.Gray;
        var body=new StackPanel{Spacing=14};
        body.Children.Add(this.language.Set(new TextBlock{FontWeight=FontWeight.SemiBold},title));
        body.Children.Add(status);body.Children.Add(rows);Child=body;
        Present([],false);
    }
    public void Present(IReadOnlyList<HardwareSensorSnapshot> sensors,bool supported) {
        language.Set(status,!supported?"Not available on this platform yet."
            :sensors.Count==0?emptyStatus
            :"Device labels · Unavailable readings show —");
        if(!order.SequenceEqual(sensors.Select(x=>x.Id))) {
            controls.Clear();rows.Children.Clear();order=sensors.Select(x=>x.Id).ToArray();
            foreach(var sensor in sensors) {
                var label=new TextBlock{TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,12,0)};
                var value=new TextBlock{FontWeight=FontWeight.SemiBold,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Right};
                var row=new Grid{ColumnDefinitions=new("*,Auto")};
                Grid.SetColumn(value,1);row.Children.Add(label);row.Children.Add(value);rows.Children.Add(row);
                controls.Add(sensor.Id,(label,value));
            }
        }
        foreach(var sensor in sensors) {
            var control=controls[sensor.Id];control.Label.Text=sensor.Label;control.Value.Text=sensor.Value;
        }
    }
}
