using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HardwarePulse;

// Isolated WPF acceptance: no collector, login, HTTP or credential writes.
static class QuotaFeatureTests {
    const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    static readonly List<string> failures=new List<string>();
    static void Check(bool value,string name){if(!value)failures.Add(name);}
    static IEnumerable<DependencyObject> Tree(DependencyObject node){yield return node;foreach(object child in LogicalTreeHelper.GetChildren(node)){var d=child as DependencyObject;if(d!=null)foreach(var item in Tree(d))yield return item;}}
    static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
    static void Invoke(Shell shell,string method,params object[] args){typeof(Shell).GetMethod(method,Hidden).Invoke(shell,args);}
    static void Language(Shell shell,string name){foreach(ComboBoxItem item in shell.Control<ComboBox>("LanguagePicker").Items)if((string)item.Tag==name)shell.Control<ComboBox>("LanguagePicker").SelectedItem=item;Pump();shell.Window.UpdateLayout();}
    static void Capture(Shell shell,string path){Pump();shell.Window.UpdateLayout();var bitmap=new RenderTargetBitmap((int)shell.Window.ActualWidth,(int)shell.Window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(shell.Window);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(path))png.Save(stream);}
    [STAThread] static int Main(string[] args){
        try{
            string root=AppDomain.CurrentDomain.BaseDirectory,state=Path.GetFullPath(args[0]);Directory.CreateDirectory(state);
            Json.WriteAtomic(Path.Combine(state,"widget-settings.json"),new {language="en",solid=true});
            var application=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            using(var shell=new Shell(new PulsePaths(root,state,Path.Combine(state,"runtime")),true)){
                shell.Window.ShowActivated=false;shell.Window.ShowInTaskbar=false;shell.Show();Pump();
                NativeQuotaTests.RunUI(shell,Path.Combine(state,"quota-existing-tests.png"));
                Console.WriteLine("PASS existing native quota UI regression including money rows, percent bars and actual settings file");
                var field=typeof(Shell).GetField("quotas",Hidden);var original=(QuotaSession)field.GetValue(shell);
                var settings=(Settings)typeof(Shell).GetField("settings",Hidden).GetValue(shell);
                using(var fake=new QuotaSession((provider,cancel)=>QuotaDecoder.Decode("DeepSeek",QuotaData.Parse("{\"balance_infos\":[{\"currency\":\"CNY\",\"total_balance\":\"110.00\",\"granted_balance\":\"10.00\",\"topped_up_balance\":\"100.00\"}]}"),DateTimeOffset.UtcNow))){
                    field.SetValue(shell,fake);
                    try{
                        fake.Enable("DeepSeek",true);fake.Tick(DateTimeOffset.UtcNow);for(int i=0;i<200&&fake.GetState("DeepSeek").Refreshing;i++){Thread.Sleep(5);fake.Tick(DateTimeOffset.UtcNow);}
                        foreach(string locale in new[]{"en","zh-CN"})foreach(bool light in new[]{false,true}){
                            Language(shell,locale);settings.Data["background"]=light?"#F4F6F8":"#202831";Invoke(shell,"ApplyMaterial");
                            shell.Window.Width=280;shell.Window.Height=820;shell.Window.FontSize=16;
                            shell.ShowSettings(true);Invoke(shell,"SelectSettingsCategory","AI Quota");Pump();shell.Window.UpdateLayout();
                            var description=Tree(shell.Control<ResponsivePanel>("SettingsSections")).OfType<TextBlock>().Where(t=>t.IsVisible).Select(t=>t.Text).ToArray();
                            Check(description.Any(t=>locale=="en"?t.Contains("DeepSeek uses the API key"):t.Contains("DeepSeek 使用下方输入")),locale+" localized quota description");
                            Check(!description.Any(t=>locale=="zh-CN"&&(t.Contains("精簡")||t.Contains("拖動")||t.Contains("儲存"))),"Simplified Chinese has no Traditional labels");
                            foreach(string name in new[]{"DeepSeekKeySave","DeepSeekKeyRemove","DeepSeekKey"}){
                                var control=shell.Control<FrameworkElement>(name);var tools=shell.Control<StackPanel>("DeepSeekKeyTools");var point=control.TransformToAncestor(tools).Transform(new Point());
                                Check(point.X>=0&&point.X+control.ActualWidth<=tools.ActualWidth+1,locale+" narrow control bounds "+name);
                            }
                            var box=shell.Control<PasswordBox>("DeepSeekKey");box.Password="bad key";
                            Check(box.MinHeight>=32,"password box has a usable input height");
                            box.BringIntoView();Pump();
                            Capture(shell,Path.Combine(state,"settings-"+locale+(light?"-light":"-dark")+".png"));
                            shell.Control<Button>("DeepSeekKeySave").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                            Language(shell,locale=="en"?"zh-CN":"en");
                            Check(shell.Control<TextBlock>("DeepSeekKeyStatus").Text==(locale=="en"?"请输入有效的 API 密钥，原密钥未更改。":"Enter a valid API key. The previous key was not changed."),"credential status follows language change");
                            Language(shell,locale);shell.ShowSettings(false);shell.Control<StackPanel>("Cards").Visibility=Visibility.Collapsed;
                            shell.Window.Width=360;shell.Window.Height=600;Invoke(shell,"RenderQuota");Pump();
                            var card=shell.Control<StackPanel>("QuotaCards").Children.Cast<Border>().Single();
                            Check(Tree(card).OfType<TextBlock>().Any(t=>t.Text=="CNY 110.00"),"localized money card retains currency and amount");
                            Check(!Tree(card).OfType<ProgressBar>().Any(),"money card has no percent bar");
                            // Equal timestamp/status must still re-render changed money content.
                            fake.Readings[0].Balances[0].Amount="120.00";Invoke(shell,"RenderQuota");
                            Check(Tree(shell.Control<StackPanel>("QuotaCards")).OfType<TextBlock>().Any(t=>t.Text=="CNY 120.00"),"money changes invalidate render signature");
                            fake.Readings[0].Balances[0].Amount="110.00";Invoke(shell,"RenderQuota");
                            Capture(shell,Path.Combine(state,"balance-"+locale+(light?"-light":"-dark")+".png"));
                        }
                    }finally{field.SetValue(shell,original);}
                }
                shell.Exit();
            }
            application.Shutdown();
            foreach(string failure in failures.Distinct())Console.WriteLine("FAIL "+failure);
            if(failures.Count>0)return 1;
            Console.WriteLine("PASS English/Chinese WPF: dark/light, narrow 280px at 16px font, localized feedback and money invalidation");
            Console.WriteLine("Evidence: "+state);return 0;
        }catch(Exception e){Console.WriteLine(e);return 1;}
    }
}
