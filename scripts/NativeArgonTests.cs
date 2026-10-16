using System;
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

// Account-free acceptance of the real native projection and desktop settings.
internal static class NativeArgonTests {
    const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    static void Check(bool value,string message){if(!value)throw new Exception("Argon: "+message);}
    static object Call(Shell shell,string name,params object[] args){return typeof(Shell).GetMethod(name,Hidden).Invoke(shell,args);}
    static System.Collections.Generic.IEnumerable<DependencyObject> Tree(DependencyObject root){yield return root;foreach(object item in LogicalTreeHelper.GetChildren(root)){var node=item as DependencyObject;if(node!=null)foreach(var child in Tree(node))yield return child;}}
    static void Settle(){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(300)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    static DesktopMetric[] Rows(Shell shell){return ((System.Collections.Generic.IEnumerable<DesktopMetric>)Call(shell,"DesktopMetrics")).Where(m=>m.Key.StartsWith("quotaAntigravity",StringComparison.Ordinal)).ToArray();}
    internal static void Run(string root,string directory){
        var parser=typeof(QuotaProviders).Assembly.GetType("HardwarePulse.AntigravityCliQuota").GetMethod("ReadReport",BindingFlags.NonPublic|BindingFlags.Static);
        string report="Gemini Models\tWeekly Limit Remaining\t54%\t2026-10-23T00:00:00Z\nGemini Models\tFive Hour Limit Remaining\t73%\t2026-10-22T00:00:00Z\nGemini 4 Argon\tGemini 4 Argon Weekly Limit\t0%\t2026-10-24T00:00:00Z\n";
        var body=parser.Invoke(null,new object[]{new StringReader(report),new StringWriter(),CancellationToken.None});
        var cli=QuotaDecoder.Decode("Antigravity",body,DateTimeOffset.UtcNow);
        Check(cli.Status=="Live"&&cli.Windows.Count==3&&cli.Windows[2].Remaining==0,"weekly-only Argon CLI group must not invalidate Gemini");
        string legacy=Path.Combine(directory,"legacy-hidden");Directory.CreateDirectory(legacy);
        Json.WriteAtomic(Path.Combine(legacy,"widget-settings.json"),new {language="en",desktopVisible=new System.Collections.Generic.Dictionary<string,object>{{"quotaAntigravity0",false},{"quotaAntigravity1",false}}});
        using(var hidden=new Shell(new PulsePaths(root,legacy,Path.Combine(legacy,"runtime")),true)){
            Check(!(bool)Call(hidden,"DesktopMetricEnabled","quotaAntigravityArgonWeekly"),"Upgrading must not re-enable a fully hidden Gemini provider through the new Argon row");hidden.Exit();
        }
        Directory.CreateDirectory(directory);Json.WriteAtomic(Path.Combine(directory,"widget-settings.json"),new {language="en",solid=true});
        using(var shell=new Shell(new PulsePaths(root,directory,Path.Combine(directory,"runtime")),true))
        using(var fake=new QuotaSession((provider,cancel)=>{
            var reading=QuotaDecoder.Decode("Antigravity",body,DateTimeOffset.UtcNow);
            foreach(var window in reading.Windows.Concat(reading.AllWindows))window.Reset=DateTimeOffset.UtcNow.AddDays(window.Id=="gemini-argon-wtu-weekly"?6:4);
            return reading;
        })){
            var field=typeof(Shell).GetField("quotas",Hidden);var original=(QuotaSession)field.GetValue(shell);field.SetValue(shell,fake);
            try{
                shell.Window.ShowActivated=false;shell.Window.ShowInTaskbar=false;shell.Show();shell.Control<StackPanel>("Cards").Visibility=Visibility.Collapsed;
                fake.Enable("Antigravity",true);fake.Tick(DateTimeOffset.UtcNow);for(int i=0;i<200&&fake.GetState("Antigravity").Refreshing;i++){Thread.Sleep(5);fake.Tick(DateTimeOffset.UtcNow);}
                Call(shell,"RenderQuota");var rows=Rows(shell);
                var fullOnly=new QuotaReading{Provider="Antigravity",Observed=DateTimeOffset.UtcNow,AllWindows=new System.Collections.Generic.List<QuotaWindow>{new QuotaWindow{Label="Claude and GPT models · Weekly",Remaining=40}}};
                var fullDisplay=(QuotaReading)Call(shell,"DisplayQuota",fullOnly,true,DateTimeOffset.UtcNow);
                Check(fullDisplay.Status=="Live"&&fullDisplay.Windows.Any(w=>w.Remaining==40),"Existing full-mode valid model pools must remain visible when compact pools are unavailable");
                Check(rows.Length==3&&rows[2].Key=="quotaAntigravityArgonWeekly"&&rows[2].Title=="Argon · Weekly"&&rows[2].Value=="0% Left","desktop includes a concise independent Argon row with stable identity");
                var order=(string[])Call(shell,"DesktopOrderKeys");Check(order.Contains("quotaAntigravityArgonWeekly"),"Argon must appear in desktop settings");
                var settings=(Settings)typeof(Shell).GetField("settings",Hidden).GetValue(shell);
                settings.Map("desktopVisible")["quotaAntigravity0"]=false;settings.Map("desktopVisible")["quotaAntigravity1"]=false;
                Check((bool)Call(shell,"DesktopMetricEnabled","quotaAntigravity"),"Argon-only visibility keeps its provider's unavailable state visible");
                settings.Map("desktopVisible")["quotaAntigravityArgonWeekly"]=false;Check(!(bool)Call(shell,"DesktopMetricEnabled","quotaAntigravity"),"hiding all three windows hides the provider status");
                settings.Map("desktopVisible")["quotaAntigravityArgonWeekly"]=true;settings.Map("desktopVisible")["quotaAntigravity0"]=true;settings.Map("desktopVisible")["quotaAntigravity1"]=true;
                var reading=fake.Readings.Single();reading.Windows.Reverse();rows=Rows(shell);
                Check(rows.Single(m=>m.Title=="Argon · Weekly").Key=="quotaAntigravityArgonWeekly"&&rows.Single(m=>m.Title=="Gemini · Weekly").Key=="quotaAntigravity1","reordering source windows must not exchange saved preferences");reading.Windows.Reverse();
                foreach(string locale in new[]{"en","zh-CN"})foreach(bool light in new[]{false,true}){
                    foreach(ComboBoxItem option in shell.Control<ComboBox>("LanguagePicker").Items)if((string)option.Tag==locale)shell.Control<ComboBox>("LanguagePicker").SelectedItem=option;
                    settings.Data["background"]=light?"#F4F6F8":"#202831";Call(shell,"ApplyMaterial");shell.Window.Width=360;shell.Window.Height=640;Call(shell,"RenderQuota");Settle();shell.Window.UpdateLayout();
                    Check(Tree(shell.Control<StackPanel>("QuotaCards")).OfType<TextBlock>().Any(t=>t.Text=="Argon"),"card has a separate Argon heading");
                    var bitmap=new RenderTargetBitmap((int)shell.Window.ActualWidth,(int)shell.Window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(shell.Window);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(directory,"argon-card-"+locale+(light?"-light":"-dark")+".png")))png.Save(file);
                    var desktop=new DesktopView((name,size,color)=>(FrameworkElement)Call(shell,"Icon",name,size,color),true);try{
                        desktop.Width=280;desktop.ShowActivated=false;desktop.Show();desktop.Render(Rows(shell),16,8,light?"#17202B":"#F0F5FA",true,1,name=>(string)Call(shell,"DesktopPaletteColor",name,light?"#17202B":"#F0F5FA"));desktop.SetTextOpacity(100,false,true,100);Settle();desktop.UpdateLayout();
                        var labels=Tree(desktop).OfType<TextBlock>().Where(t=>t.IsVisible).ToArray();Check(labels.Any(t=>t.Text.StartsWith("Argon · ")),"actual desktop renders Argon");
                        foreach(var label in labels){var point=label.TransformToAncestor(desktop).Transform(new Point());Check(point.X>=-1&&point.X+label.ActualWidth<=desktop.ActualWidth+1,"280px desktop label stays in bounds");}
                        var image=new RenderTargetBitmap((int)desktop.ActualWidth,(int)desktop.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(desktop);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(Path.Combine(directory,"argon-desktop-"+locale+(light?"-light":"-dark")+".png")))encoder.Save(file);
                    }finally{desktop.Close();}
                }
                shell.Save();Check(new Settings(Path.Combine(directory,"widget-settings.json")).Map("desktopVisible").ContainsKey("quotaAntigravityArgonWeekly"),"Argon display preference persists");
            }finally{field.SetValue(shell,original);shell.Exit();}
        }
        Console.WriteLine("PASS Argon: weekly-only CLI, compact/full, zero, stable desktop keys, settings, English/Chinese and dark/light 280px WPF");
        Console.WriteLine("Argon evidence: "+directory);
    }
}
