using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using HardwarePulse;
internal static class NativeJevTests {
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool ok,string message){if(!ok)throw new Exception("Jev UI: "+message);}
    static IEnumerable<DependencyObject> Tree(DependencyObject node){yield return node;foreach(object child in LogicalTreeHelper.GetChildren(node)){var d=child as DependencyObject;if(d!=null)foreach(var value in Tree(d))yield return value;}}
    static void Call(Shell shell,string name,params object[] args){typeof(Shell).GetMethod(name,Hidden).Invoke(shell,args);}
    public static void Run(string root,string state){
        Directory.CreateDirectory(state);Json.WriteAtomic(Path.Combine(state,"widget-settings.json"),new{language="en",solid=true});
        using(var shell=new Shell(new PulsePaths(root,state,Path.Combine(state,"runtime")),true))
        using(var fake=new QuotaSession((provider,cancel)=>provider=="Jev"?JevBalance.Decode(new Dictionary<string,object>{{"ok",true},{"data",new Dictionary<string,object>{{"balanceCents",1000}}}},DateTimeOffset.UtcNow):new QuotaReading{Provider=provider,Status="Live",Observed=DateTimeOffset.UtcNow,Windows=new List<QuotaWindow>{new QuotaWindow{Label="Weekly",Remaining=64}}})){
            var field=typeof(Shell).GetField("quotas",Hidden);var original=(QuotaSession)field.GetValue(shell);field.SetValue(shell,fake);
            try{
                var cookie=shell.Control<PasswordBox>("JevCookie");var agent=shell.Control<TextBox>("JevUserAgent");
                Check(cookie!=null&&cookie.MaxLength==32768&&agent!=null,"bounded masked Cookie and optional public UA are real controls");
                Check(shell.Control<Button>("JevOpenBrowser")!=null&&shell.Control<Expander>("JevGuide")!=null,"fixed-site one-click launch and built-in guide are present");
                Check(!shell.Control<Expander>("JevBrowserDetails").IsExpanded&&(JevQuota.InstalledBrowser()==null?agent.Text=="":agent.Text.Contains("Chrome/")),"browser metadata is automatic when available and advanced details are folded");
                bool stored=false;int saves=0,removes=0;string saved=null;
                typeof(Shell).GetField("jevHasSession",Hidden).SetValue(shell,new Func<bool>(()=>stored));
                typeof(Shell).GetField("jevSaveSession",Hidden).SetValue(shell,new Action<string>(value=>{saved=value;stored=true;saves++;}));
                typeof(Shell).GetField("jevRemoveSession",Hidden).SetValue(shell,new Action(()=>{stored=false;removes++;}));
                shell.Control<CheckBox>("QuotaJev").IsChecked=true;shell.Control<CheckBox>("QuotaJev").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Check(shell.Control<CheckBox>("QuotaJev").IsChecked==false&&fake.Readings.Length==0,"missing session opens guidance without scheduling an unauthenticated request");
                cookie.Password="Cookie: session=fixture";shell.Control<Button>("JevSessionSave").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Check(cookie.Password==""&&saves==0,"invalid input clears the field and does not touch the stored session");
                cookie.Password="session=fixture-cookie; organization_id=fixture-org";agent.Text="fixture-browser";
                shell.Control<Button>("JevSessionSave").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Check(saves==1&&JevSession.Decode(saved)!=null&&cookie.Password==""&&fake.Readings.Length==0,"valid save is routed once, masked input clears, and monitoring remains opt-in");
                shell.Control<CheckBox>("QuotaJev").IsChecked=true;shell.Control<CheckBox>("QuotaJev").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                var now=DateTimeOffset.UtcNow;fake.Tick(now);for(int i=0;i<200&&fake.GetState("Jev").Refreshing;i++){Thread.Sleep(5);fake.Tick(now);}Call(shell,"RenderQuota");
                var cards=shell.Control<StackPanel>("QuotaCards");Check(Tree(cards).OfType<TextBlock>().Any(t=>t.Text=="USD 10.00")&&!Tree(cards).OfType<ProgressBar>().Any(),"Jev card shows USD money, never a percentage");
                var settings=(Settings)typeof(Shell).GetField("settings",Hidden).GetValue(shell);settings.Data["deepSeekCurrency"]="CNY";
                var metrics=((IEnumerable<DesktopMetric>)typeof(Shell).GetMethod("DesktopMetrics",Hidden).Invoke(shell,null)).Where(m=>m.Key.StartsWith("quotaJev",StringComparison.Ordinal)).ToArray();
                Check(metrics.Length==1&&metrics[0].Title=="Jev"&&metrics[0].Value=="USD 10.00","desktop USD is independent of the DeepSeek currency filter");
                string json=Json.Serializer().Serialize(settings.Data);Check(!json.Contains("fixture-cookie")&&!json.Contains(saved),"neither plaintext nor encoded session enters preferences");
                shell.Window.ShowActivated=false;shell.Window.ShowInTaskbar=false;shell.Show();
                fake.Enable("Codex",true);fake.Tick(now);for(int i=0;i<200&&fake.GetState("Codex").Refreshing;i++){Thread.Sleep(5);fake.Tick(now);}
                foreach(string locale in new[]{"en","zh-CN","zh-TW"})foreach(bool light in new[]{false,true}){
                    QuotaFeatureTests.Language(shell,locale);settings.Data["background"]=light?"#F4F6F8":"#202831";Call(shell,"ApplyMaterial");
                    shell.Window.Width=280;shell.Window.Height=820;shell.ShowSettings(true);Call(shell,"SelectSettingsCategory","AI Quota");
                    shell.Control<Expander>("JevSessionTools").IsExpanded=true;shell.Control<Expander>("JevGuide").IsExpanded=false;cookie.BringIntoView();QuotaFeatureTests.Settle();
                    var tools=(FrameworkElement)shell.Control<Expander>("JevSessionTools").Content;
                    foreach(string name in new[]{"JevCookie","JevSessionSave","JevSessionRemove","JevOpenBrowser"}){
                        var control=shell.Control<FrameworkElement>(name);var point=control.TransformToAncestor(tools).Transform(new Point());Check(point.X>=0&&point.X+control.ActualWidth<=tools.ActualWidth+1,"narrow control bounds: "+locale+" / "+name);
                    }
                    string suffix=locale+(light?"-light":"-dark");QuotaFeatureTests.Capture(shell,Path.Combine(state,"jev-settings-"+suffix+".png"));
                    shell.Control<Expander>("JevGuide").IsExpanded=true;shell.Control<Expander>("JevGuide").BringIntoView();QuotaFeatureTests.Settle();QuotaFeatureTests.Capture(shell,Path.Combine(state,"jev-guide-"+suffix+".png"));
                    shell.ShowSettings(false);shell.Control<StackPanel>("Cards").Visibility=Visibility.Collapsed;Call(shell,"RenderQuota");QuotaFeatureTests.Settle();
                    Check(cards.Children.Count==2,"new wallet coexists with existing quota card");QuotaFeatureTests.Capture(shell,Path.Combine(state,"jev-cards-"+suffix+".png"));
                }
                QuotaFeatureTests.Language(shell,"en");
                var order=shell.Control<StackPanel>("DesktopOrderList");var row=order.Children.Cast<Border>().Single(r=>(string)r.Tag=="quotaJev");var grid=(Grid)row.Child;var visible=(CheckBox)grid.Children[2];
                visible.IsChecked=false;visible.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Check(!((IEnumerable<DesktopMetric>)typeof(Shell).GetMethod("DesktopMetrics",Hidden).Invoke(shell,null)).Any(m=>m.Key.StartsWith("quotaJev",StringComparison.Ordinal)),"desktop visibility suppresses the whole wallet group");
                visible.IsChecked=true;visible.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));order.Children.Remove(row);order.Children.Insert(0,row);Call(shell,"SaveDesktopOrder");shell.Save();
                Check(new Settings(Path.Combine(state,"widget-settings.json")).Order("desktopOrder",new[]{"quotaJev"})[0]=="quotaJev","wallet ordering persists under the stable provider group");
                shell.Control<Button>("JevSessionRemove").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Check(removes==1&&!stored&&shell.Control<CheckBox>("QuotaJev").IsChecked==false&&fake.Readings.All(r=>r.Provider!="Jev"),"remove clears the provider and disables further reads");
            }finally{field.SetValue(shell,original);shell.Exit();}
        }
        Console.WriteLine("PASS Jev WPF: guide, automatic UA, masked save/remove, no implicit monitoring, card/desktop USD and secret-free preferences; store IO injected");
        Console.WriteLine("Jev evidence: "+state);
    }
}
