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
    static void RefreshFeedback(Shell shell,string screenshot){
        var field=typeof(Shell).GetField("quotas",Hidden);var original=(QuotaSession)field.GetValue(shell);var now=DateTimeOffset.UtcNow;int claudeCalls=0,codexCalls=0;
        using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim())
        using(var fake=new QuotaSession((provider,cancel)=>{if(provider=="Claude"){Interlocked.Increment(ref claudeCalls);entered.Set();Check(release.Wait(5000),"feedback fixture not released");}else Interlocked.Increment(ref codexCalls);return new QuotaReading{Provider=provider,Status="Live",Observed=DateTimeOffset.UtcNow};})){
            field.SetValue(shell,fake);
            try{
                fake.RestoreSchedules(new[]{new QuotaSchedule{Provider="Claude",Status="Login required",Observed=now.AddMinutes(-10),NextAttempt=now.AddMinutes(20),NotBefore=now.AddMinutes(20),RecoveryFailures=4,RateLimitFailures=1},new QuotaSchedule{Provider="Codex",Status="Live",Observed=now,NextAttempt=now.AddMinutes(5),NotBefore=now.AddSeconds(30)}});
                fake.Enable("Claude",true);fake.Enable("Codex",true);Render(shell);
                var card=shell.Control<StackPanel>("QuotaCards").Children.Cast<Border>().Single(c=>(string)c.Tag=="Claude");
                var header=(Grid)((StackPanel)card.Child).Children[0];var button=header.Children.OfType<Button>().Single();
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));fake.Tick(now);Check(entered.Wait(5000),"card refresh did not request Claude");Render(shell);
                Check(shell.Control<TextBlock>("QuotaRefreshStatus").Text.Contains("Claude: Refreshing quota")&&Card(shell,"Claude").Any(s=>s.Contains("Refreshing quota")),"refresh progress missing in Settings/card");
                Check(codexCalls==0,"Claude card button refreshed another provider");
                release.Set();Complete(fake,"Claude",now);Render(shell);
                Check(shell.Control<TextBlock>("QuotaRefreshStatus").Text=="Claude: Quota refresh complete","completed refresh missing explicit feedback");
                shell.Control<Button>("QuotaRefresh").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Render(shell);
                Check(shell.Control<TextBlock>("QuotaRefreshStatus").Text.Contains("Refresh deferred until"),"minimum request interval was silently skipped");
                Capture(shell,Path.Combine(Path.GetDirectoryName(screenshot),"quota-refresh-feedback.png"));
                fake.Enable("Claude",false);fake.Enable("Codex",false);
                fake.RestoreSchedules(new[]{new QuotaSchedule{Provider="Claude",Status="Refresh rate limited",Observed=now,NextAttempt=now.AddHours(1),NotBefore=now.AddHours(1),RecoveryFailures=1,RateLimitFailures=1}});fake.Enable("Claude",true);
                shell.Control<Button>("QuotaRefresh").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Render(shell);
                Check(shell.Control<TextBlock>("QuotaRefreshStatus").Text.Contains("Refresh deferred until")&&shell.Control<TextBlock>("QuotaRefreshStatus").Text.Contains("Refresh rate limited"),"429 refresh must explain its wait");
                fake.Tick(now);Check(claudeCalls==1,"feedback button bypassed real 429");
                Capture(shell,Path.Combine(Path.GetDirectoryName(screenshot),"quota-refresh-wait.png"));
                shell.ShowSettings(true);typeof(Shell).GetMethod("SelectSettingsCategory",Hidden).Invoke(shell,new object[]{"AI Quota"});shell.Window.UpdateLayout();
                Capture(shell,Path.Combine(Path.GetDirectoryName(screenshot),"quota-refresh-settings.png"));shell.ShowSettings(false);
                // Only metadata is consulted, using an injected synthetic predicate.
                fake.RestoreSchedules(new[]{new QuotaSchedule{Provider="Claude",Status="Login required",Observed=now,NextAttempt=now.AddMinutes(30),NotBefore=now.AddMinutes(30),RecoveryFailures=4,RateLimitFailures=1}});
                fake.Readings.Single().CacheScope="synthetic-rejected-revision";
                typeof(Shell).GetField("nextClaudeRevisionCheck",Hidden).SetValue(shell,DateTimeOffset.MinValue);
                int checks=0;var check=typeof(Shell).GetMethod("CheckClaudeLoginRecovery",Hidden);
                check.Invoke(shell,new object[]{now,new Func<string,bool>(scope=>{checks++;return true;})});
                Check(fake.GetState("Claude").NextAttempt==now.AddMinutes(30),"unchanged login metadata shortened automatic backoff");
                check.Invoke(shell,new object[]{now.AddSeconds(29),new Func<string,bool>(scope=>{checks++;return false;})});Check(checks==1,"metadata checked more often than thirty seconds");
                check.Invoke(shell,new object[]{now.AddSeconds(30),new Func<string,bool>(scope=>{checks++;return false;})});
                Check(checks==2&&fake.GetState("Claude").NextAttempt==now.AddMinutes(5)&&fake.GetState("Claude").RateLimitFailures==1,"changed owner metadata failed bounded authentication recovery");
                fake.Enable("Claude",false);var language=(Languages)typeof(Shell).GetField("language",Hidden).GetValue(shell);string preference=language.Preference;
                try{language.Preference="zh-CN";Render(shell);Check(shell.Control<TextBlock>("QuotaRefreshStatus").Text=="Claude: 已关闭","Simplified Chinese disabled feedback is not localized");language.Preference="zh-TW";Render(shell);Check(shell.Control<TextBlock>("QuotaRefreshStatus").Text=="Claude: 已關閉","Traditional Chinese disabled feedback is not localized");}
                finally{language.Preference=preference;}
            }finally{release.Set();((Dictionary<string,QuotaReading>)typeof(Shell).GetField("quotaRefreshBefore",Hidden).GetValue(shell)).Clear();shell.Control<TextBlock>("QuotaRefreshStatus").Visibility=Visibility.Collapsed;field.SetValue(shell,original);Render(shell);}
        }
        Console.WriteLine("PASS native quota refresh feedback: single-provider button, progress, success, cooldown reason and metadata-only recovery");
    }
    static void TimeoutFeedback(Shell shell){
        var field=typeof(Shell).GetField("quotas",Hidden);var original=(QuotaSession)field.GetValue(shell);var now=DateTimeOffset.UtcNow;int calls=0;
        using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim())
        using(var fake=new QuotaSession((provider,cancel)=>{if(Interlocked.Increment(ref calls)==2){entered.Set();release.Wait(5000);}return new QuotaReading{Provider=provider,Status="Live",Observed=now};})){
            field.SetValue(shell,fake);
            try{
                fake.Enable("Claude",true);fake.Tick(now);Complete(fake,"Claude",now);Render(shell);
                typeof(Shell).GetMethod("RefreshQuota",Hidden).Invoke(shell,new object[]{"Claude"});fake.Tick(now.AddSeconds(30));Check(entered.Wait(5000),"timeout feedback fixture not started");
                fake.Tick(now.AddSeconds(60));Render(shell);
                Check(Card(shell,"Claude").Any(s=>s.StartsWith("Refresh timed out · Waiting for request to stop")),"timed-out card still claims Live while waiting for worker");
                Check(shell.Control<TextBlock>("QuotaRefreshStatus").Text.Contains("Refresh timed out"),"Settings hides the pending timeout reason");
            }finally{
                release.Set();Complete(fake,"Claude",now.AddSeconds(60));
                ((Dictionary<string,QuotaReading>)typeof(Shell).GetField("quotaRefreshBefore",Hidden).GetValue(shell)).Clear();shell.Control<TextBlock>("QuotaRefreshStatus").Visibility=Visibility.Collapsed;field.SetValue(shell,original);Render(shell);
            }
        }
    }
    public static void RunUI(Shell shell,string screenshot){
        RefreshFeedback(shell,screenshot);TimeoutFeedback(shell);
        var field=typeof(Shell).GetField("quotas",Hidden);var original=(QuotaSession)field.GetValue(shell);var now=DateTimeOffset.UtcNow;
        QuotaReading next=new QuotaReading{Provider="Claude",Status="Live",CacheScope="synthetic-source",Observed=now.AddMinutes(-2),Windows=new List<QuotaWindow>{new QuotaWindow{Label="5-hour",Remaining=73,Reset=now.AddMinutes(30)},new QuotaWindow{Label="Weekly",Remaining=91,Reset=now.AddDays(5)}}};
        using(var fake=new QuotaSession((provider,cancel)=>next)){
            field.SetValue(shell,fake);
            try{
                fake.Enable("Claude",true);fake.Tick(now);Complete(fake,"Claude",now);
                next=new QuotaReading{Provider="Claude",Status="Refresh rate limited",CacheScope="synthetic-source",HttpStatus=429,FailureKind="HTTP response",Observed=now,RetryAt=now.AddMinutes(2)};
                fake.Refresh();fake.Tick(now.AddSeconds(30));Complete(fake,"Claude",now.AddSeconds(30));Render(shell);
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
                fake.Tick(now.AddMinutes(16));Complete(fake,"Claude",now.AddMinutes(16));Render(shell);
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
                QuotaScheduleStoreTests.Run(Path.GetDirectoryName(screenshot));
            }finally{field.SetValue(shell,original);Render(shell);}
        }
    }
}
