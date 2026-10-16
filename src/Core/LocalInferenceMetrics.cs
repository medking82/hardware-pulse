using System;
using System.Linq;

namespace HardwarePulse {
    // Not a quota: backend-reported output-token throughput, never prefill or a benchmark.
    public sealed class LocalInferenceReading {
        public string Provider="Strata",Status="Unavailable";
        public DateTimeOffset Observed;
        public DateTimeOffset? SourceTime;
        public double? TokensPerSecond,LastTokensPerSecond;
        public bool Fresh(DateTimeOffset now){return SourceTime.HasValue&&(now-SourceTime.Value).TotalSeconds>=-2&&(now-SourceTime.Value).TotalSeconds<=5;}
    }
    public static class LocalInferenceMetrics {
        static double? Rate(object value){var n=QuotaDecoder.Number(value);return n.HasValue&&n>=0&&n<=10000000?n:null;}
        public static LocalInferenceReading Decode(string provider,object body,DateTimeOffset now){
            var result=new LocalInferenceReading{Provider=provider,Observed=now};
            // Each backend must explicitly supply its own mapping; model/software names
            // do not imply a common telemetry protocol.
            if(provider!="Strata")return result;
            result.SourceTime=QuotaDecoder.Time(QuotaDecoder.Get(body,"time"));
            if(!result.Fresh(now))return result;
            var live=QuotaDecoder.Get(body,"live");
            switch(QuotaDecoder.Text(QuotaDecoder.Get(live,"state"))){
                case "generating":result.Status="Generating";result.TokensPerSecond=Rate(QuotaDecoder.Get(live,"tok_s"));break;
                case "reading":result.Status="Reading prompt";break;
                case "idle":result.Status="Idle";break;
                case "unloaded":result.Status="Model unloaded";break;
                case "loading":result.Status="Loading model";break;
                default:return result;
            }
            // History is informational only. It never populates the live numeric cell,
            // and cannot be attributed to the currently loaded model.
            if(result.Status=="Idle")result.LastTokensPerSecond=Rate(QuotaDecoder.Get(QuotaDecoder.Items(QuotaDecoder.Get(body,"requests")).FirstOrDefault(),"decode_tok_s"));
            return result;
        }
    }
}
