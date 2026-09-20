using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class ReadingPaletteTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static Color BrushColor(IBrush? brush)=>((ISolidColorBrush)brush!).Color;
    static Color Icon(Control control) {
        var path=control.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().First();
        return BrushColor(path.Stroke??path.Fill);
    }
    public static void Run(string? output=null) {
        string directory=Directory.CreateTempSubdirectory("pulse-palette-").FullName;
        MonitorWindow? owner=null;
        try {
            string path=Path.Combine(directory,"settings.json");File.WriteAllText(path,"{\"future\":7}");
            var store=new PreviewSettingsStore(path);
            owner=new MonitorWindow(new MonitorSource(true),start:false,store:store);owner.Show();
            owner.Present(new("24%","8 GiB","0","0",true,true){Hardware=new(){state="LIVE",values=new(){{"cpu",55}}}});
            owner.OpenFloatingMonitor();Dispatcher.UIThread.RunJobs();
            var desktop=owner.FloatingMonitor!;
            var card=owner.GetVisualDescendants().OfType<Border>().Single(x=>x.Name=="CardCPU");
            var row=desktop.GetVisualDescendants().OfType<Grid>().Single(x=>x.Name=="DesktopMetricCPU");
            var hero=card.GetVisualDescendants().OfType<TextBlock>().Single(x=>x.Text=="55.0 °C");
            var label=card.GetVisualDescendants().OfType<TextBlock>().Single(x=>x.Text=="CPU");
            var neutral=BrushColor(label.Foreground);
            Check(Icon(card)==Color.Parse("#A5E7D5")&&Icon(row)==Icon(card),"Original hardware palette is shared by default");
            owner.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="OpenSettings").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Dispatcher.UIThread.RunJobs();
            var tabs=owner.GetVisualDescendants().OfType<TabControl>().Single(x=>x.Name=="SettingsTabs");tabs.SelectedIndex=1;Dispatcher.UIThread.RunJobs();
            var picker=owner.GetVisualDescendants().OfType<ColorPicker>().Single(x=>x.Name=="ReadingColorPicker");
            Check(!picker.IsVisible,"Hardware palette must not show a unified color editor");
            owner.GetVisualDescendants().OfType<RadioButton>().Single(x=>x.Name=="UnifiedReadingColors").IsChecked=true;
            picker.Color=Color.Parse("#80D4FA");Dispatcher.UIThread.RunJobs();
            owner.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="Back").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Dispatcher.UIThread.RunJobs();
            Check(picker.IsVisible&&Icon(card)==picker.Color&&BrushColor(hero.Foreground)==picker.Color&&Icon(row)==picker.Color,"Unified color reaches existing Monitor and Desktop without another poll");
            Check(BrushColor(label.Foreground)==neutral,"Palette change recolored neutral labels");
            if(output!=null){owner.Width=360;Dispatcher.UIThread.RunJobs();using var frame=owner.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"palette-monitor.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
            owner.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="OpenSettings").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Dispatcher.UIThread.RunJobs();
            tabs.SelectedIndex=2;Dispatcher.UIThread.RunJobs();
            var follow=owner.GetVisualDescendants().OfType<CheckBox>().Single(x=>x.Name=="DesktopAppIconColors");follow.IsChecked=false;
            var text=owner.GetVisualDescendants().OfType<ColorPicker>().Single(x=>x.Name=="DesktopColor");text.Color=Color.Parse("#F4C8E0");Dispatcher.UIThread.RunJobs();
            Check(Icon(row)==text.Color&&row.Children.OfType<TextBlock>().All(x=>BrushColor(x.Foreground)==text.Color),"Desktop text and non-following icons use the selected color");
            Check(Icon(card)==picker.Color&&BrushColor(desktop.Foreground)==Color.Parse("#F5F7FA"),"Desktop color leaked into App or editor controls");
            follow.IsChecked=true;Dispatcher.UIThread.RunJobs();
            Check(Icon(row)==picker.Color&&row.Children.OfType<TextBlock>().All(x=>BrushColor(x.Foreground)==text.Color),"Restoring App icon colors lost independent Desktop text");
            var topmost=owner.GetVisualDescendants().OfType<CheckBox>().Single(x=>x.Name=="DesktopAlwaysOnTop");topmost.IsChecked=true;
            Check(Icon(row)==picker.Color,"Topmost changed the explicit icon palette");
            if(output!=null){Dispatcher.UIThread.RunJobs();using var frame=desktop.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"palette-desktop.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
            owner.Close();owner=null;
            var saved=store.Load();Check(saved.UnifiedReadingColors&&saved.ReadingColor=="#80D4FA"&&saved.DesktopColor=="#F4C8E0"&&saved.DesktopAppIconColors,"Color preferences did not persist");
            using(var json=System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)))Check(json.RootElement.GetProperty("future").GetInt32()==7,"Color save lost unknown preferences");
            File.WriteAllText(path,"{\"readingColor\":\"#00000000\",\"desktopColor\":\"red\",\"desktopAppIconColors\":false}");
            var invalid=new PreviewSettingsStore(path).Load();Check(invalid.ReadingColor=="#DDE9F0"&&invalid.DesktopColor=="#F5F7FA"&&!invalid.DesktopAppIconColors,"Invalid colors must fall back without discarding valid settings");
        } finally {owner?.Close();Directory.Delete(directory,true);}
        Console.WriteLine("PASS shared reading palette: App/temperature/Desktop parity, independent text, topmost, persistence and invalid color fallback");
    }
}
