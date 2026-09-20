using System;
using System.Collections.Generic;

namespace HardwarePulse {
    // A status-line render is not proof of a fresh API response. Never label it Live.
    // Only quota values survive decoding; the input graph and session data are not retained.
    public sealed class ClaudeStatusLineSnapshot {
        readonly List<QuotaWindow> windows;
        readonly Dictionary<string,DateTimeOffset> receivedByWindow=new Dictionary<string,DateTimeOffset>();
        public DateTimeOffset Received { get; private set; }
        ClaudeStatusLineSnapshot(List<QuotaWindow> values,DateTimeOffset received){windows=values;Received=received;}

        public static ClaudeStatusLineSnapshot Decode(object body,DateTimeOffset received,ClaudeStatusLineSnapshot previous){
            var values=new List<QuotaWindow>();
            object limits=QuotaDecoder.Get(body,"rate_limits");
            Add(values,limits,"five_hour","5-hour",received,TimeSpan.FromHours(5));
            Add(values,limits,"seven_day","Weekly",received,TimeSpan.FromDays(7));
            // Each window ages independently: a Weekly change cannot freshen 5-hour data.
            var snapshot=new ClaudeStatusLineSnapshot(values,received);
            foreach(var value in values){
                var firstReceived=received;
                if(previous!=null)foreach(var old in previous.windows)
                    if(old.Label==value.Label&&old.Remaining==value.Remaining&&old.Reset==value.Reset){firstReceived=previous.receivedByWindow[old.Label];break;}
                snapshot.receivedByWindow.Add(value.Label,firstReceived);
                if(firstReceived<snapshot.Received)snapshot.Received=firstReceived;
            }
            return snapshot;
        }
        static void Add(List<QuotaWindow> values,object limits,string key,string label,DateTimeOffset now,TimeSpan period){
            object window=QuotaDecoder.Get(limits,key);
            var used=QuotaDecoder.Number(QuotaDecoder.Get(window,"used_percentage"));
            var seconds=QuotaDecoder.Number(QuotaDecoder.Get(window,"resets_at"));
            if(!used.HasValue||used<0||used>100||!seconds.HasValue||seconds<0||seconds!=Math.Truncate(seconds.Value))return;
            DateTimeOffset reset;
            try{reset=new DateTimeOffset(1970,1,1,0,0,0,TimeSpan.Zero).AddSeconds(seconds.Value);}catch(ArgumentOutOfRangeException){return;}
            if(reset<=now||reset-now>period)return;
            values.Add(new QuotaWindow{Label=label,Remaining=100-used.Value,Reset=reset});
        }
        public QuotaReading Read(DateTimeOffset now){
            var result=new QuotaReading{Provider="Claude",Source="CLI snapshot",Observed=Received};
            bool stale=false;
            foreach(var window in windows)if(window.Reset>now){
                var received=receivedByWindow[window.Label];
                if(now<received||now-received>=TimeSpan.FromMinutes(10)){stale=true;continue;}
                if(result.Windows.Count==0||received<result.Observed)result.Observed=received;
                result.Windows.Add(new QuotaWindow{Label=window.Label,Remaining=window.Remaining,Reset=window.Reset});
                result.AllWindows.Add(new QuotaWindow{Label=window.Label,Remaining=window.Remaining,Reset=window.Reset});
            }
            if(result.Windows.Count>0)result.Status="CLI snapshot";
            else if(stale)result.Status="Quota stale";
            return result;
        }
    }
}
