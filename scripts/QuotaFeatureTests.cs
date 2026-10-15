using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HardwarePulse;

public static class QuotaFeatureTests {
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
    static void Until(Func<bool> condition){var end=DateTime.UtcNow.AddSeconds(5);while(!condition()&&DateTime.UtcNow<end){Pump();System.Threading.Thread.Sleep(10);}Check(condition(),"UI operation timed out");}
    static void Capture(Shell shell,string path){for(int i=0;i<25;i++){Pump();System.Threading.Thread.Sleep(10);}shell.Window.UpdateLayout();var bitmap=new RenderTargetBitmap((int)shell.Window.ActualWidth,(int)shell.Window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(shell.Window);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))png.Save(file);}
    [STAThread] public static int Main(string[] args){
        try{
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            string root=AppDomain.CurrentDomain.BaseDirectory,state=args[0];Directory.CreateDirectory(state);
            var paths=new PulsePaths(root,state,Path.Combine(state,"runtime"));
            using(var shell=new Shell(paths,true)){
                shell.Window.ShowInTaskbar=false;shell.Show();Pump();
                foreach(ComboBoxItem item in shell.Control<ComboBox>("LanguagePicker").Items)if((string)item.Tag=="en")shell.Control<ComboBox>("LanguagePicker").SelectedItem=item;
                NativeQuotaTests.RunUI(shell,Path.Combine(state,"quota-money-and-percent.png"));
                shell.ShowSettings(true);typeof(Shell).GetMethod("SelectSettingsCategory",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(shell,new object[]{"AI Quota"});
                var input=shell.Control<PasswordBox>("DeepSeekApiKey");var save=shell.Control<Button>("SaveDeepSeekKey");var clear=shell.Control<Button>("ClearDeepSeekKey");
                Check(input!=null&&input.Password=="","masked input must not expose existing key");
                Check(shell.Control<CheckBox>("QuotaDeepSeek").IsChecked!=true,"saving credentials must not auto-enable monitoring before save");
                input.Password="fixture-key-ui-not-a-real-key";save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Until(()=>save.IsEnabled);
                Check(input.Password==""&&new ProtectedQuotaCredentials(state).Read("DeepSeek")=="fixture-key-ui-not-a-real-key","masked save and encrypted round trip");
                shell.Save();Check(!File.ReadAllText(Path.Combine(state,"widget-settings.json")).Contains("fixture-key-ui"),"key leaked into ordinary settings");
                shell.Window.Width=340;shell.Window.Height=900;Capture(shell,Path.Combine(state,"quota-settings-en-narrow.png"));
                foreach(ComboBoxItem item in shell.Control<ComboBox>("LanguagePicker").Items)if((string)item.Tag=="zh-CN")shell.Control<ComboBox>("LanguagePicker").SelectedItem=item;
                Capture(shell,Path.Combine(state,"quota-settings-zh-narrow.png"));
                input.Password="invalid key with spaces";save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
                Check(new ProtectedQuotaCredentials(state).Read("DeepSeek")=="fixture-key-ui-not-a-real-key","invalid save must retain previous key");
                clear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(!new ProtectedQuotaCredentials(state).Exists("DeepSeek"),"clear must remove encrypted credential");
                Check(shell.Control<CheckBox>("QuotaDeepSeek").IsChecked!=true,"saving credentials must not enable network access");
                shell.Exit();
            }
            app.Shutdown();Console.WriteLine("PASS quota feature WPF: registry toggles, separate money/percent cards, SVG, masked encrypted save/clear, no settings leak, explicit opt-in, narrow bilingual rendering");Console.WriteLine("Evidence: "+state);return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
