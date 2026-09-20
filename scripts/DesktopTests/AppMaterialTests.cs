using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class AppMaterialTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run() {
        CheckDefaultTextContrast();
        string directory=Directory.CreateTempSubdirectory("pulse-app-material-").FullName;
        try {
            string path=Path.Combine(directory,"settings.json");
            File.WriteAllText(path,"{}");
            Check(new PreviewSettingsStore(path).Load().AppOpacity==new PreviewSettings().AppOpacity,"Missing opacity uses the readable new-profile default");
            File.WriteAllText(path,"{\"schema\":1,\"appOpacity\":35,\"solid\":false,\"future\":17}");
            var window=new MonitorWindow(new MonitorSource(true),start:false,store:new PreviewSettingsStore(path));
            window.Show();Dispatcher.UIThread.RunJobs();
            var main=window.GetVisualDescendants().OfType<TabControl>().Single(x=>x.Name=="MainTabs");
            main.SelectedIndex=1;Dispatcher.UIThread.RunJobs();
            var groups=window.GetVisualDescendants().OfType<TabControl>().Single(x=>x.Name=="SettingsTabs");
            groups.SelectedIndex=1;Dispatcher.UIThread.RunJobs();
            var slider=window.GetVisualDescendants().OfType<Slider>().Single(x=>x.Name=="AppOpacity");
            var solid=window.GetVisualDescendants().OfType<CheckBox>().Single(x=>x.Name=="AppSolid");
            var surface=window.GetVisualDescendants().OfType<Border>().Single(x=>x.Name=="SettingsSurface");
            Check(slider.Value==35,"Restore saved App opacity independently from theme");
            solid.IsChecked=true;
            Check(((ISolidColorBrush)window.Background!).Color.A==255&&!slider.IsEnabled,"Solid provides opaque background and disables opacity adjustment");
            Check(slider.Value==35&&window.Opacity==1,"Solid fallback preserves preference and does not fade text");
            solid.IsChecked=false;slider.Value=0;
            Check(((ISolidColorBrush)surface.Background!).Color.A==255,"Settings remain opaque while monitoring opacity is zero");
            bool unsupported=window.ActualTransparencyLevel==WindowTransparencyLevel.None||
                (OperatingSystem.IsWindows()&&window.TryGetPlatformHandle()?.HandleDescriptor!="HWND");
            byte expected=unsupported?(byte)255:(byte)0;
            Check(((ISolidColorBrush)window.Background!).Color.A==expected,"Zero opacity is transparent only when platform supports it");
            Check(window.Opacity==1&&((ISolidColorBrush)window.Foreground!).Color.A==255,"Background opacity does not fade foreground; visual contrast requires native acceptance");
            slider.Value=100;
            Check(((ISolidColorBrush)window.Background!).Color.A==255,"Full opacity is opaque on every platform");
            slider.Value=35;solid.IsChecked=true;window.Close();
            var saved=new PreviewSettingsStore(path).Load();
            Check(saved.AppOpacity==35&&saved.Solid,"Opacity and solid preference survive close");
            using(var json=System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)))
                Check(json.RootElement.GetProperty("future").GetInt32()==17,"Material save preserves unknown fields");
            foreach(var value in new[]{-1,101}) {
                File.WriteAllText(path,"{\"appOpacity\":"+value+"}");
                Check(new PreviewSettingsStore(path).Load().AppOpacity==Math.Clamp(value,0,100),"Out-of-range opacity normalized");
            }
            Console.WriteLine("PASS App material: solid/unsupported fallback, foreground, zero/full opacity and profile roundtrip");
        } finally {Directory.Delete(directory,true);}
    }
    static void CheckDefaultTextContrast() {
        // Model the brightest possible content behind the glass. Headless mode
        // falls back to opaque, so explicitly composite the saved default alpha.
        // This palette regression supplements, not replaces, native visual checks.
        var window=new MonitorWindow(new MonitorSource(true),start:false);
        window.Show();Dispatcher.UIThread.RunJobs();
        try {
            var tint=((ISolidColorBrush)window.Background!).Color;
            double opacity=new PreviewSettings().AppOpacity/100;
            var gradient=(RadialGradientBrush)window.GetVisualDescendants().OfType<DockPanel>().Single(x=>x.Name=="Viewport").Background!;
            var foregrounds=window.GetVisualDescendants().OfType<TextBlock>().Where(x=>x.Name=="DeviceSubtitle")
                .Select(x=>((ISolidColorBrush)x.Foreground!).Color).Append(((ISolidColorBrush)window.Foreground!).Color);
            double minimum=double.MaxValue;
            foreach(var behind in new[]{Colors.White,Colors.Black})foreach(var stop in gradient.GradientStops) {
                var background=Composite(stop.Color,Composite(tint,behind,opacity),stop.Color.A/255d*opacity);
                foreach(var foreground in foregrounds) {
                    double a=Luminance(foreground),b=Luminance(background);
                    minimum=Math.Min(minimum,(Math.Max(a,b)+.05)/(Math.Min(a,b)+.05));
                }
            }
            Check(minimum>=4.5,$"Default main/secondary text contrast over bright/dark glass: {minimum:F2}:1; requires 4.5:1");
            Console.WriteLine($"PASS default main/secondary text palette contrast: {minimum:F2}:1 minimum over black/white backgrounds");
        }finally {window.Close();}
    }
    static Color Composite(Color front,Color back,double alpha)=>Color.FromRgb(
        (byte)Math.Round(front.R*alpha+back.R*(1-alpha)),(byte)Math.Round(front.G*alpha+back.G*(1-alpha)),(byte)Math.Round(front.B*alpha+back.B*(1-alpha)));
    static double Luminance(Color color) {
        double Linear(byte value){double x=value/255d;return x<=.04045?x/12.92:Math.Pow((x+.055)/1.055,2.4);}
        return .2126*Linear(color.R)+.7152*Linear(color.G)+.0722*Linear(color.B);
    }
}
