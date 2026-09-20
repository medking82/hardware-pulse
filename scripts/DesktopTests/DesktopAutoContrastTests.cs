using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class DesktopAutoContrastTests {
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static void Run() {
        var window=new FloatingMonitorWindow(new UiLanguage("en"),nativeEffectsAllowed:false);
        var preferences=new PreviewSettings{DesktopAutoContrast=true,DesktopTopmost=true,DesktopTextOpacity=42};
        try {
            window.ApplyPreferences(preferences);window.Show();window.Present(new("20%","4 GiB","—","—",true,true));Dispatcher.UIThread.RunJobs();
            var surface=window.GetVisualDescendants().OfType<Border>().Single(x=>x.Name=="DesktopSurface");
            var row=window.GetVisualDescendants().OfType<Grid>().Single(x=>x.Name=="DesktopMetricCPU");
            Color Text()=>((ISolidColorBrush)row.Children.OfType<TextBlock>().Last().Foreground!).Color;
            Check(Text()==Color.Parse("#101820")&&((ISolidColorBrush)surface.Background!).Color.R==245,"Automatic overlay must pair dark readings with light backing");
            Check(window.ActualThemeVariant==Avalonia.Styling.ThemeVariant.Light&&((ISolidColorBrush)window.Foreground!).Color.R<128,"Editor must remain readable on automatic light backing");
            Check(((Grid)row.Parent!).Opacity==.42,"Auto contrast changed saved text opacity");
            preferences.DesktopTopmost=false;window.ApplyPreferences(preferences);
            Check(Text()==Color.Parse("#F5F7FA")&&((ISolidColorBrush)surface.Background!).Color.R==20,"Unavailable wallpaper sample must use protected light text");
            preferences.DesktopAutoContrast=false;preferences.DesktopColor="#152127";window.ApplyPreferences(preferences);
            Check(Text()==Color.Parse("#152127")&&((ISolidColorBrush)surface.Background!).Color.R==245,"Explicit dark text needs contrasting surface");
        }finally{window.Close();}
        string directory=Directory.CreateTempSubdirectory("pulse-auto-contrast-").FullName;
        try {
            var store=new PreviewSettingsStore(Path.Combine(directory,"settings.json"));var saved=new PreviewSettings{DesktopFontSize=25,DesktopSpacing=30,DesktopTextOpacity=40,DesktopBackgroundOpacity=65};store.Save(saved);
            var owner=new MonitorWindow(new MonitorSource(true),start:false,store:store);owner.Show();
            try {
                owner.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="OpenSettings").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                owner.GetVisualDescendants().OfType<TabControl>().Single().SelectedIndex=2;Dispatcher.UIThread.RunJobs();
                owner.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="DesktopRecommended").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(owner.GetVisualDescendants().OfType<CheckBox>().Single(x=>x.Name=="DesktopAutoContrast").IsChecked==true,"Recommended style did not enable Auto Contrast");
                owner.Close();var loaded=store.Load();
                Check(loaded.DesktopAutoContrast&&loaded.DesktopFontSize==16&&loaded.DesktopSpacing==10&&loaded.DesktopTextOpacity==100&&loaded.DesktopBackgroundOpacity==65,"Recommended style/persistence changed wrong preferences");
            }finally{owner.Close();}
        }finally{Directory.Delete(directory,true);}
        Console.WriteLine("PASS Desktop Auto Contrast: paired ink/surface, readable editor, fallback, opacity and original recommended-style scope");
    }
}
