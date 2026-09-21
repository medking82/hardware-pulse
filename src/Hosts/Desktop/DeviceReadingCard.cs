using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace HardwarePulse.Desktop;

// Presentation only: the same immutable snapshot still feeds Monitor and Desktop.
public sealed class DeviceReadingCard : Border {
    readonly UiLanguage language;
    readonly string? heroKey;
    readonly TextBlock hero=new(){FontSize=23,FontWeight=FontWeight.SemiBold,VerticalAlignment=VerticalAlignment.Center};
    readonly TextBlock subtitle=new(){FontSize=10,Opacity=.75,TextWrapping=TextWrapping.Wrap};
    readonly StackPanel readings=new(){Spacing=4};
    readonly Dictionary<string,(Grid Row,TextBlock Label,TextBlock Value)> rows=new();
    public DeviceReadingCard(string name,string accent,string? heroKey,UiLanguage language) {
        this.language=language;this.heroKey=heroKey;Name="DeviceCard_"+name;
        ReadingCard.Apply(this);
        var brush=new SolidColorBrush(Color.Parse(accent));hero.Foreground=brush;
        var header=new DockPanel{LastChildFill=true};DockPanel.SetDock(hero,Dock.Right);header.Children.Add(hero);
        var title=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8,VerticalAlignment=VerticalAlignment.Center};
        var icon=AppIcon.Create(name.ToLowerInvariant());icon.Width=18;icon.Height=18;
        if(icon.Stroke!=null)icon.Stroke=brush;else icon.Fill=brush;
        title.Children.Add(icon);title.Children.Add(language.Set(new TextBlock{FontSize=13,FontWeight=FontWeight.SemiBold},name));header.Children.Add(title);
        Child=new StackPanel{Spacing=5,Children={header,subtitle,readings}};
    }
    public void Present(IReadOnlyList<HardwareSensorSnapshot> values,string? device=null) {
        var primary=values.FirstOrDefault(x=>x.Id==heroKey);
        hero.IsVisible=heroKey!=null;hero.Text=primary?.Value??"—";hero.Tag=heroKey==null?null:"hardware/"+heroKey;
        subtitle.Text=device??primary?.Device??"";subtitle.IsVisible=subtitle.Text.Length>0;
        var supporting=values.Where(x=>x.Id!=heroKey).ToArray();
        var active=supporting.Select(x=>x.Id).ToHashSet();
        foreach(string removed in rows.Keys.Where(x=>!active.Contains(x)).ToArray()){readings.Children.Remove(rows[removed].Row);rows.Remove(removed);}
        for(int i=0;i<supporting.Length;i++) {
            var sensor=supporting[i];
            if(!rows.TryGetValue(sensor.Id,out var row)) {
                var label=new TextBlock{FontSize=11,Opacity=.8,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};
                var value=new TextBlock{FontSize=12,FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Right,MaxWidth=180,Tag="hardware/"+sensor.Id};
                var grid=new Grid{ColumnDefinitions=new("*,Auto"),ColumnSpacing=10,Tag="device-reading"};
                Grid.SetColumn(value,1);grid.Children.Add(label);grid.Children.Add(value);
                row=(grid,label,value);rows.Add(sensor.Id,row);readings.Children.Insert(i,grid);
            }
            row.Label.Text=sensor.DisplayLabel(language);row.Value.Text=sensor.Value;
        }
    }
}
