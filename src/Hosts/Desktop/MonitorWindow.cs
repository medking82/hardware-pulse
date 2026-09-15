using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Platform;
using System.Xml.Linq;

namespace HardwarePulse.Desktop;

public sealed class MonitorWindow : Window {
    readonly MonitorSource source;
    readonly bool smoke;
    readonly CancellationTokenSource stop=new();
    readonly TextBlock cpu=Value(),ram=Value(),down=Value(),up=Value();
    readonly TextBlock status=new(){Text="Starting…",TextWrapping=TextWrapping.Wrap};
    readonly Grid cards=new(){ColumnDefinitions=new("*,*"),RowDefinitions=new("Auto,Auto")};
    readonly Border[] panels;
    readonly ComboBox interfaces=new(){HorizontalAlignment=HorizontalAlignment.Stretch,PlaceholderText="Select network interface"};
    readonly CheckBox pause=new(){Content="Pause monitoring"};
    public Task Sampling {get;private set;}=Task.CompletedTask;
    public MonitorWindow(MonitorSource source,bool smoke=false,bool start=true) {
        this.source=source;this.smoke=smoke;
        Title="Pulse · Desktop preview";Width=800;Height=560;MinWidth=360;MinHeight=400;
        FontSize=15;
        var heading=new TextBlock{Text="Pulse",FontSize=32,FontWeight=FontWeight.SemiBold};
        panels=[Card("CPU",cpu,"System load","cpu"),Card("Memory",ram,OperatingSystem.IsMacOS()?"Used memory estimate":"Host memory","memory"),Card("Download",down,"Selected interface","down"),Card("Upload",up,"Selected interface","up")];
        foreach(var panel in panels)cards.Children.Add(panel);
        var body=new StackPanel{Spacing=16,Margin=new Thickness(24)};
        body.Children.Add(heading);body.Children.Add(status);body.Children.Add(interfaces);body.Children.Add(cards);body.Children.Add(pause);
        body.Children.Add(new TextBlock{Text="Preview · CPU, memory and network. Temperature, fans, FPS, AI quota and Desktop overlay are not connected yet.",TextWrapping=TextWrapping.Wrap,Opacity=.75});
        Content=new ScrollViewer{Content=body,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled};
        SizeChanged+=(_,_)=>LayoutCards();LayoutCards();
        if(start)Opened+=(_,_)=>Sampling=SampleAsync();
        Closed+=(_,_)=>stop.Cancel();
    }
    static TextBlock Value()=>new(){Text="—",FontSize=23,FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.Wrap};
    static Border Card(string title,TextBlock value,string detail,string icon) {
        var stack=new StackPanel{Spacing=10};
        var header=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10};
        using(var stream=AssetLoader.Open(new Uri($"avares://Pulse.Desktop/Assets/{icon}.svg"))) {
            // These repository-owned icons contain one stroked path in a 24x24 viewBox.
            var path=XDocument.Load(stream).Descendants().Single(x=>x.Name.LocalName=="path");
            header.Children.Add(new Avalonia.Controls.Shapes.Path{Data=Geometry.Parse(path.Attribute("d")!.Value),Width=24,Height=24,Stroke=new SolidColorBrush(Color.Parse("#38877E")),StrokeThickness=1.7,StrokeLineCap=PenLineCap.Round,StrokeJoin=PenLineJoin.Round});
        }
        header.Children.Add(new TextBlock{Text=title,FontWeight=FontWeight.SemiBold,VerticalAlignment=VerticalAlignment.Center});
        stack.Children.Add(header);stack.Children.Add(value);
        stack.Children.Add(new TextBlock{Text=detail,Opacity=.75,TextWrapping=TextWrapping.Wrap});
        return new Border{Child=stack,Padding=new Thickness(20),Margin=new Thickness(0,0,12,12),CornerRadius=new CornerRadius(14),BorderThickness=new Thickness(1),BorderBrush=Brushes.Gray};
    }
    void LayoutCards() {
        int count=ClientSize.Width>=660?2:1;
        cards.ColumnDefinitions=new(count==2?"*,*":"*");
        cards.RowDefinitions=new(count==2?"Auto,Auto":"Auto,Auto,Auto,Auto");
        for(int i=0;i<panels.Length;i++){Grid.SetRow(panels[i],i/count);Grid.SetColumn(panels[i],i%count);}
    }
    public void Present(MonitorSnapshot snapshot) {
        cpu.Text=snapshot.Cpu;ram.Text=snapshot.Memory;down.Text=snapshot.Download;up.Text=snapshot.Upload;
        status.Text=source.IsDemo?"Demo · Sample values":snapshot.CpuReady&&snapshot.MemoryReady?"Live · Refreshes every second":"Waiting for available readings…";
    }
    async Task SampleAsync() {
        try {
            var names=await Task.Run(source.Interfaces,stop.Token);
            if(stop.IsCancellationRequested)return;
            interfaces.ItemsSource=names;if(names.Length>0)interfaces.SelectedIndex=0;
            int samples=0;
            using var timer=new PeriodicTimer(TimeSpan.FromSeconds(1));
            do {
                if(pause.IsChecked==true){status.Text="Paused";continue;}
                string? name=interfaces.SelectedItem as string;
                var snapshot=await Task.Run(()=>source.Poll(name),stop.Token);
                if(stop.IsCancellationRequested)return;
                Present(snapshot);
                if(smoke&&++samples==3) {
                    if(!snapshot.CpuReady||!snapshot.MemoryReady)Environment.ExitCode=3;
                    Console.WriteLine(snapshot.CpuReady&&snapshot.MemoryReady?(source.IsDemo?"PASS native Desktop UI with explicit demo values":"PASS native Desktop UI and live CPU/RAM"):"FAIL native Desktop telemetry");
                    Close();return;
                }
            } while(await timer.WaitForNextTickAsync(stop.Token));
        } catch(OperationCanceledException) when(stop.IsCancellationRequested){}
        catch(Exception) {if(!stop.IsCancellationRequested)status.Text="Monitoring unavailable. Close and reopen to retry.";if(smoke){Environment.ExitCode=3;Close();}}
    }
}
