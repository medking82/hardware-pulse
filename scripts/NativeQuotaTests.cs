using System;
using System.Linq;
using System.Threading;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using HardwarePulse;

internal static class NativeQuotaTests {
    static void Check(bool value,string message){if(!value)throw new Exception("Quota: "+message);}
    static QuotaReading Decode(string provider,string json){return QuotaDecoder.Decode(provider,QuotaData.Parse(json),DateTimeOffset.UtcNow);}
        public static void Run(){
            var cliReport=typeof(QuotaProviders).Assembly.GetType("HardwarePulse.AntigravityCliQuota").GetMethod("ReadReport",BindingFlags.NonPublic|BindingFlags.Static);
            string weekly="Gemini Models\tWeekly Limit Remaining\t0%\t2026-10-01T00:00:00Z\n";
            string sessionReport="Gemini Models\tFive Hour Limit Remaining\t100%\t2026-09-30T12:00:00+08:00\n";
            var cliBody=cliReport.Invoke(null,new object[]{new System.IO.StringReader(weekly+sessionReport),new System.IO.StringWriter(),CancellationToken.None});
            var cliQuota=QuotaDecoder.Decode("Antigravity",cliBody,DateTimeOffset.UtcNow);
            Check(cliQuota.Status=="Live"&&cliQuota.Windows.Count==2&&cliQuota.Windows[0].Remaining==100&&cliQuota.Windows[1].Remaining==0,"CLI quota preserves genuine zero/full windows");
            foreach(string bad in new[]{"",weekly,weekly+weekly+sessionReport,(weekly+sessionReport).Replace("100%","101%"),(weekly+sessionReport).Replace("100%","50%%"),(weekly+sessionReport).Replace("2026-10-01T00:00:00Z","2026-10-01T00:00:00"),new string('x',65537)}){
                try{cliReport.Invoke(null,new object[]{new System.IO.StringReader(bad),new System.IO.StringWriter(),CancellationToken.None});throw new Exception("Invalid CLI quota accepted");}
                catch(TargetInvocationException failure){Check(failure.InnerException is QuotaFailure,"Invalid CLI quota must fail closed");}
            }
        var protocol=typeof(QuotaProviders).Assembly.GetType("HardwarePulse.CodexAppServerQuota").GetMethod("ReadProtocol",BindingFlags.NonPublic|BindingFlags.Static);
        var output=new System.IO.StringWriter();
        string rpc="{\"id\":1,\"result\":{}}\n{\"method\":\"account/updated\",\"params\":{}}\n{\"id\":2,\"result\":{\"rateLimits\":{\"secondary\":{\"usedPercent\":36,\"windowDurationMins\":10080}}}}\n";
        var rpcBody=protocol.Invoke(null,new object[]{new System.IO.StringReader(rpc),output,CancellationToken.None});
        var rpcQuota=QuotaDecoder.Decode("Codex",rpcBody,DateTimeOffset.UtcNow);
        Check(rpcQuota.Status=="Live"&&rpcQuota.Windows.Single().Remaining==64,"official RPC quota decoded through existing model");
        var sent=output.ToString().Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries).Select(line=>QuotaDecoder.Text(QuotaDecoder.Get(QuotaData.Parse(line),"method"))).ToArray();
        Check(sent.SequenceEqual(new[]{"initialize","initialized","account/rateLimits/read"}),"RPC must issue only handshake and quota read");
        foreach(string invalid in new[]{"", "{\"id\":1,\"error\":{\"message\":\"private fixture\"}}\n",new string('x',1048577)}){
            try{protocol.Invoke(null,new object[]{new System.IO.StringReader(invalid),new System.IO.StringWriter(),CancellationToken.None});throw new Exception("Invalid RPC accepted");}
            catch(TargetInvocationException error){Check(error.InnerException is QuotaFailure&&((QuotaFailure)error.InnerException).Status=="Quota unavailable","RPC failure must be bounded and sanitized");}
        }
        var ownership=typeof(QuotaProviders).Assembly.GetType("HardwarePulse.AntigravityQuota").GetMethod("IsOwnedProcess",BindingFlags.NonPublic|BindingFlags.Static);
        uint pid=(uint)System.Diagnostics.Process.GetCurrentProcess().Id;string sid=System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
        Check((bool)ownership.Invoke(null,new object[]{pid,sid}),"bound WMI instance can query current process owner");
        Check(!(bool)ownership.Invoke(null,new object[]{pid,"S-1-0-0"}),"different process owner still rejected");
        var codex=Decode("Codex","{\"rate_limit\":{\"primary_window\":{\"used_percent\":0,\"limit_window_seconds\":18000,\"reset_at\":1800000000},\"secondary_window\":{\"used_percent\":36,\"limit_window_seconds\":604800}},\"additional_rate_limits\":[{\"metered_feature\":\"spark\",\"rate_limit\":{\"primary_window\":{\"used_percent\":100,\"limit_window_seconds\":18000}}}]}");
        Check(codex.Status=="Live"&&codex.Windows.Count==1,"Codex main weekly only");Check(codex.Windows[0].Label=="Weekly"&&codex.Windows[0].Remaining==64,"used to remaining weekly conversion");
        Check(codex.AllWindows.Count==3&&codex.AllWindows.Any(w=>w.Label=="spark · 5-hour"&&w.Remaining==0),"Codex HTTP additional_rate_limits missing from full display");
        var pools=Decode("Codex","{\"rateLimitsByLimitId\":{\"codex\":{\"secondary\":{\"usedPercent\":100,\"windowDurationMins\":10080,\"resetsAt\":1800000000}},\"spark\":{\"secondary\":{\"usedPercent\":0,\"windowDurationMins\":10080}}}}");Check(pools.Windows.Count==1&&pools.Windows[0].Remaining==0&&pools.Windows[0].Reset.HasValue,"Codex weekly pool identity, zero remaining and Unix reset");
        var claude=Decode("Claude","{\"five_hour\":{\"utilization\":26,\"resets_at\":\"2026-09-15T00:00:00Z\"},\"seven_day\":{\"utilization\":4},\"seven_day_sonnet\":null,\"extra_usage\":{\"utilization\":90}}");Check(claude.Windows.Count==2&&claude.Windows[0].Remaining==74&&claude.Windows[1].Remaining==96,"Claude excludes spending and absent pools");
        var ag=Decode("Antigravity","{\"response\":{\"groups\":[{\"displayName\":\"Gemini\",\"buckets\":[{\"window\":\"session\",\"remainingFraction\":0.8},{\"window\":\"weekly\",\"remaining\":{\"case\":\"remainingFraction\",\"value\":0},\"resetTime\":\"2026-09-16T00:00:00Z\"},{\"window\":\"weekly\",\"remainingFraction\":1,\"disabled\":true}]}]}}");Check(ag.Windows.Count==2&&ag.Windows[0].Label=="5-hour"&&ag.Windows[0].Remaining==80&&ag.Windows[1].Label=="Weekly"&&ag.Windows[1].Remaining==0,"AG Gemini two windows, fractions, zero and deduplication");
        var selected=Decode("Antigravity","{\"groups\":[{\"displayName\":\"Claude and GPT models\",\"buckets\":[{\"window\":\"5h\",\"remainingFraction\":1}]},{\"displayName\":\"Gemini Models\",\"buckets\":[{\"window\":\"weekly\",\"remainingFraction\":1,\"disabled\":true},{\"window\":\"5h\",\"remainingFraction\":0.5},{\"window\":\"monthly\",\"remainingFraction\":1}]}]}");
        Check(selected.Windows.Count==2&&selected.Windows[0].Label=="5-hour"&&selected.Windows[0].Remaining==50&&selected.Windows[1].Label=="Weekly"&&!selected.Windows[1].Remaining.HasValue,"AG excludes other model groups and unknown windows; sorts 5-hour first and preserves disabled");
        foreach(string provider in QuotaSession.Providers)Check(Decode(provider,"{}").Status!="Live","missing response not 100%");
        Check(!Decode("Claude","{\"five_hour\":{\"utilization\":-1}}").Windows[0].Remaining.HasValue,"invalid percent");Check(!Decode("Claude","{\"five_hour\":{\"utilization\":\"0\"}}").Windows[0].Remaining.HasValue,"numeric strings rejected");
        Check(QuotaDecoder.ResetText(DateTimeOffset.UtcNow.AddSeconds(-1),DateTimeOffset.UtcNow)=="Reset pending","reset does not fabricate quota");
        var request=typeof(QuotaProviders).GetMethod("Request",BindingFlags.NonPublic|BindingFlags.Static);
        try{request.Invoke(null,new object[]{"https://example.invalid/usage",new System.Collections.Generic.Dictionary<string,string>(),null,CancellationToken.None,false});throw new Exception("Untrusted URL admitted");}catch(TargetInvocationException e){Check(e.InnerException.GetType().Name=="QuotaFailure","remote URL allowlist");}
        foreach(var failure in new[]{Tuple.Create(302,"Quota unavailable"),Tuple.Create(401,"Login required"),Tuple.Create(403,"Quota access denied"),Tuple.Create(429,"Refresh rate limited"),Tuple.Create(500,"Quota unavailable")}){
        var listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);listener.Start();
        try{
            int port=((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            var server=System.Threading.Tasks.Task.Run(()=>{using(var client=listener.AcceptTcpClient()){client.ReceiveTimeout=3000;using(var stream=client.GetStream()){var reader=new System.IO.StreamReader(stream);string line;do{line=reader.ReadLine();}while(!string.IsNullOrEmpty(line));var response=System.Text.Encoding.ASCII.GetBytes("HTTP/1.1 "+failure.Item1+" Test\r\nLocation: https://example.invalid/credential-leak\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");stream.Write(response,0,response.Length);}}});
            try{request.Invoke(null,new object[]{"http://127.0.0.1:"+port,new System.Collections.Generic.Dictionary<string,string>{{"Authorization","Bearer synthetic-test-only"}},null,CancellationToken.None,true});throw new Exception("HTTP failure admitted");}catch(TargetInvocationException e){var error=e.InnerException as QuotaFailure;Check(error!=null&&error.Status==failure.Item2,"HTTP "+failure.Item1+" must report "+failure.Item2+" without forwarding credentials");}
            Check(server.Wait(3000),"local redirect fixture completed");
        }finally{listener.Stop();}
        }
        int calls=0;using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim())using(var session=new QuotaSession((provider,cancel)=>{Interlocked.Increment(ref calls);entered.Set();release.Wait();return new QuotaReading{Provider=provider,Status="Live"};})){
            var now=DateTimeOffset.UtcNow;session.Enable("Codex",true);session.Tick(now);Check(entered.Wait(3000),"background refresh starts");session.Tick(now.AddMinutes(6));Check(calls==1,"no overlapping requests");session.Enable("Codex",false);release.Set();Thread.Sleep(50);session.Tick(now);Check(session.Readings.Length==0,"disabled result discarded");
        }
        using(var session=new QuotaSession((provider,cancel)=>{throw new Exception("sensitive diagnostic must not escape");})){
            session.Enable("Claude",true);session.Tick(DateTimeOffset.UtcNow);for(int i=0;i<100&&session.Readings[0].Status=="Refresh pending";i++){Thread.Sleep(10);session.Tick(DateTimeOffset.UtcNow);}Check(session.Readings[0].Status=="Quota unavailable","fault sanitized");
        }
        Console.WriteLine("Quota parser and refresh lifecycle checks passed");
    }
    public static void RunUI(Shell shell,string screenshot){
        var field=typeof(Shell).GetField("quotas",BindingFlags.NonPublic|BindingFlags.Instance);var original=(QuotaSession)field.GetValue(shell);
        using(var fake=new QuotaSession((provider,cancel)=>new QuotaReading{Provider=provider,Status="Live",Observed=DateTimeOffset.UtcNow,Windows=new System.Collections.Generic.List<QuotaWindow>{new QuotaWindow{Label="5-hour",Remaining=74,Reset=DateTimeOffset.UtcNow.AddMinutes(30)},new QuotaWindow{Label="Weekly",Remaining=96,Reset=DateTimeOffset.UtcNow.AddDays(6)}}})){
            field.SetValue(shell,fake);try{
                foreach(string provider in QuotaSession.Providers){var toggle=shell.Control<CheckBox>("Quota"+provider);Check(toggle.IsChecked!=true,"providers opt in");toggle.IsChecked=true;toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));}
                fake.Tick(DateTimeOffset.UtcNow);for(int i=0;i<100&&fake.Readings.Any(r=>r.Status!="Live");i++){Thread.Sleep(10);fake.Tick(DateTimeOffset.UtcNow);}shell.UpdatePanel();
                Check(shell.Control<StackPanel>("QuotaCards").Children.Count==3,"three provider cards");
                var metrics=(System.Collections.IEnumerable)typeof(Shell).GetMethod("DesktopMetrics",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(shell,null);Check(metrics.Cast<object>().Count()>=6,"Desktop quota readings");
                var hardware=shell.Control<StackPanel>("Cards");var previous=hardware.Visibility;double height=shell.Window.Height;shell.Window.Height=920;hardware.Visibility=Visibility.Collapsed;shell.Window.UpdateLayout();
                var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)shell.Window.ActualWidth,(int)shell.Window.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(shell.Window);var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using(var file=System.IO.File.Create(screenshot))png.Save(file);
                hardware.Visibility=previous;shell.Window.Height=height;
                foreach(string provider in QuotaSession.Providers){var toggle=shell.Control<CheckBox>("Quota"+provider);toggle.IsChecked=false;toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));}shell.UpdatePanel();Check(shell.Control<StackPanel>("QuotaCards").Children.Count==0,"disable removes quota view");
            }finally{field.SetValue(shell,original);}
        }
    }
}
