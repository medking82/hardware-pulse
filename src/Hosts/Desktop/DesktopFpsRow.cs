using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace HardwarePulse.Desktop;

// Four stable cells keep FPS readable without treating unavailable 1% low as zero.
public sealed class DesktopFpsRow : Grid {
    readonly TextBlock[] values=new TextBlock[4];
    readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,FontSize=12};
    public DesktopFpsRow() {
        Name="DesktopFpsReading";ColumnDefinitions=new("*,*,*,*");RowDefinitions=new("Auto,Auto,Auto");ColumnSpacing=8;RowSpacing=3;
        string[] labels=["FPS","AVG","MIN","1% LOW"];
        for(int i=0;i<4;i++) {
            var label=new TextBlock{Text=labels[i],FontSize=11};Grid.SetColumn(label,i);Children.Add(label);
            var value=values[i]=new TextBlock{Name="FpsMetric"+i,Text="—",FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.NoWrap};
            var box=new Viewbox{Child=value,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Left};
            Grid.SetColumn(box,i);Grid.SetRow(box,1);Children.Add(box);
        }
        Grid.SetRow(status,2);Grid.SetColumnSpan(status,4);Children.Add(status);
    }
    public void Present(DesktopFpsSnapshot snapshot,UiLanguage language) {
        string[] readings=[snapshot.Current,snapshot.Average,snapshot.Minimum,snapshot.Low];
        for(int i=0;i<values.Length;i++)values[i].Text=readings[i];
        status.Text=language.T(snapshot.Status);
        Avalonia.Automation.AutomationProperties.SetName(this,$"FPS {snapshot.Current}, AVG {snapshot.Average}, MIN {snapshot.Minimum}, 1% LOW {snapshot.Low}. {status.Text}");
    }
    public void SetReadingSize(double size){foreach(var value in values)value.FontSize=size;RowDefinitions[1].Height=new GridLength(Math.Ceiling(size*1.5));}
}
