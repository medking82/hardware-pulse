using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class AppMaterialTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run() {
        string directory=Directory.CreateTempSubdirectory("pulse-app-material-").FullName;
        try {
            string path=Path.Combine(directory,"settings.json");
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
            byte expected=window.ActualTransparencyLevel==WindowTransparencyLevel.None?(byte)255:(byte)0;
            Check(((ISolidColorBrush)window.Background!).Color.A==expected,"Zero opacity is transparent only when platform supports it");
            Check(window.Opacity==1&&((ISolidColorBrush)window.Foreground!).Color.A==255,"Foreground stays fully readable at zero background opacity");
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
}
