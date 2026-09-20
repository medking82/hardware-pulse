using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class LocalContrastTests {
    [DllImport("user32.dll")] static extern bool GetWindowDisplayAffinity(nint window,out uint affinity);
    [DllImport("user32.dll")] static extern nint GetDC(nint window);
    [DllImport("user32.dll")] static extern int ReleaseDC(nint window,nint dc);
    [DllImport("gdi32.dll")] static extern uint GetPixel(nint dc,int x,int y);
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
    public static void Settings() {
        string directory=Directory.CreateTempSubdirectory("pulse-contrast-settings-").FullName;
        try {
            var store=new PreviewSettingsStore(System.IO.Path.Combine(directory,"settings.json"));
            var settings=store.Load();Check(!settings.DesktopLocalContrast,"Contrast must default off");
            settings.DesktopLocalContrast=true;Check(store.Save(settings),"Could not save contrast setting");
            Check(new PreviewSettingsStore(System.IO.Path.Combine(directory,"settings.json")).Load().DesktopLocalContrast,"Contrast setting did not persist");
        }finally{Directory.Delete(directory,true);}
    }
    static void Until(Func<bool> predicate,string message,int milliseconds=5000) {
        var until=DateTime.UtcNow.AddMilliseconds(milliseconds);
        while(!predicate()&&DateTime.UtcNow<until){using var slice=new CancellationTokenSource(100);Dispatcher.UIThread.MainLoop(slice.Token);}
        Check(predicate(),message);
    }
    public static void Native() {
        if(!OperatingSystem.IsWindows())return;
        var background=new Window{Width=900,Height=700,Position=new PixelPoint(80,80),Topmost=true,Background=Brushes.Black};
        var settings=new PreviewSettings{DesktopLocalContrast=true,DesktopBackgroundOpacity=0,DesktopTopmost=true,DesktopTextOpacity=70};
        var window=new FloatingMonitorWindow(new UiLanguage("en")){Position=new PixelPoint(160,160)};
        settings.DesktopOverlayOpacity=0;window.ApplyPreferences(settings);background.Show();window.Present(new("20%","4 GiB","—","—",true,true));window.Show();
        nint hwnd=window.TryGetPlatformHandle()!.Handle;
        TextBlock Reading()=>window.GetVisualDescendants().OfType<TextBlock>().First(x=>x.Text=="20%");
        Color Color()=>((ISolidColorBrush)Reading().Foreground!).Color;
        try {
            Until(()=>window.ContrastStatus=="Local contrast active"&&Color().R==245,"Dark background did not produce light text");
            Check(Color().A==255&&((Grid)window.GetVisualDescendants().OfType<Grid>().Single(x=>x.Name=="DesktopMetricCPU").Parent!).Opacity==.7,"Contrast must apply text opacity exactly once");
            var icon=window.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().First(x=>Equals(x.Tag,"cpu"));
            Avalonia.Media.Color Ink()=>((ISolidColorBrush)(icon.Stroke??icon.Fill)!).Color;
            var palette=Ink();Check(palette.R!=palette.G,"App icon palette was replaced");
            background.Background=Brushes.White;
            Until(()=>Color().R==20,"Bright background did not produce dark text");
            Check(Ink()==palette&&icon.Effect!=null,"App icon palette/protection was not preserved");
            settings.DesktopOverlayOpacity=86;window.ApplyPreferences(settings);
            Until(()=>Color().R==245,"Dark Desktop surface over white wallpaper did not retain readable light text");
            settings.DesktopOverlayOpacity=0;window.ApplyPreferences(settings);
            Until(()=>Color().R==20,"Transparent Desktop surface did not resume wallpaper contrast");
            settings.DesktopAppIconColors=false;window.ApplyPreferences(settings);
            Until(()=>Ink().R==20&&Ink().G==20,"Adaptive icon tint did not follow local contrast");
            settings.DesktopAppIconColors=true;window.ApplyPreferences(settings);
            Until(()=>Ink()==palette&&icon.Effect!=null,"App icon palette did not recover");
            window.Hide();Check(GetWindowDisplayAffinity(hwnd,out uint affinity)&&affinity==0,"Hide retained capture exclusion");
            window.Show();Until(()=>window.ContrastStatus=="Local contrast active","Show did not resume capture");
            window.BeginScreenshot();Check(GetWindowDisplayAffinity(hwnd,out affinity)&&affinity==0,"Screenshot mode did not restore visibility");
            Check(window.ContrastStatus.StartsWith("Screenshot mode"),"Missing screenshot status");
            var frozen=Color();background.Background=Brushes.Black;
            using(var slice=new CancellationTokenSource(400))Dispatcher.UIThread.MainLoop(slice.Token);
            Check(Color()==frozen,"Screenshot mode did not freeze the existing text color");
            Until(()=>window.ContrastStatus=="Local contrast active","Screenshot mode did not resume after timeout",18000);
            try {Until(()=>Color().R==245,"Screenshot recovery did not sample the changed background");}
            catch {
                var point=Reading().PointToScreen(new Point(Reading().Bounds.Width/2,Reading().Bounds.Height/2));
                nint screen=GetDC(0);uint pixel=GetPixel(screen,point.X,point.Y);ReleaseDC(0,screen);
                Console.WriteLine($"CONTRAST_RECOVERY status={window.ContrastStatus}; ink={Color()}; surface={window.ContrastBackground}; panel={window.Position}/{window.ClientSize}; fixture={background.Position}/{background.ClientSize}; visible={background.IsVisible}; background={background.Background}; sample={point}/{pixel:X8}");throw;
            }
            settings.DesktopLocalContrast=false;window.UpdateLocalContrast();
            Check(GetWindowDisplayAffinity(hwnd,out affinity)&&affinity==0&&window.ContrastStatus=="Local Contrast is off.","Disable did not stop capture");
            Check(Reading().Effect==null,"Disable retained contrast edge effects");
            settings.DesktopLocalContrast=true;window.UpdateLocalContrast();
            settings.DesktopLocalContrast=false;window.UpdateLocalContrast();
            Until(()=>window.ContrastStatus=="Local Contrast is off.","Late capture overwrote disabled state");
            window.Close();
        }finally{window.Close();background.Close();}
        Console.WriteLine("PASS Local Contrast: real dark/light background, text opacity, hide/resume, 15-second screenshot recovery and disable cleanup");
    }
}
