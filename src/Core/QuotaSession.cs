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
        static readonly TimeSpan NormalRefresh=TimeSpan.FromMinutes(5);
        static readonly TimeSpan TransientRetry=TimeSpan.FromSeconds(30);
        static readonly TimeSpan RateLimitRetry=TimeSpan.FromMinutes(2);
        public static readonly string[] Providers={"Codex","Antigravity","Claude"};
        public QuotaSession(Func<string,CancellationToken,QuotaReading> reader){read=reader;foreach(string provider in Providers)slots.Add(provider,new Slot{Reading=new QuotaReading{Provider=provider,Status="Refresh pending"}});}
        public QuotaReading[] Readings {get{return slots.Values.Where(s=>s.Enabled).Select(s=>s.Reading).ToArray();}}
        public void Enable(string provider,bool enabled){var slot=slots[provider];if(slot.Enabled==enabled)return;slot.Enabled=enabled;slot.Version++;if(slot.Cancel!=null)slot.Cancel.Cancel();slot.Reading=new QuotaReading{Provider=provider,Status="Refresh pending"};slot.Next=DateTimeOffset.MinValue;}
        public void Tick(DateTimeOffset now){if(disposed)return;foreach(var pair in slots){var slot=pair.Value;
            if(slot.Pending!=null&&slot.Pending.IsCompleted){
                if(slot.Enabled&&slot.Version==slot.PendingVersion){
                    if(slot.Pending.Status==TaskStatus.RanToCompletion){
                        var result=slot.Pending.Result;
                        if(IsTransient(result.Status)&&HasQuota(slot.Reading))slot.Reading=Preserve(slot.Reading,"Last update failed");
                        else slot.Reading=result;
                        slot.Next=now.Add(result.Status=="Refresh rate limited"?RateLimitRetry:IsTransient(result.Status)?TransientRetry:NormalRefresh);
                    }else{
                        var ignored=slot.Pending.Exception;
                        slot.Reading=HasQuota(slot.Reading)?Preserve(slot.Reading,"Last update failed"):new QuotaReading{Provider=pair.Key,Status="Quota unavailable",Observed=now};
                        slot.Next=now.Add(TransientRetry);
                    }
                }else{var ignored=slot.Pending.Exception;}
                slot.Pending=null;slot.Cancel.Dispose();slot.Cancel=null;
            }
            if(!slot.Enabled||slot.Pending!=null||now<slot.Next)continue;
            slot.Cancel=new CancellationTokenSource(TimeSpan.FromSeconds(30));slot.PendingVersion=slot.Version;string provider=pair.Key;var token=slot.Cancel.Token;
            slot.Pending=Task.Run(()=>read(provider,token));
        }}
        static bool IsTransient(string status){return status=="Quota unavailable"||status=="Refresh rate limited";}
        static bool HasQuota(QuotaReading reading){return reading.Windows.Count>0||reading.AllWindows.Count>0;}
        static QuotaReading Preserve(QuotaReading reading,string status){return new QuotaReading{Provider=reading.Provider,Status=status,Observed=reading.Observed,
            Windows=new List<QuotaWindow>(reading.Windows),AllWindows=new List<QuotaWindow>(reading.AllWindows)};}
        public void Refresh(){foreach(var slot in slots.Values)if(slot.Pending==null)slot.Next=DateTimeOffset.MinValue;}
        public void Dispose(){if(disposed)return;disposed=true;foreach(var slot in slots.Values){if(slot.Cancel!=null){slot.Cancel.Cancel();var source=slot.Cancel;if(slot.Pending!=null)slot.Pending.ContinueWith(t=>{var ignored=t.Exception;source.Dispose();},TaskScheduler.Default);else source.Dispose();}}}
    }
}
