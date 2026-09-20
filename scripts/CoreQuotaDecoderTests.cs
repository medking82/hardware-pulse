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
