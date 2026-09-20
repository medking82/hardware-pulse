using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class NavigationTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run(string? output) {
        var source=new MonitorSource(true);
        var window=new MonitorWindow(source,start:false){Width=360,Height=400};window.Show();window.Present(source.Poll(null));Dispatcher.UIThread.RunJobs();
        try {
            T Find<T>(string name) where T:Control=>window.GetVisualDescendants().OfType<T>().Single(x=>x.Name==name);
            void Press(Button button){button.Focus();window.KeyPress(Key.Space,RawInputModifiers.None,PhysicalKey.Space," ");window.KeyRelease(Key.Space,RawInputModifiers.None,PhysicalKey.Space," ");Dispatcher.UIThread.RunJobs();}
            var cards=Find<Grid>("ReadingCards");var settings=Find<Button>("OpenSettings");
            var monitorScroll=window.GetVisualDescendants().OfType<ScrollViewer>().Single();
            var before=settings.TranslatePoint(new Point(),window)!.Value;
            monitorScroll.Offset=new Vector(0,10000);Dispatcher.UIThread.RunJobs();
            Check(settings.TranslatePoint(new Point(),window)!.Value==before,"Settings stays fixed below scrolling cards");
            Press(settings);
            var back=Find<Button>("Back");
            Check(back.IsFocused,"Settings keyboard activation transfers focus to Back");
            Check(!window.GetVisualDescendants().OfType<Grid>().Any(x=>x.Name=="ReadingCards"),"Monitor is removed from Settings navigation and focus tree");
            var tabs=Find<TabControl>("SettingsTabs");tabs.SelectedIndex=1;Dispatcher.UIThread.RunJobs();
            var scroll=window.GetVisualDescendants().OfType<ScrollViewer>().First(x=>x.Content is StackPanel);
            before=back.TranslatePoint(new Point(),window)!.Value;scroll.Offset=new Vector(0,10000);Dispatcher.UIThread.RunJobs();
            Check(back.TranslatePoint(new Point(),window)!.Value==before,"Back stays fixed above scrolling Settings");
            window.Present(source.Poll(null) with {Cpu="33.0%"});
            if(output!=null){using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"navigation-settings.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
            Press(back);
            Check(settings.IsFocused&&ReferenceEquals(cards,Find<Grid>("ReadingCards")),"Back restores focus and existing card instances");
            Check(window.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="33.0%"),"Readings received while Settings is open survive return");
            Press(settings);Check(Find<TabControl>("SettingsTabs").SelectedIndex==1,"Reopening Settings retains category selection");
        } finally {window.Close();}
        Console.WriteLine("PASS navigation: fixed Settings/Back, keyboard focus, preserved controls and readings");
    }
}
