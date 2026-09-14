using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;

namespace HardwarePulse {
    public sealed class QuotaWindow {
        public string Label;
        public double? Remaining;
        public DateTimeOffset? Reset;
    }
    public sealed class QuotaReading {
        public string Provider,Status="Quota unavailable";
        public DateTimeOffset Observed;
        public List<QuotaWindow> Windows=new List<QuotaWindow>();
    }
    // Response-shape mapping adapted from Token Monitor (MIT); see licenses/TokenMonitor.txt.
    public static class QuotaData {
        public static Dictionary<string,object> Parse(string text){return new JavaScriptSerializer{MaxJsonLength=1048576,RecursionLimit=32}.Deserialize<Dictionary<string,object>>(text);}
        public static object Get(object value,params string[] keys){var map=value as Dictionary<string,object>;if(map!=null)foreach(string key in keys){object result;if(map.TryGetValue(key,out result)&&result!=null)return result;}return null;}
        public static string Text(object value){return value as string??"";}
        public static IEnumerable<object> Items(object value){var list=value as IEnumerable;return list==null||value is string||value is IDictionary?Enumerable.Empty<object>():list.Cast<object>();}
        public static double? Number(object value){if(!(value is int||value is long||value is double||value is decimal||value is float))return null;double n=Convert.ToDouble(value,CultureInfo.InvariantCulture);return double.IsNaN(n)||double.IsInfinity(n)?(double?)null:n;}
        public static DateTimeOffset? Time(object value){try{var n=Number(value);if(n.HasValue)return new DateTimeOffset(1970,1,1,0,0,0,TimeSpan.Zero).AddSeconds(n.Value>20000000000?n.Value/1000:n.Value);DateTimeOffset parsed;return DateTimeOffset.TryParse(Text(value),CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out parsed)?parsed:(DateTimeOffset?)null;}catch(ArgumentOutOfRangeException){return null;}}
        static void Add(QuotaReading result,string label,object percent,object reset,bool remaining){
            var n=Number(percent);double? value=n.HasValue&&n>=0&&n<=100?(remaining?n:100-n):null;
            result.Windows.Add(new QuotaWindow{Label=label.Length>100?label.Substring(0,100):label,Remaining=value,Reset=Time(reset)});
        }
        static void CodexPool(QuotaReading r,string label,object pool){foreach(string key in new[]{"primary","secondary"}){
            var w=Get(pool,key,key+"_window",key+"Window");if(w==null)continue;
            var minutes=Number(Get(w,"windowDurationMins","window_duration_mins"));var seconds=Number(Get(w,"limit_window_seconds","limitWindowSeconds"));if(!minutes.HasValue&&seconds.HasValue)minutes=seconds/60;
            string period=minutes==300?"5-hour":minutes==10080?"Weekly":minutes.HasValue?minutes.Value.ToString("0",CultureInfo.InvariantCulture)+" min":key;
            Add(r,(label==""?"":label+" · ")+period,Get(w,"used_percent","usedPercent"),Get(w,"reset_at","resetAt","resets_at","resetsAt"),false);
        }}
        public static QuotaReading Decode(string provider,object body,DateTimeOffset now){
            var r=new QuotaReading{Provider=provider,Observed=now};
            if(provider=="Codex"){
                var byId=Get(body,"rateLimitsByLimitId","rate_limits_by_limit_id") as Dictionary<string,object>;
                object main=null;if(byId!=null)byId.TryGetValue("codex",out main);
                CodexPool(r,"",main??Get(body,"rate_limit","rateLimit","rateLimits","rate_limits"));
                r.Windows.RemoveAll(window=>window.Label!="Weekly");
            }else if(provider=="Claude"){
                var map=body as Dictionary<string,object>;if(map!=null)foreach(var entry in map){
                    if(entry.Key!="five_hour"&&!entry.Key.StartsWith("seven_day",StringComparison.Ordinal))continue;
                    if(Get(entry.Value,"utilization")==null&&Get(entry.Value,"resets_at")==null)continue;
                    string label=entry.Key=="five_hour"?"5-hour":entry.Key=="seven_day"?"Weekly":entry.Key.Substring(10).Replace('_',' ')+" · Weekly";
                    if(entry.Value!=null)Add(r,label,Get(entry.Value,"utilization"),Get(entry.Value,"resets_at"),false);
                }
            }else if(provider=="Antigravity"){
                var summary=Get(body,"response","summary")??body;
                foreach(var group in Items(Get(summary,"groups")))foreach(var bucket in Items(Get(group,"buckets"))){
                    string groupName=Text(Get(group,"displayName")).Trim();
                    if(!groupName.Equals("Gemini",StringComparison.OrdinalIgnoreCase)&&!groupName.Equals("Gemini Models",StringComparison.OrdinalIgnoreCase))continue;
                    string window=Text(Get(bucket,"window","bucketId","displayName")).Trim().ToLowerInvariant().Replace('_','-');
                    string label=window=="weekly"?"Weekly":new[]{"session","5h","5-hour","five-hour","five hour"}.Contains(window)?"5-hour":null;
                    if(label==null||r.Windows.Any(w=>w.Label==label))continue;
                    var fraction=Get(bucket,"remainingFraction")??Get(Get(bucket,"remaining"),"remainingFraction");
                    if(fraction==null&&Text(Get(Get(bucket,"remaining"),"case"))=="remainingFraction")fraction=Get(Get(bucket,"remaining"),"value");
                    var n=Number(fraction);object pct=n.HasValue?(object)(n.Value*100):null;if(Equals(Get(bucket,"disabled"),true))pct=null;
                    Add(r,label,pct,Get(bucket,"resetTime"),true);
                }
                r.Windows=r.Windows.OrderBy(w=>w.Label=="5-hour"?0:1).ToList();
            }
            if(r.Windows.Any(w=>w.Remaining.HasValue))r.Status="Live";
            return r;
        }
        public static string ResetText(DateTimeOffset? reset,DateTimeOffset now){if(!reset.HasValue)return "";var span=reset.Value-now;if(span<=TimeSpan.Zero)return "Reset pending";return "Reset "+(span.TotalDays>=1?(int)span.TotalDays+"d "+span.Hours+"h":span.TotalHours>=1?(int)span.TotalHours+"h "+span.Minutes+"m":Math.Max(1,(int)Math.Ceiling(span.TotalMinutes))+"m");}
    }
}
