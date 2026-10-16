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
    static void Settle(){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    static void Invoke(Shell shell,string method,params object[] args){typeof(Shell).GetMethod(method,Hidden).Invoke(shell,args);}
    static void Language(Shell shell,string name){foreach(ComboBoxItem item in shell.Control<ComboBox>("LanguagePicker").Items)if((string)item.Tag==name)shell.Control<ComboBox>("LanguagePicker").SelectedItem=item;Pump();shell.Window.UpdateLayout();}
    static void Capture(Shell shell,string path){Pump();shell.Window.UpdateLayout();var bitmap=new RenderTargetBitmap((int)shell.Window.ActualWidth,(int)shell.Window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(shell.Window);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(path))png.Save(stream);}
    static DesktopMetric[] MoneyRows(Shell shell){return ((IEnumerable<DesktopMetric>)typeof(Shell).GetMethod("DesktopMetrics",Hidden).Invoke(shell,null)).Where(m=>m.Key.StartsWith("quotaDeepSeek",StringComparison.Ordinal)).ToArray();}
    static void Currency(ComboBox choice,string code){choice.SelectedItem=choice.Items.Cast<ComboBoxItem>().Single(item=>(string)item.Tag==code);Pump();}
    static void ProviderIcons(Shell shell,Settings settings,string state){
        settings.Data["desktopAppIconColors"]=true;settings.Data["unifiedReadingColors"]=false;
        var palette=typeof(Shell).GetMethod("DesktopPaletteColor",Hidden);var icon=typeof(Shell).GetMethod("Icon",Hidden);
        foreach(bool light in new[]{false,true}){
            settings.Data["background"]=light?"#F4F6F8":"#202831";Invoke(shell,"ApplyMaterial");
            var grid=new Grid{Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(light?"#F4F6F8":"#202831"))};
            foreach(var provider in QuotaCatalog.All)grid.ColumnDefinitions.Add(new ColumnDefinition());
            int column=0;
            foreach(var provider in QuotaCatalog.All){
                string color=(string)palette.Invoke(shell,new object[]{provider.Icon,"#CDD6DF"});
                Check(color==(light?"#17202B":QuotaCatalog.Accent(provider.Name)),provider.Name+" desktop palette follows the catalog/theme");
                var tile=new StackPanel{Margin=new Thickness(12)};Grid.SetColumn(tile,column++);grid.Children.Add(tile);
                tile.Children.Add(new TextBlock{Text=provider.Name,Foreground=light?Brushes.Black:Brushes.White,FontSize=14});
                foreach(double opacity in new[]{1d,.4d}){
                    var row=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,16,0,0),Opacity=opacity};tile.Children.Add(row);
                    foreach(double size in new[]{16d,24d,32d}){
                        var mark=(FrameworkElement)icon.Invoke(shell,new object[]{provider.Icon,size,color});mark.Margin=new Thickness(0,0,12,0);row.Children.Add(mark);
                    }
                }
            }
            var window=new Window{Content=grid,Width=640,Height=180,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,ShowActivated=false};
            try{window.Show();Settle();window.UpdateLayout();var bitmap=new RenderTargetBitmap(640,180,96,96,PixelFormats.Pbgra32);bitmap.Render(window);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(state,"provider-icons-"+(light?"light":"dark")+".png")))png.Save(stream);}
            finally{window.Close();}
        }
        settings.Data["background"]="#202831";Invoke(shell,"ApplyMaterial");
        settings.Data["desktopAppIconColors"]=false;Check((string)palette.Invoke(shell,new object[]{"deepseek","#CDD6DF"})=="#CDD6DF","icon palette opt-out preserves the text color");
        settings.Data["desktopAppIconColors"]=true;settings.Data["unifiedReadingColors"]=true;
        Check((string)palette.Invoke(shell,new object[]{"deepseek","#CDD6DF"})==(string)palette.Invoke(shell,new object[]{"claude","#CDD6DF"}),"unified icon colors remain consistent");
        settings.Data["unifiedReadingColors"]=false;settings.Data["background"]="#202831";Invoke(shell,"ApplyMaterial");
    }
    static void DesktopCapture(Shell shell,string path,bool light){
        using(var close=new DesktopFixture(shell)){
            var window=close.Window;window.Width=280;window.ShowActivated=false;window.Show();
            foreach(var size in new[]{new[]{280d,16d},new[]{320d,20d},new[]{466d,32d}}){
                window.Width=size[0];string tint=light?"#17202B":"#F0F5FA";window.Render(MoneyRows(shell),size[1],8,tint,true,1,name=>close.Palette(name,tint));Pump();window.UpdateLayout();
                var names=Tree(window).OfType<TextBlock>().Where(t=>t.Text=="DeepSeek"&&t.IsVisible).ToArray();
                Check(names.Length==MoneyRows(shell).Length&&names.All(t=>t.ActualHeight<size[1]*1.8),"desktop provider fits on one line at "+size[0]+"px / "+size[1]+"px font");
            }
            string textColor=light?"#17202B":"#F0F5FA";window.Width=280;window.Render(MoneyRows(shell),16,8,textColor,true,1,name=>close.Palette(name,textColor));window.SetTextOpacity(100,false,true,100);Settle();window.UpdateLayout();
            var paths=Tree(window).OfType<System.Windows.Shapes.Path>().ToArray();
            Check(paths.Length==MoneyRows(shell).Length&&paths.All(p=>p.Fill is SolidColorBrush&&((SolidColorBrush)p.Fill).Color.ToString()==(light?"#FF17202B":"#FF6E8FFF")),"actual desktop icon brushes use the provider palette, not a text-color fallback");
            foreach(var text in Tree(window).OfType<TextBlock>().Where(t=>t.IsVisible&&(t.Text=="DeepSeek"||t.Text.StartsWith("CNY ")||t.Text.StartsWith("USD ")))){
                var point=text.TransformToAncestor(window).Transform(new Point());Check(point.X>=0&&point.Y>=0&&point.X+text.ActualWidth<=window.ActualWidth+1&&point.Y+text.ActualHeight<=window.ActualHeight+1,"settled desktop label/value stays within the window");
            }
            var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);
            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(path))png.Save(stream);
        }
    }
    sealed class DesktopFixture:IDisposable {
        public readonly DesktopView Window;
        readonly Shell shell;
        public DesktopFixture(Shell shell){this.shell=shell;Window=new DesktopView((name,size,color)=>(FrameworkElement)typeof(Shell).GetMethod("Icon",Hidden).Invoke(shell,new object[]{name,size,color}),true);}
        public string Palette(string name,string textColor){return (string)typeof(Shell).GetMethod("DesktopIconColor",Hidden).Invoke(shell,new object[]{name,textColor});}
        public void Dispose(){Window.Close();}
    }
    static void CurrencyChecks(Shell shell,QuotaSession fake,Settings settings,string root,string state){
        var choice=shell.Control<ComboBox>("DeepSeekCurrency");Check(choice!=null,"DeepSeek has a persisted currency selector");if(choice==null)return;
        Check((string)((ComboBoxItem)choice.SelectedItem).Tag=="CNY","initial balance display defaults to CNY");
        var mode=shell.Control<ComboBox>("QuotaDisplay");mode.SelectedIndex=0;
        var reading=fake.Readings.Single();var scheduled=fake.GetState("DeepSeek").NextAttempt;
        Currency(choice,"USD");Check(MoneyRows(shell).Length==1&&MoneyRows(shell)[0].Value=="USD 0.00","USD selection shows a genuine zero without falling back to CNY");
        Check(Tree(shell.Control<StackPanel>("QuotaCards")).OfType<TextBlock>().Any(t=>t.Text=="USD 0.00")&&!Tree(shell.Control<StackPanel>("QuotaCards")).OfType<TextBlock>().Any(t=>t.Text.StartsWith("CNY ")),"currency filter applies immediately to the App");
        shell.Save();Check(new Settings(Path.Combine(state,"widget-settings.json")).Text("deepSeekCurrency")=="USD","currency selection survives a settings roundtrip");
        using(var restored=new Shell(new PulsePaths(root,state,Path.Combine(state,"runtime-restored")),true)){
            Check((string)((ComboBoxItem)restored.Control<ComboBox>("DeepSeekCurrency").SelectedItem).Tag=="USD","new window restores the saved currency selection");restored.Exit();
        }
        Currency(choice,"All");Check(MoneyRows(shell).Length==2,"All currencies retains both independent balances");
        mode.SelectedIndex=1;Check(Tree(shell.Control<StackPanel>("QuotaCards")).OfType<TextBlock>().Any(t=>t.Text=="CNY 10.00"),"full balance details remain available");
        Currency(choice,"USD");Check(!Tree(shell.Control<StackPanel>("QuotaCards")).OfType<TextBlock>().Any(t=>t.Text.StartsWith("CNY ")),"full balance details honor the currency filter");
        var usd=reading.Balances.Single(b=>b.Currency=="USD");var usdFull=reading.AllBalances.Where(b=>b.Currency=="USD").ToArray();
        reading.Balances.Remove(usd);reading.AllBalances.RemoveAll(b=>b.Currency=="USD");Invoke(shell,"RenderQuota");
        Check(MoneyRows(shell).Single().Value=="No USD Balance"&&Tree(shell.Control<StackPanel>("QuotaCards")).OfType<TextBlock>().Any(t=>t.Text=="No USD Balance"),"missing selected currency is explicit, never fabricated or replaced");
        reading.Observed=DateTimeOffset.UtcNow.AddMinutes(-11);Check(MoneyRows(shell).Single().Value=="Quota Stale","stale state is not hidden by a missing currency");reading.Observed=DateTimeOffset.UtcNow;
        reading.Balances.Add(usd);reading.AllBalances.AddRange(usdFull);
        settings.Data["deepSeekCurrency"]="invalid";Invoke(shell,"RenderQuota");Check(MoneyRows(shell).Single().Value=="CNY 110.00","unsupported saved currency uses the CNY default");
        Currency(choice,"All");Currency(choice,"CNY");mode.SelectedIndex=0;
        Check(MoneyRows(shell).Single().Title=="DeepSeek"&&MoneyRows(shell).Single().Value=="CNY 110.00","desktop stays concise after changing the currency");
        Check(fake.GetState("DeepSeek").NextAttempt==scheduled&&reading.Balances.Count==2,"display-only currency changes preserve source readings and request scheduling");
    }
    static void DesktopSettings(Shell shell,QuotaSession fake,Settings settings,string root,string state){
        var list=shell.Control<StackPanel>("DesktopOrderList");var row=list.Children.Cast<Border>().SingleOrDefault(r=>(string)r.Tag=="quotaDeepSeek");
        Check(row!=null,"Desktop reading settings include the DeepSeek group");if(row==null)return;
        var grid=(Grid)row.Child;var toggle=grid.Children.OfType<CheckBox>().Single();var handle=grid.Children.OfType<Thumb>().Single();
        Check(((TextBlock)grid.Children[1]).Text=="DeepSeek"&&toggle.IsChecked==true,"DeepSeek has a concise labeled visibility toggle and reorder handle");
        var currency=shell.Control<ComboBox>("DeepSeekCurrency");Currency(currency,"All");int cardCount=shell.Control<StackPanel>("QuotaCards").Children.Count;
        bool enabled=settings.Flag("quotaDeepSeek");var scheduled=fake.GetState("DeepSeek").NextAttempt;
        settings.Data["desktopEnabled"]=true;Invoke(shell,"UpdateDesktop");
        var desktop=(DesktopView)typeof(Shell).GetField("desktop",Hidden).GetValue(shell);
        Check(MoneyRows(shell).Length==2&&Tree(desktop).OfType<TextBlock>().Count(t=>t.Text=="DeepSeek"&&t.IsVisible)==2,"All currencies are shown by the DeepSeek desktop group");
        toggle.IsChecked=false;toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Settle();
        Check(MoneyRows(shell).Length==0&&!Tree(desktop).OfType<TextBlock>().Any(t=>t.Text=="DeepSeek"&&t.IsVisible),"DeepSeek desktop toggle hides both real desktop rows");
        Check(shell.Control<StackPanel>("QuotaCards").Children.Count==cardCount&&fake.Readings.Length==1&&settings.Flag("quotaDeepSeek")==enabled,"desktop visibility never disables monitoring or App cards");
        fake.Readings[0].Observed=DateTimeOffset.UtcNow.AddMinutes(-11);Invoke(shell,"UpdateDesktop");Check(MoneyRows(shell).Length==0,"stale status cannot resurrect a hidden DeepSeek group");fake.Readings[0].Observed=DateTimeOffset.UtcNow;
        shell.ShowSettings(true);Invoke(shell,"SelectSettingsCategory","Desktop");
        for(int move=0;move<list.Children.Count&&list.Children.IndexOf(row)>0;move++){var key=new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,PresentationSource.FromVisual(shell.Window),0,System.Windows.Input.Key.Up){RoutedEvent=System.Windows.Input.Keyboard.PreviewKeyDownEvent};handle.RaiseEvent(key);}
        Check((string)list.Children[0].GetValue(FrameworkElement.TagProperty)=="quotaDeepSeek","DeepSeek uses the real keyboard/drag reorder save path");
        shell.Save();
        using(var restored=new Shell(new PulsePaths(root,state,Path.Combine(state,"runtime-desktop-restored")),true)){
            var restoredList=restored.Control<StackPanel>("DesktopOrderList");var first=(Border)restoredList.Children[0];
            Check((string)first.Tag=="quotaDeepSeek"&&((Grid)first.Child).Children.OfType<CheckBox>().Single().IsChecked==false,"DeepSeek order and hidden preference survive a new window");restored.Exit();
        }
        toggle.IsChecked=true;toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        foreach(string code in new[]{"USD","CNY","All"}){
            Currency(currency,code);Check(MoneyRows(shell).Length==(code=="All"?2:1)&&((IEnumerable<DesktopMetric>)typeof(Shell).GetMethod("DesktopMetrics",Hidden).Invoke(shell,null)).First().Key.StartsWith("quotaDeepSeek",StringComparison.Ordinal),"currency changes preserve the group's selected position: "+code);
        }
        Check(fake.GetState("DeepSeek").NextAttempt==scheduled,"desktop preference changes preserve request scheduling");
        Invoke(shell,"SetDesktopEnabled",false);Currency(currency,"CNY");
        foreach(string locale in new[]{"en","zh-CN"})foreach(bool light in new[]{false,true}){
            Language(shell,locale);settings.Data["background"]=light?"#F4F6F8":"#202831";Invoke(shell,"ApplyMaterial");shell.Window.Width=360;shell.Window.Height=740;
            shell.ShowSettings(true);Invoke(shell,"SelectSettingsCategory","Desktop");row.BringIntoView();Settle();Capture(shell,Path.Combine(state,"desktop-settings-"+locale+(light?"-light":"-dark")+".png"));
            Check((string)toggle.ToolTip==(locale=="en"?"Show On Desktop":"在桌面显示"),"DeepSeek visibility tooltip follows the UI language");
        }
        Language(shell,"en");shell.ShowSettings(false);
    }
    [STAThread] static int Main(string[] args){
        try{
            string root=AppDomain.CurrentDomain.BaseDirectory,state=Path.GetFullPath(args[0]);Directory.CreateDirectory(state);
            Json.WriteAtomic(Path.Combine(state,"widget-settings.json"),new {language="en",solid=true});
            var application=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            NativeLocalInferenceTests.Run(root,Path.Combine(state,"local-inference"));
            NativeSnapshotRecoveryTests.Run(root,Path.Combine(state,"snapshot-recovery"));
            NativeArgonTests.Run(root,Path.Combine(state,"argon"));
            using(var shell=new Shell(new PulsePaths(root,state,Path.Combine(state,"runtime")),true)){
                shell.Window.ShowActivated=false;shell.Window.ShowInTaskbar=false;shell.Show();Pump();
                NativeQuotaTests.RunUI(shell,Path.Combine(state,"quota-existing-tests.png"));
                Console.WriteLine("PASS existing native quota UI regression including money rows, percent bars and actual settings file");
                Check((string)shell.Control<Button>("DeepSeekKeySave").Content=="Save Encrypted","English save button uses Title Case");
                Check((string)shell.Control<Button>("DeepSeekKeyRemove").Content=="Remove Key","English remove button uses Title Case");
                Check(((ComboBoxItem)shell.Control<ComboBox>("QuotaDisplay").Items[1]).Content as string=="All Available Quotas","English quota choice uses Title Case");
                var field=typeof(Shell).GetField("quotas",Hidden);var original=(QuotaSession)field.GetValue(shell);
                var settings=(Settings)typeof(Shell).GetField("settings",Hidden).GetValue(shell);
                ProviderIcons(shell,settings,state);
                var labels=new Languages(Path.Combine(root,"Languages.txt")){Preference="en"};
                Check(labels.Label("DeepSeek API key")=="DeepSeek API Key"&&labels.Label("5-hour")=="5-Hour","Title Case preserves brands and acronyms");
                Check(labels.Label("KB/s")=="KB/s"&&labels.Label("api.deepseek.com")=="api.deepseek.com"&&labels.Label("My custom device")=="My custom device","labels never rewrite units, endpoints or unknown device names");
                Check(labels.Label("Changes save automatically.")=="Changes save automatically.","explanatory sentences retain sentence case");
                using(var fake=new QuotaSession((provider,cancel)=>QuotaDecoder.Decode("DeepSeek",QuotaData.Parse("{\"balance_infos\":[{\"currency\":\"CNY\",\"total_balance\":\"110.00\",\"granted_balance\":\"10.00\",\"topped_up_balance\":\"100.00\"},{\"currency\":\"USD\",\"total_balance\":\"0.00\"}]}"),DateTimeOffset.UtcNow))){
                    field.SetValue(shell,fake);
                    try{
                        fake.Enable("DeepSeek",true);fake.Tick(DateTimeOffset.UtcNow);for(int i=0;i<200&&fake.GetState("DeepSeek").Refreshing;i++){Thread.Sleep(5);fake.Tick(DateTimeOffset.UtcNow);}
                        Invoke(shell,"RenderQuota");CurrencyChecks(shell,fake,settings,root,state);
                        DesktopSettings(shell,fake,settings,root,state);
                        foreach(string locale in new[]{"en","zh-CN"})foreach(bool light in new[]{false,true}){
                            Language(shell,locale);settings.Data["background"]=light?"#F4F6F8":"#202831";Invoke(shell,"ApplyMaterial");
                            var currency=shell.Control<ComboBox>("DeepSeekCurrency");
                            Check((string)((ComboBoxItem)currency.Items[0]).Content==(locale=="en"?"CNY (RMB)":"人民币（CNY）"),"currency selector follows the UI language");
                            DesktopCapture(shell,Path.Combine(state,"desktop-"+locale+(light?"-light":"-dark")+"-CNY.png"),light);
                            Currency(currency,"All");DesktopCapture(shell,Path.Combine(state,"desktop-"+locale+(light?"-light":"-dark")+"-all.png"),light);Currency(currency,"CNY");
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
                            shell.Control<Button>("DeepSeekKeyRemove").BringIntoView();Settle();Capture(shell,Path.Combine(state,"settings-actions-"+locale+(light?"-light":"-dark")+".png"));
                            shell.Control<Button>("DeepSeekKeySave").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                            Language(shell,locale=="en"?"zh-CN":"en");
                            Check(shell.Control<TextBlock>("DeepSeekKeyStatus").Text==(locale=="en"?"请输入有效的 API 密钥，原密钥未更改。":"Enter a valid API key. The previous key was not changed."),"credential status follows language change");
                            Language(shell,locale);shell.ShowSettings(false);shell.Control<StackPanel>("Cards").Visibility=Visibility.Collapsed;
                            shell.Window.Width=360;shell.Window.Height=600;Invoke(shell,"RenderQuota");Pump();
                            var card=shell.Control<StackPanel>("QuotaCards").Children.Cast<Border>().Single();
                            Check(Tree(card).OfType<TextBlock>().Any(t=>t.Text=="CNY 110.00"),"localized money card retains currency and amount");
                            Check(Tree(card).OfType<TextBlock>().Any(t=>t.Text==(locale=="en"?"Balance":"余额")),"balance caption is concise in English and Chinese");
                            if(locale=="en")Check(Tree(card).OfType<TextBlock>().Any(t=>System.Text.RegularExpressions.Regex.IsMatch(t.Text??"",@"^Live · \d+ Min Ago$")),"DeepSeek freshness words use Title Case");
                            Check(!Tree(card).OfType<ProgressBar>().Any(),"money card has no percent bar");
                            // Equal timestamp/status must still re-render changed money content.
                            fake.Readings[0].Balances[0].Amount="120.00";Invoke(shell,"RenderQuota");
                            Check(Tree(shell.Control<StackPanel>("QuotaCards")).OfType<TextBlock>().Any(t=>t.Text=="CNY 120.00"),"money changes invalidate render signature");
                            fake.Readings[0].Balances[0].Amount="110.00";Invoke(shell,"RenderQuota");
                            Capture(shell,Path.Combine(state,"balance-"+locale+(light?"-light":"-dark")+".png"));
                            shell.Control<ComboBox>("QuotaDisplay").SelectedIndex=1;Settle();
                            Check(Tree(shell.Control<StackPanel>("QuotaCards")).OfType<TextBlock>().Any(t=>t.Text==(locale=="en"?"Topped Up":"充值")),"full balance details use localized Title Case captions");
                            Capture(shell,Path.Combine(state,"balance-full-"+locale+(light?"-light":"-dark")+".png"));shell.Control<ComboBox>("QuotaDisplay").SelectedIndex=0;
                        }
                    }finally{field.SetValue(shell,original);}
                }
                shell.Exit();
            }
            application.Shutdown();
            foreach(string failure in failures.Distinct())Console.WriteLine("FAIL "+failure);
            Console.WriteLine("Evidence: "+state);
            if(failures.Count>0)return 1;
            Console.WriteLine("PASS English/Chinese WPF: dark/light, 280px settings, concise desktop rows at 16/20/32px, Title Case, persisted CNY/USD/All filter, zero/missing/stale balances and money invalidation");
            return 0;
        }catch(Exception e){Console.WriteLine(e);return 1;}
    }
}
