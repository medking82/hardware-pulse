using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class LocalContrastTests {
    [DllImport("user32.dll")] static extern bool GetWindowDisplayAffinity(nint window,out uint affinity);
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
    public static void Settings() {
        string directory=Directory.CreateTempSubdirectory("pulse-contrast-settings-").FullName;
        try {
            var store=new PreviewSettingsStore(System.IO.Path.Combine(directory,"settings.json"));
            var settings=store.Load();Check(!settings.FloatingLocalContrast,"Contrast must default off");
            settings.FloatingLocalContrast=true;Check(store.Save(settings),"Could not save contrast setting");
            Check(new PreviewSettingsStore(System.IO.Path.Combine(directory,"settings.json")).Load().FloatingLocalContrast,"Contrast setting did not persist");
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
        var settings=new PreviewSettings{FloatingLocalContrast=true,FloatingBackgroundOpacity=0,FloatingTopmost=true,FloatingTextOpacity=70};
        var window=new FloatingMonitorWindow(new UiLanguage("en"),settings){Position=new PixelPoint(160,160)};
        background.Show();window.Present(new("20%","4 GiB","—","—",true,true));window.Show();
        nint hwnd=window.TryGetPlatformHandle()!.Handle;
        TextBlock Reading()=>window.GetVisualDescendants().OfType<TextBlock>().First(x=>x.Text=="20%");
        Color Color()=>((ISolidColorBrush)Reading().Foreground!).Color;
        try {
            Until(()=>window.ContrastStatus=="Local contrast active"&&Color().R==245,"Dark background did not produce light text");
            Check(Color().A==(byte)Math.Round(70*2.55),"Contrast ignored text opacity");
            var icon=window.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().First(x=>Equals(x.Tag,"cpu"));
            Avalonia.Media.Color Ink()=>((ISolidColorBrush)(icon.Stroke??icon.Fill)!).Color;
            var palette=Ink();Check(palette.R!=palette.G,"App icon palette was replaced");
            background.Background=Brushes.White;
            Until(()=>Color().R==20,"Bright background did not produce dark text");
            Check(Ink()==palette&&icon.Effect!=null,"App icon palette/protection was not preserved");
            settings.FloatingIconsFollowApp=false;window.ApplyTextAppearance();
            Until(()=>Ink().R==20&&Ink().G==20,"Adaptive icon tint did not follow local contrast");
            settings.FloatingIconsFollowApp=true;window.ApplyTextAppearance();
            Until(()=>Ink()==palette&&icon.Effect!=null,"App icon palette did not recover");
            window.Hide();Check(GetWindowDisplayAffinity(hwnd,out uint affinity)&&affinity==0,"Hide retained capture exclusion");
            window.Show();Until(()=>window.ContrastStatus=="Local contrast active","Show did not resume capture");
            window.BeginScreenshot();Check(GetWindowDisplayAffinity(hwnd,out affinity)&&affinity==0,"Screenshot mode did not restore visibility");
            Check(window.ContrastStatus.StartsWith("Screenshot mode"),"Missing screenshot status");
            Until(()=>window.ContrastStatus=="Local contrast active","Screenshot mode did not resume after timeout",18000);
            settings.FloatingLocalContrast=false;window.UpdateLocalContrast();
            Check(GetWindowDisplayAffinity(hwnd,out affinity)&&affinity==0&&window.ContrastStatus=="Local Contrast is off.","Disable did not stop capture");
            Check(Reading().Effect==null,"Disable retained contrast edge effects");
            settings.FloatingLocalContrast=true;window.UpdateLocalContrast();
            settings.FloatingLocalContrast=false;window.UpdateLocalContrast();
            Until(()=>window.ContrastStatus=="Local Contrast is off.","Late capture overwrote disabled state");
            window.Close();
        }finally{window.Close();background.Close();}
        Console.WriteLine("PASS Local Contrast: real dark/light background, text opacity, hide/resume, 15-second screenshot recovery and disable cleanup");
    }
}
