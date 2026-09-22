using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using HardwarePulse;

internal static class NativeQuotaRecoveryTests {
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool value,string message){if(!value)throw new Exception("Quota recovery UI: "+message);}
    static IEnumerable<string> Labels(DependencyObject node){var text=node as TextBlock;if(text!=null)yield return text.Text;foreach(object child in LogicalTreeHelper.GetChildren(node)){var item=child as DependencyObject;if(item!=null)foreach(string label in Labels(item))yield return label;}}
    static void Render(Shell shell){typeof(Shell).GetMethod("RenderQuota",Hidden).Invoke(shell,null);}
    static string[] Card(Shell shell,string provider){return Labels(shell.Control<StackPanel>("QuotaCards").Children.Cast<Border>().Single(c=>(string)c.Tag==provider)).ToArray();}
    static DesktopMetric[] Desktop(Shell shell){var result=new List<DesktopMetric>();typeof(Shell).GetMethod("AddDesktopQuotas",Hidden).Invoke(shell,new object[]{result});return result.ToArray();}
    static void Complete(QuotaSession session,string provider,DateTimeOffset now){for(int i=0;i<200&&session.GetState(provider).Refreshing;i++){Thread.Sleep(5);session.Tick(now);}Check(!session.GetState(provider).Refreshing,"synthetic worker did not finish");}
    static void Capture(Shell shell,string path){
        var hardware=shell.Control<StackPanel>("Cards");var visible=hardware.Visibility;double width=shell.Window.Width,height=shell.Window.Height;
        try{hardware.Visibility=Visibility.Collapsed;shell.Window.Width=420;shell.Window.Height=560;shell.Window.UpdateLayout();var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)shell.Window.ActualWidth,(int)shell.Window.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(shell.Window);var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using(var file=File.Create(path))png.Save(file);}
        finally{hardware.Visibility=visible;shell.Window.Width=width;shell.Window.Height=height;}
    }
    public static void RunUI(Shell shell,string screenshot){
        var field=typeof(Shell).GetField("quotas",Hidden);var original=(QuotaSession)field.GetValue(shell);var now=DateTimeOffset.UtcNow;
        QuotaReading next=new QuotaReading{Provider="Claude",Status="Live",CacheScope="synthetic-source",Observed=now.AddMinutes(-2),Windows=new List<QuotaWindow>{new QuotaWindow{Label="5-hour",Remaining=73,Reset=now.AddMinutes(30)},new QuotaWindow{Label="Weekly",Remaining=91,Reset=now.AddDays(5)}}};
        using(var fake=new QuotaSession((provider,cancel)=>next)){
            field.SetValue(shell,fake);
            try{
                fake.Enable("Claude",true);fake.Tick(now);Complete(fake,"Claude",now);
                next=new QuotaReading{Provider="Claude",Status="Refresh rate limited",CacheScope="synthetic-source",HttpStatus=429,FailureKind="HTTP response",Observed=now,RetryAt=now.AddMinutes(2)};
                fake.Refresh();fake.Tick(now);Complete(fake,"Claude",now);Render(shell);
                var card=Card(shell,"Claude");
                Check(card.Any(s=>s.Contains("Retry in"))&&card.Any(s=>s.Contains("Cached 2 min ago")),"retry and cache age must be distinct from latest error age");
                Check(card.Any(s=>s.Contains("73%"))&&card.Any(s=>s.Contains("91%"))&&!card.Any(s=>s.StartsWith("Live")),"same-source cached values must never appear Live");
                Check(Desktop(shell).All(m=>m.Value.Contains("Cached")&&m.ToolTip.Contains("HTTP 429")),"Desktop cached readings must visibly label their age/state");
                var stableCard=shell.Control<StackPanel>("QuotaCards").Children[0];Render(shell);Check(object.ReferenceEquals(stableCard,shell.Control<StackPanel>("QuotaCards").Children[0]),"unchanged countdown must not rebuild cards each tick");
                Capture(shell,Path.Combine(Path.GetDirectoryName(screenshot),"quota-cached.png"));
                fake.GetState("Claude").LastGood.Windows[0].Reset=now.AddSeconds(-1);Render(shell);card=Card(shell,"Claude");
                Check(!card.Any(s=>s.Contains("73%"))&&card.Any(s=>s.Contains("91%")),"cache reset must invalidate each window independently");
                fake.GetState("Claude").LastGood.Observed=now.AddMinutes(-11);Render(shell);card=Card(shell,"Claude");
                Check(card.Contains("5-hour")&&card.Contains("Weekly")&&card.Count(s=>s=="—")==2&&!card.Any(s=>s.Contains("91%")),"expired cache must keep basic structure with unknown values");
                Check(Desktop(shell).Single().Value=="Refresh rate limited"&&Desktop(shell).Single().ToolTip.Contains("Next retry"),"Desktop error remains concise with retry detail");
                fake.GetState("Claude").LastGood.Windows[0].Reset=now.AddMinutes(30);
                fake.GetState("Claude").LastGood.Observed=now.AddMinutes(-2);Render(shell);card=Card(shell,"Claude");
                Check(card.Any(s=>s.Contains("73%"))&&card.Any(s=>s.Contains("91%")),"scope fixture cache missing before rejection");
                next=new QuotaReading{Provider="Claude",Status="Refresh rate limited",Observed=now,CacheScope="different-source",HttpStatus=429};
                fake.Tick(now.AddMinutes(3));Complete(fake,"Claude",now.AddMinutes(3));Render(shell);
                card=Card(shell,"Claude");Check(!card.Any(s=>s.Contains("73%")||s.Contains("91%")),"source change cannot reuse previous account values");
                fake.Enable("Claude",false);
                next=new QuotaReading{Provider="Antigravity",Status="Quota unavailable",Source="Desktop",HttpStatus=503,FailureKind="HTTP response",Observed=now};
                fake.Enable("Antigravity",true);fake.Tick(now);Complete(fake,"Antigravity",now);Render(shell);
                Check(Card(shell,"Antigravity").Any(s=>s.Contains("Retry in")),"Antigravity unavailable must expose scheduled retry");
                Check(Desktop(shell).Single().ToolTip.Contains("HTTP 503"),"Antigravity failure detail must retain safe HTTP code");
                fake.Enable("Antigravity",false);
                next=new QuotaReading{Provider="Claude",Status="Refresh rate limited",Observed=now,CacheScope="different-source",HttpStatus=429};
                fake.Enable("Claude",true);fake.Tick(now);Complete(fake,"Claude",now);Render(shell);
                Capture(shell,screenshot);
                QuotaDiagnosticLogTests.Run(Path.GetDirectoryName(screenshot));
            }finally{field.SetValue(shell,original);Render(shell);}
        }
    }
}
