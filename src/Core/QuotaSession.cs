using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HardwarePulse {
    // UI-thread owned orchestration; adapters execute on workers. No files or credentials retained here.
    public sealed class QuotaSession:IDisposable {
        sealed class Slot {internal bool Enabled;internal CancellationTokenSource Cancel;internal Task<QuotaReading> Pending;internal DateTimeOffset Next;internal QuotaReading Reading;internal int Version;internal int PendingVersion;}
        readonly Dictionary<string,Slot> slots=new Dictionary<string,Slot>();
        readonly Func<string,CancellationToken,QuotaReading> read;
        bool disposed;
        public static readonly string[] Providers={"Codex","Antigravity","Claude"};
        public QuotaSession(Func<string,CancellationToken,QuotaReading> reader){read=reader;foreach(string provider in Providers)slots.Add(provider,new Slot{Reading=new QuotaReading{Provider=provider,Status="Refresh pending"}});}
        public QuotaReading[] Readings {get{return slots.Values.Where(s=>s.Enabled).Select(s=>s.Reading).ToArray();}}
        public void Enable(string provider,bool enabled){var slot=slots[provider];if(slot.Enabled==enabled)return;slot.Enabled=enabled;slot.Version++;if(slot.Cancel!=null)slot.Cancel.Cancel();slot.Reading=new QuotaReading{Provider=provider,Status="Refresh pending"};slot.Next=DateTimeOffset.MinValue;}
        public void Tick(DateTimeOffset now){if(disposed)return;foreach(var pair in slots){var slot=pair.Value;
            if(slot.Pending!=null&&slot.Pending.IsCompleted){
                if(slot.Pending.Status==TaskStatus.RanToCompletion&&slot.Enabled&&slot.Version==slot.PendingVersion)slot.Reading=slot.Pending.Result;
                else{var ignored=slot.Pending.Exception;if(slot.Enabled&&slot.Version==slot.PendingVersion)slot.Reading=new QuotaReading{Provider=pair.Key,Status="Quota unavailable",Observed=now};}
                if(slot.Enabled&&slot.Version==slot.PendingVersion) {
                    // Transient transport failures retry sooner; authentication stays
                    // on the normal cadence and rate limiting receives its own backoff.
                    slot.Next=now.AddSeconds(slot.Reading.Status=="Quota unavailable"?30:slot.Reading.Status=="Refresh rate limited"?120:300);
                }
                slot.Pending=null;slot.Cancel.Dispose();slot.Cancel=null;
            }
            if(!slot.Enabled||slot.Pending!=null||now<slot.Next)continue;
            slot.Next=now.AddMinutes(5);slot.Cancel=new CancellationTokenSource(TimeSpan.FromSeconds(30));slot.PendingVersion=slot.Version;string provider=pair.Key;var token=slot.Cancel.Token;
            slot.Pending=Task.Run(()=>read(provider,token));
        }}
        public void Refresh(){foreach(var slot in slots.Values)if(slot.Pending==null)slot.Next=DateTimeOffset.MinValue;}
        public void Dispose(){if(disposed)return;disposed=true;foreach(var slot in slots.Values){if(slot.Cancel!=null){slot.Cancel.Cancel();var source=slot.Cancel;if(slot.Pending!=null)slot.Pending.ContinueWith(t=>{var ignored=t.Exception;source.Dispose();},TaskScheduler.Default);else source.Dispose();}}}
    }
}
