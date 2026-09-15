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
}