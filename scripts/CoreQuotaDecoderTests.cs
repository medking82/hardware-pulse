using System;
using System.Collections.Generic;
using HardwarePulse;
using System.Linq;

internal static class CoreQuotaDecoderTests {
    static void Check(bool value,string message){if(!value)throw new Exception("Core quota: "+message);}
    static QuotaReading Decode(string provider,string json){
#if NET10_0
        using(var document=System.Text.Json.JsonDocument.Parse(json))
            return QuotaDecoder.Decode(provider,Graph(document.RootElement),DateTimeOffset.UtcNow);
#else
        var body=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Dictionary<string,object>>(json);
        return QuotaDecoder.Decode(provider,body,DateTimeOffset.UtcNow);
#endif
    }
#if NET10_0
    // Test-only JSON bridge. Core consumes ordinary decoded values, never JsonElement.
    static object Graph(System.Text.Json.JsonElement element){
        switch(element.ValueKind){
            case System.Text.Json.JsonValueKind.Object:
                var map=new Dictionary<string,object>();foreach(var property in element.EnumerateObject())map.Add(property.Name,Graph(property.Value));return map;
            case System.Text.Json.JsonValueKind.Array:return element.EnumerateArray().Select(Graph).ToArray();
            case System.Text.Json.JsonValueKind.String:return element.GetString();
            case System.Text.Json.JsonValueKind.Number:return element.GetDouble();
            case System.Text.Json.JsonValueKind.True:return true;
            case System.Text.Json.JsonValueKind.False:return false;
            default:return null;
        }
    }
#endif
    public static void Run(){
        ArgonIndependentWindow();
        LocalGenerationSpeed();
        LocalInferenceLifecycle();
        StatusLineSnapshots();
        var codex=Decode("Codex","{\"rate_limit\":{\"primary_window\":{\"used_percent\":0,\"limit_window_seconds\":18000,\"reset_at\":1800000000},\"secondary_window\":{\"used_percent\":36,\"limit_window_seconds\":604800}},\"additional_rate_limits\":[{\"metered_feature\":\"spark\",\"rate_limit\":{\"primary_window\":{\"used_percent\":100,\"limit_window_seconds\":18000}}}]}");
        Check(codex.Status=="Live"&&codex.Windows.Count==1,"Codex main weekly only");Check(codex.Windows[0].Label=="Weekly"&&codex.Windows[0].Remaining==64,"used to remaining weekly conversion");
        Check(codex.AllWindows.Count==3&&codex.AllWindows.Any(w=>w.Label=="spark · 5-hour"&&w.Remaining==0),"Codex HTTP additional_rate_limits missing from full display");
        var pools=Decode("Codex","{\"rateLimitsByLimitId\":{\"codex\":{\"secondary\":{\"usedPercent\":100,\"windowDurationMins\":10080,\"resetsAt\":1800000000}},\"spark\":{\"secondary\":{\"usedPercent\":0,\"windowDurationMins\":10080}}}}");Check(pools.Windows.Count==1&&pools.Windows[0].Remaining==0&&pools.Windows[0].Reset.HasValue,"Codex weekly pool identity, zero remaining and Unix reset");
        var claude=Decode("Claude","{\"five_hour\":{\"utilization\":26,\"resets_at\":\"2026-09-15T00:00:00Z\"},\"seven_day\":{\"utilization\":4},\"seven_day_sonnet\":null,\"extra_usage\":{\"utilization\":90}}");Check(claude.Windows.Count==2&&claude.Windows[0].Remaining==74&&claude.Windows[1].Remaining==96,"Claude excludes spending and absent pools");
        var ag=Decode("Antigravity","{\"response\":{\"groups\":[{\"displayName\":\"Gemini\",\"buckets\":[{\"window\":\"session\",\"remainingFraction\":0.8},{\"window\":\"weekly\",\"remaining\":{\"case\":\"remainingFraction\",\"value\":0},\"resetTime\":\"2026-09-16T00:00:00Z\"},{\"window\":\"weekly\",\"remainingFraction\":1,\"disabled\":true}]}]}}");Check(ag.Windows.Count==2&&ag.Windows[0].Label=="5-hour"&&ag.Windows[0].Remaining==80&&ag.Windows[1].Label=="Weekly"&&ag.Windows[1].Remaining==0,"AG Gemini two windows, fractions, zero and deduplication");
        var selected=Decode("Antigravity","{\"groups\":[{\"displayName\":\"Claude and GPT models\",\"buckets\":[{\"window\":\"5h\",\"remainingFraction\":1}]},{\"displayName\":\"Gemini Models\",\"buckets\":[{\"window\":\"weekly\",\"remainingFraction\":1,\"disabled\":true},{\"window\":\"5h\",\"remainingFraction\":0.5},{\"window\":\"monthly\",\"remainingFraction\":1}]}]}");
        Check(selected.Windows.Count==2&&selected.Windows[0].Label=="5-hour"&&selected.Windows[0].Remaining==50&&selected.Windows[1].Label=="Weekly"&&!selected.Windows[1].Remaining.HasValue,"AG excludes other model groups and unknown windows; sorts 5-hour first and preserves disabled");
        foreach(string provider in QuotaSession.Providers)Check(Decode(provider,"{}").Status!="Live","missing response not 100%");
        var balance=Decode("DeepSeek","{\"is_available\":true,\"balance_infos\":[{\"currency\":\"CNY\",\"total_balance\":\"110.00\",\"granted_balance\":\"10.00\",\"topped_up_balance\":\"100.00\"}]}");
        Check(balance.Status=="Live"&&balance.Balances.Count==1&&balance.Balances[0].Currency=="CNY"&&balance.Balances[0].Amount=="110.00","DeepSeek available balance decoded as money");
        Check(balance.AllWindows.Count==0&&balance.AllBalances.Count==3,"DeepSeek full display keeps granted and topped up rows without percentage windows");
        var depleted=Decode("DeepSeek","{\"is_available\":false,\"balance_infos\":[{\"currency\":\"CNY\",\"total_balance\":\"0.00\",\"granted_balance\":\"0.00\",\"topped_up_balance\":\"0.00\"}]}");
        Check(depleted.Status=="Live"&&depleted.Balances[0].Amount=="0.00","unavailable wallet keeps its reported zero");
        var partial=Decode("DeepSeek","{\"balance_infos\":[{\"currency\":\"EUR\",\"total_balance\":\"5.00\"},{\"currency\":\"CNY\",\"total_balance\":\"7.00\"}]}");
        Check(partial.Status=="Live"&&partial.Balances.Count==1&&partial.Balances[0].Amount=="7.00","unknown currency row dropped without hiding the valid row");
        foreach(string invalid in new[]{"{\"balance_infos\":[]}","{\"balance_infos\":[{\"currency\":\"CNY\",\"total_balance\":\"NaN\"}]}","{\"balance_infos\":[{\"currency\":\"CNY\",\"total_balance\":\"-1.00\"}]}","{\"balance_infos\":[{\"currency\":\"CNY\",\"total_balance\":1.5}]}","{\"balance_infos\":[{\"currency\":\"CNY\",\"total_balance\":\"1e9\"}]}","{\"balance_infos\":\"not-a-list\"}"})Check(Decode("DeepSeek",invalid).Status!="Live","invalid balance response must not report live");
        Check(!Decode("Claude","{\"five_hour\":{\"utilization\":-1}}").Windows[0].Remaining.HasValue,"invalid percent");Check(!Decode("Claude","{\"five_hour\":{\"utilization\":\"0\"}}").Windows[0].Remaining.HasValue,"numeric strings rejected");
        Check(QuotaDecoder.ResetText(DateTimeOffset.UtcNow.AddSeconds(-1),DateTimeOffset.UtcNow)=="Reset pending","reset does not fabricate quota");
        Check(QuotaDecoder.Number(double.NaN)==null&&QuotaDecoder.Number(double.PositiveInfinity)==null,"non-finite values unavailable");
        Check(QuotaDecoder.Time(1800000000000L)==QuotaDecoder.Time(1800000000L),"milliseconds and seconds share reset time");
        Check(QuotaDecoder.Time(double.MaxValue)==null&&QuotaDecoder.Time("invalid")==null,"out-of-range and malformed reset unavailable");
        var now=new DateTimeOffset(2026,9,15,0,0,0,TimeSpan.Zero);
        Check(QuotaDecoder.ResetText(null,now)==""&&QuotaDecoder.ResetText(now.AddSeconds(1),now)=="Reset 1m","missing reset and minimum minute");
        Check(QuotaDecoder.ResetText(now.AddHours(2).AddMinutes(3),now)=="Reset 2h 3m"&&QuotaDecoder.ResetText(now.AddDays(2).AddHours(3),now)=="Reset 2d 3h","reset formatting");
        Console.WriteLine("PASS Core quota: provider fixtures, compact/full windows, missing/zero, disabled, aliases, deduplication and reset semantics");
    }
    static void ArgonIndependentWindow(){
        string gemini="{\"displayName\":\"Gemini Models\",\"buckets\":[{\"window\":\"weekly\",\"remainingFraction\":0.54},{\"window\":\"5h\",\"remainingFraction\":0.73}]}";
        string argon="{\"displayName\":\"Gemini 4 Argon\",\"buckets\":[{\"bucketId\":\"gemini-argon-wtu-weekly\",\"window\":\"weekly\",\"remainingFraction\":0,\"resetTime\":\"2026-10-23T12:00:00Z\"}]}";
        foreach(string groups in new[]{gemini+","+argon,argon+","+gemini}){
            var reading=Decode("Antigravity","{\"groups\":["+groups+"]}");
            Check(reading.Status=="Live"&&reading.Windows.Count==3,"Argon weekly must survive compact decoding independently of Gemini");
            Check(reading.Windows.Select(w=>w.Label).SequenceEqual(new[]{"5-hour","Weekly","Argon · Weekly"}),"Compact window order must not depend on group order");
            Check(reading.Windows[2].Remaining==0&&reading.Windows[2].Reset==DateTimeOffset.Parse("2026-10-23T12:00:00Z"),"Argon zero and independent reset must remain intact");
            Check(reading.AllWindows.Count==3&&!reading.AllWindows.Any(w=>w.Label.Contains("Argon")&&w.Label.Contains("5-hour")),"Do not invent an Argon five-hour window");
        }
        var invalid=Decode("Antigravity","{\"groups\":["+gemini+","+argon.Replace("\"remainingFraction\":0","\"remainingFraction\":2")+"]}");
        Check(invalid.Status=="Live"&&invalid.Windows.Count==3&&!invalid.Windows[2].Remaining.HasValue,"Invalid Argon must not erase valid Gemini windows or become zero");
    }
    static void LocalGenerationSpeed(){
        var type=typeof(Reading).Assembly.GetType("HardwarePulse.LocalInferenceMetrics");Check(type!=null,"Local inference speed requires a source-independent Core decoder");
        var decode=type.GetMethod("Decode");var now=new DateTimeOffset(2026,10,16,10,0,0,TimeSpan.Zero);
        var live=new Dictionary<string,object>{{"state","generating"},{"tok_s",59.5},{"prefill_tok_s_mean",9000d}};
        var body=new Dictionary<string,object>{{"time",now.ToString("o")},{"live",live}};
        body["requests"]=new object[]{new Dictionary<string,object>{{"decode_tok_s",41d}}};
        Func<object,object> read=value=>decode.Invoke(null,new object[]{"Strata",value,now});
        Func<object,string,object> field=(value,name)=>value.GetType().GetField(name).GetValue(value);
        var generated=read(body);Check((string)field(generated,"Status")=="Generating"&&(double?)field(generated,"TokensPerSecond")==59.5,"Use current decode speed, not prefill or previous request speed");
        live["state"]="idle";var idle=read(body);Check((string)field(idle,"Status")=="Idle"&&field(idle,"TokensPerSecond")==null&&(double?)field(idle,"LastTokensPerSecond")==41,"Idle must not claim the previous request is live");
        live["state"]="reading";Check(field(read(body),"TokensPerSecond")==null,"Prompt processing must not become generation speed");
        live["state"]="generating";
        foreach(object invalid in new object[]{-1d,double.NaN,double.PositiveInfinity,"59.5",null}){live["tok_s"]=invalid;Check(field(read(body),"TokensPerSecond")==null,"Invalid speed must not become a value");}
        live["tok_s"]=0d;Check((double?)field(read(body),"TokensPerSecond")==0,"A real generating zero is valid");
        body["time"]=now.AddSeconds(-6).ToString("o");Check((string)field(read(body),"Status")=="Unavailable","Stale server metrics must not appear live or idle");
        body["time"]=now.AddSeconds(3).ToString("o");Check((string)field(read(body),"Status")=="Unavailable","Future server time must not produce a fresh reading");
        Check((string)field(decode.Invoke(null,new object[]{"Unknown",body,now}),"Status")=="Unavailable","Unknown backends must not silently use Strata semantics");
        body["time"]=now.ToString("o");live["state"]="unloaded";live["tok_s"]=59.5;
        Check((string)field(read(body),"Status")=="Model unloaded"&&field(read(body),"TokensPerSecond")==null,"Unloaded is not idle or zero and must not retain a live speed");
    }
    static void LocalInferenceLifecycle(){
        var type=typeof(Reading).Assembly.GetType("HardwarePulse.LocalInferenceSession");Check(type!=null,"Local inference polling requires cancellation and source-version isolation");
        var now=DateTimeOffset.UtcNow;int calls=0;var release=new System.Threading.ManualResetEvent(false);
        Func<string,string,System.Threading.CancellationToken,LocalInferenceReading> reader=(provider,endpoint,cancel)=>{int call=System.Threading.Interlocked.Increment(ref calls);if(call==1)release.WaitOne();return new LocalInferenceReading{Provider=provider,Status="Generating",Observed=now,SourceTime=now,TokensPerSecond=call==1?59.5:22};};
        using(var session=(IDisposable)Activator.CreateInstance(type,new object[]{reader})){
            Action<bool> enable=value=>type.GetMethod("Configure").Invoke(session,new object[]{value,"Strata","http://127.0.0.1:8080"});
            Action tick=()=>type.GetMethod("Tick").Invoke(session,new object[]{now});
            Func<LocalInferenceReading> current=()=>(LocalInferenceReading)type.GetMethod("Current").Invoke(session,new object[]{now});
            enable(false);tick();Check(calls==0,"Disabled inference must not read any endpoint");enable(true);tick();
            for(int i=0;i<200&&calls==0;i++)System.Threading.Thread.Sleep(2);
            Check(calls==1,"One inference request starts");enable(false);enable(true);tick();Check(calls==1,"Re-enable must not overlap a retiring request");
            Check(current().TokensPerSecond==null,"Switching invalidates current metrics immediately");release.Set();
            for(int i=0;i<300;i++){tick();if(current().TokensPerSecond==22)break;System.Threading.Thread.Sleep(2);}
            Check(calls==2&&current().TokensPerSecond==22,"A cancelled old response must never replace the new reading");
            var expired=(LocalInferenceReading)type.GetMethod("Current").Invoke(session,new object[]{now.AddSeconds(6)});Check(expired.Status=="Unavailable"&&expired.TokensPerSecond==null,"The host expires metrics even when polling stalls");
            enable(false);Check(current().TokensPerSecond==null,"Disabling clears all numeric output");
        }
        release.Dispose();
    }
    static Dictionary<string,object> StatusBody(object used,object reset){return new Dictionary<string,object>{{"rate_limits",new Dictionary<string,object>{{"five_hour",new Dictionary<string,object>{{"used_percentage",used},{"resets_at",reset}}}}},{"transcript_path","must-not-survive"}};}
    static void StatusLineSnapshots(){
        var now=new DateTimeOffset(2026,9,20,0,0,0,TimeSpan.Zero);
        double reset=(now.AddHours(1)-new DateTimeOffset(1970,1,1,0,0,0,TimeSpan.Zero)).TotalSeconds;
        var body=StatusBody(0,reset);
        var snapshot=ClaudeStatusLineSnapshot.Decode(body,now,null);
        var reading=snapshot.Read(now);
        Check(reading.Status=="CLI snapshot"&&reading.Source=="CLI snapshot"&&reading.Windows.Single().Remaining==100,"status-line zero used is full remaining, never Live");
        reading.Windows[0].Remaining=3;
        reading.AllWindows[0].Remaining=4;
        body.Clear();
        Check(snapshot.Read(now).Windows[0].Remaining==100,"snapshot does not retain input graph or expose mutable windows");
        var repeat=ClaudeStatusLineSnapshot.Decode(StatusBody(0,reset),now.AddMinutes(9),snapshot);
        Check(repeat.Received==now&&repeat.Read(now.AddMinutes(10)).Status=="Quota stale"&&repeat.Read(now.AddMinutes(10)).Windows.Count==0,"repeat render cannot refresh age; exact expiry hides values");
        Check(snapshot.Read(now.AddSeconds(-1)).Windows.Count==0,"clock rollback cannot produce fresh values");
        Check(snapshot.Read(now.AddHours(1)).Windows.Count==0,"past reset never fabricates a replenished window");
        var changed=ClaudeStatusLineSnapshot.Decode(StatusBody(100,reset),now.AddMinutes(9),repeat);
        Check(changed.Received==now.AddMinutes(9)&&changed.Read(now.AddMinutes(9)).Windows[0].Remaining==0,"changed payload records new reception, exhausted quota remains zero");
        foreach(object invalid in new object[]{-1,101,"0",double.NaN,double.PositiveInfinity,null})
            Check(ClaudeStatusLineSnapshot.Decode(StatusBody(invalid,reset),now,null).Read(now).Windows.Count==0,"invalid status-line percentage");
        foreach(object invalid in new object[]{reset*1000,reset+0.5,"1800000000",double.MaxValue,reset-3600,reset+18000,null})
            Check(ClaudeStatusLineSnapshot.Decode(StatusBody(0,invalid),now,null).Read(now).Windows.Count==0,"reset requires future integral Unix seconds within window");
        Check(ClaudeStatusLineSnapshot.Decode(null,now,snapshot).Read(now).Windows.Count==0,"missing input removes prior values");
        var weekly=new Dictionary<string,object>{{"rate_limits",new Dictionary<string,object>{{"seven_day",new Dictionary<string,object>{{"used_percentage",12.5},{"resets_at",reset+86400}}},{"seven_day_sonnet",new Dictionary<string,object>{{"used_percentage",0},{"resets_at",reset}}}}}};
        var week=ClaudeStatusLineSnapshot.Decode(weekly,now,null).Read(now);
        Check(week.Windows.Count==1&&week.Windows[0].Label=="Weekly"&&week.Windows[0].Remaining==87.5,"only documented windows retained; fractional percentages preserved");
        var both=StatusBody(0,reset);
        var limits=(Dictionary<string,object>)both["rate_limits"];
        limits.Add("seven_day",new Dictionary<string,object>{{"used_percentage",10},{"resets_at",reset+86400}});
        var original=ClaudeStatusLineSnapshot.Decode(both,now,null);
        ((Dictionary<string,object>)limits["seven_day"])["used_percentage"]=20;
        var partial=ClaudeStatusLineSnapshot.Decode(both,now.AddMinutes(9),original).Read(now.AddMinutes(10));
        Check(partial.Windows.Count==1&&partial.Windows[0].Label=="Weekly"&&partial.Observed==now.AddMinutes(9),"changed Weekly cannot freshen unchanged 5-hour snapshot");
        Console.WriteLine("PASS Claude status-line snapshot: whitelist, zero/full, invalid/reset, repeat age, stale and mutation isolation");
    }
}
