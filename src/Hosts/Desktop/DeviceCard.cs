using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace HardwarePulse.Desktop;

// The device hierarchy, colors and measurements follow Native/Cards.cs.
// Presentation only: no collector, timer or provider calls.
sealed class DeviceCard : Border {
    sealed record Metric(string Key,string Label,string ShortLabel,string Unit);
    readonly string key;
    ReadingPalette palette=new();
    public string Key=>key;
    readonly UiLanguage language;
    readonly TextBlock subtitle=new(){Name="DeviceSubtitle",FontSize=10,Foreground=Brush.Parse("#DDE9F0"),Margin=new(0,3,0,6)};
    readonly TextBlock hero=new(){FontSize=21,HorizontalAlignment=HorizontalAlignment.Right};
    readonly Grid rows=new(){Name="DeviceMetrics",ColumnDefinitions=new("*,*"),ColumnSpacing=10};
    readonly Grid header=new(){ColumnDefinitions=new("*,Auto"),RowDefinitions=new("Auto,Auto"),ColumnSpacing=10};
    readonly StackPanel titleRow=new(){Orientation=Orientation.Horizontal,Spacing=8};
    readonly TextBlock title=new(){FontSize=13,FontWeight=FontWeight.SemiBold,VerticalAlignment=VerticalAlignment.Center};
    bool? displayedDetails;
    readonly Grid? pairs;
    readonly Avalonia.Controls.Shapes.Path icon;
    readonly List<(Metric Metric,Grid Row,TextBlock Label,TextBlock Value)> metrics=new();
    readonly TextBlock usage=new(){FontSize=12,Margin=new(0,3,0,2)};
    readonly Grid usageBar=new(){Name="DeviceUsageTrack",Height=3,ColumnDefinitions=new("0,*")};
    readonly string? heroKey,usageKey;
    public DeviceCard(string key,UiLanguage language) {
        this.key=key;this.language=language;Name="Card"+key;
        string accent=palette.ForIcon(key.ToLowerInvariant());
        heroKey=key switch {"CPU"=>"cpu","GPU"=>"gpu","Airflow"=>"system",_=>null};
        usageKey=key=="GPU"?"vram":key=="Memory"?"ram":null;
        CornerRadius=new(14);Padding=new(10,7);BorderThickness=new(1);
        BorderBrush=Brush.Parse("#426D8B9F");Background=Brush.Parse("#3031485B");
        var body=new StackPanel();
        icon=AppIcon.Create(key.ToLowerInvariant());
        icon.Stroke=icon.Stroke==null?null:Brush.Parse(accent);icon.Fill=icon.Fill==null?null:Brush.Parse(accent);
        titleRow.Children.Add(new Viewbox{Width=18,Height=18,Child=icon});
        titleRow.Children.Add(language.Set(title,key));
        header.Children.Add(titleRow);hero.Foreground=Brush.Parse(accent);Grid.SetColumn(hero,1);header.Children.Add(hero);
        body.Children.Add(header);body.Children.Add(subtitle);body.Children.Add(rows);
        Metric[] definitions=key switch {
            "CPU"=>[new("cpuLoad","Utilization","Load","%"),new("vcore","Vcore · Motherboard","Vcore","V"),new("cpuFan","CPU Fan","Fan","RPM")],
            "GPU"=>[new("gpuLoad","Utilization","Load","%"),new("vram","VRAM Junction","VRAM Temp","°C"),new("gpuVolt","Core Voltage","Voltage","V"),new("gpuFan","Fan Speed","Fan","RPM")],
            "Memory"=>[new("ramA","Module 1","Module 1","°C"),new("ramB","Module 2","Module 2","°C")],
            "NVMe"=>[new("diskC","Drive 1","Drive 1","°C"),new("diskD","Drive 2","Drive 2","°C")],
            "Airflow"=>[new("bottom","System Fan 1","System Fan 1","RPM"),new("top","System Fan 2","System Fan 2","RPM")],
            _=>[new("lanLink","LAN Link Speed","LAN Link Speed","link"),new("wifiLink","Wi-Fi Link Speed","Wi-Fi Link Speed","link"),new("wifiSignal","Wi-Fi Signal","Wi-Fi Signal","%"),new("netDown","Download","Download","rate"),new("netUp","Upload","Upload","rate")]
        };
        rows.RowDefinitions=new(string.Join(",",Enumerable.Repeat("Auto",definitions.Length)));
        if(key is "Memory" or "NVMe") {pairs=new Grid{ColumnDefinitions=new("*,*"),ColumnSpacing=6};Grid.SetColumnSpan(pairs,2);rows.Children.Add(pairs);}
        foreach(var metric in definitions) {
            var row=new Grid{ColumnDefinitions=new("*,Auto"),ColumnSpacing=10,Margin=new(0,2)};
            var label=new TextBlock{TextWrapping=TextWrapping.Wrap};
            var value=new TextBlock{FontWeight=FontWeight.SemiBold,VerticalAlignment=VerticalAlignment.Center};
            Grid.SetColumn(value,1);row.Children.Add(label);row.Children.Add(value);
            if(pairs==null)rows.Children.Add(row);
            else {
                row.ColumnDefinitions=new("*");row.RowDefinitions=new("Auto,Auto");Grid.SetColumn(value,0);Grid.SetRow(value,1);
                Grid.SetColumn(row,metrics.Count);pairs.Children.Add(row);
            }
            metrics.Add((metric,row,label,value));
        }
        usageBar.Background=Brush.Parse("#203C5266");
        usageBar.Children.Add(new Border{Name="DeviceUsageFill",Background=Brush.Parse(accent),CornerRadius=new(2)});
        body.Children.Add(usage);body.Children.Add(usageBar);Child=body;
        SizeChanged+=(_,_)=>Reflow(Bounds.Width);
        PropertyChanged+=(_,e)=>{if(e.Property==ThemeVariantScope.ActualThemeVariantProperty)ApplyPalette();};ApplyPalette();
    }
    void Reflow(double availableWidth) {
        if(availableWidth<=0)return;
        double width=Math.Max(0,availableWidth-Padding.Left-Padding.Right-BorderThickness.Left-BorderThickness.Right);
        var unbounded=new Size(double.PositiveInfinity,double.PositiveInfinity);
        titleRow.Measure(unbounded);hero.Measure(unbounded);
        bool stacked=hero.IsVisible&&titleRow.DesiredSize.Width+hero.DesiredSize.Width+header.ColumnSpacing>width;
        Grid.SetRow(hero,stacked?1:0);Grid.SetColumn(hero,stacked?0:1);Grid.SetColumnSpan(hero,stacked?2:1);
        hero.HorizontalAlignment=stacked?HorizontalAlignment.Left:HorizontalAlignment.Right;
        hero.Margin=new Thickness(0,stacked?3:0,0,0);
        if(pairs==null) {
            var visible=metrics.Where(x=>x.Row.IsVisible).ToArray();
            bool compact=displayedDetails!=true&&key!="Airflow";
            double widest=0;
            if(compact)foreach(var item in visible){item.Row.Measure(unbounded);widest=Math.Max(widest,item.Row.DesiredSize.Width);}
            int columns=compact&&2*widest+10<=width?2:1;
            for(int i=0;i<visible.Length;i++){Grid.SetRow(visible[i].Row,i/columns);Grid.SetColumn(visible[i].Row,i%columns);Grid.SetColumnSpan(visible[i].Row,columns==1?2:1);}
        }
    }
    public void ApplyDensity(double fontSize,int level,double width) {
        double scale=fontSize/12;bool details=displayedDetails==true,full=details||key=="Airflow";
        Padding=level==0?new Thickness(10,7):new Thickness(7,level==3?3:5);
        title.FontSize=13*scale;subtitle.FontSize=10*scale;hero.FontSize=(details?23:21)*scale;usage.FontSize=12*scale;
        subtitle.IsVisible=level<3||details;
        foreach(var item in metrics) {
            item.Label.FontSize=(pairs!=null?10:full?12:10)*scale;
            item.Value.FontSize=(pairs!=null?(details?21:18):full?12:11)*scale;
        }
        Reflow(width);
    }
    public void ApplyReadingPalette(ReadingPalette value){palette=value;ApplyPalette();}
    void ApplyPalette() {
        bool light=ActualThemeVariant==ThemeVariant.Light;
        var color=Brush.Parse(palette.ForIcon(key.ToLowerInvariant(),light));
        Background=Brush.Parse(light?"#DDEEF1F4":"#3031485B");
        subtitle.Foreground=Brush.Parse(light?"#17202B":"#DDE9F0");hero.Foreground=color;
        icon.Stroke=icon.Stroke==null?null:color;icon.Fill=icon.Fill==null?null:color;
        ((Border)usageBar.Children[0]).Background=color;
        foreach(var item in metrics)if(item.Metric.Unit=="°C")item.Value.Foreground=color;
        // Labels and non-temperature measurements retain neutral foregrounds.
    }
    public void Present(MonitorSnapshot snapshot,bool maximum,bool details,IReadOnlyDictionary<string,string>? names=null) {
        bool modeChanged=displayedDetails!=details;
        displayedDetails=details;
        var reading=snapshot.Hardware;
        bool Has(string metric)=>reading?.values.ContainsKey(metric)==true||reading?.available?.GetValueOrDefault(metric)==true;
        string NameOf(string metric,string fallback)=>HardwareNames.Get(names,metric)??reading?.names.GetValueOrDefault(metric)??language.T(fallback);
        string Value(string metric,string unit) {
            if(metric=="cpuLoad"&&!Has(metric))return maximum?snapshot.PeakCpu:snapshot.Cpu;
            if(metric=="netDown")return maximum?snapshot.PeakDownload:snapshot.Download;
            if(metric=="netUp")return maximum?snapshot.PeakUpload:snapshot.Upload;
            var values=maximum&&unit!="link"?snapshot.HardwarePeaks:reading?.state=="LIVE"?reading.values:null;
            if(values?.TryGetValue(metric,out double value)!=true)return "—";
            return unit=="link"?language.T(NetworkRate.Link(value)):ReadingFormat.SensorNumber(value,unit)+" "+unit;
        }
        string fallback=key switch {"CPU"=>"Processor","GPU"=>"Graphics","NVMe"=>"NVMe · Composite Temperature","Airflow"=>"Case / Motherboard","Network"=>"Active adapter",_=>key};
        subtitle.Text=key=="Network"?snapshot.NetworkName??language.T(fallback):NameOf(key,fallback);
        subtitle.TextWrapping=details?TextWrapping.Wrap:TextWrapping.NoWrap;subtitle.TextTrimming=TextTrimming.CharacterEllipsis;
        hero.IsVisible=heroKey!=null&&Has(heroKey);hero.Text=heroKey==null?"":Value(heroKey,"°C");hero.FontSize=details?23:21;
        foreach(var (metric,row,label,value) in metrics) {
            row.IsVisible=metric.Key is "cpuLoad" or "netDown" or "netUp"||Has(metric.Key);
            bool full=details||key=="Airflow";
            label.Text=full?NameOf(metric.Key,metric.Label):language.T(metric.ShortLabel);
            label.FontSize=full?12:10;value.FontSize=full?12:11;value.Text=Value(metric.Key,metric.Unit);
            label.TextWrapping=full?TextWrapping.Wrap:TextWrapping.NoWrap;
            if(pairs==null&&modeChanged){row.ColumnDefinitions[0].Width=full?new GridLength(1,GridUnitType.Star):GridLength.Auto;row.ColumnSpacing=full?10:4;}
            if(pairs!=null) {
                label.FontSize=10;value.FontSize=details?21:18;value.Margin=new(0,details?6:0,0,0);
                pairs.ColumnDefinitions[Grid.GetColumn(row)].Width=row.IsVisible?new GridLength(1,GridUnitType.Star):new GridLength(0);
            }
            if(metric.Key=="gpuFan"&&reading?.gpuFanCount>1)value.Text=Value("gpuFan","RPM").Replace(" RPM","")+" / "+Value("gpuFan2","RPM");
        }
        usage.IsVisible=usageBar.IsVisible=usageKey!=null;
        if(usageKey!=null) {
            usage.Margin=new Thickness(0,details?8:3,0,2);
            var current=reading?.state=="LIVE"?reading.usage.GetValueOrDefault(usageKey):null;
            usage.Text=current!=null?language.T(usageKey.ToUpperInvariant())+" "+ReadingFormat.UsageText(current):usageKey=="ram"?"RAM "+snapshot.Memory:"VRAM —";
            double percent=current!=null&&double.IsFinite(current.percent)?Math.Clamp(current.percent,0,100):0;
            usageBar.ColumnDefinitions[0].Width=new GridLength(percent,GridUnitType.Star);
            usageBar.ColumnDefinitions[1].Width=new GridLength(100-percent,GridUnitType.Star);
            usageBar.IsVisible=current!=null;
        }
        IsVisible=key is "CPU" or "Memory" or "Network"||heroKey!=null&&Has(heroKey)||metrics.Any(x=>Has(x.Metric.Key))||reading?.usage.ContainsKey(usageKey??"")==true;
        Reflow(Bounds.Width);
    }
}
