using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HardwarePulse {
    // UI-thread owned orchestration; adapters execute on workers. No files or credentials retained here.
    public sealed class QuotaSession:IDisposable {
        sealed class Slot {internal bool Enabled;internal bool RefreshQueued;internal CancellationTokenSource Cancel;internal Task<QuotaReading> Pending;internal DateTimeOffset Next;internal QuotaReading Reading;internal QuotaReading LastGood;internal QuotaRefreshState State=new QuotaRefreshState();internal int Version;internal int PendingVersion;internal int RecoveryFailures;internal int RateLimitFailures;internal DateTimeOffset? Started;internal DateTimeOffset? LastSuccess;internal double? LastDurationMilliseconds;internal bool TimedOut;internal bool TimeoutReported;}
        readonly Dictionary<string,Slot> slots=new Dictionary<string,Slot>();
        readonly Func<string,CancellationToken,QuotaReading> read;
        bool disposed;
        public static readonly string[] Providers={"Codex","Antigravity","Claude"};
        public event Action<QuotaAttempt> AttemptCompleted;
        public QuotaSession(Func<string,CancellationToken,QuotaReading> reader){read=reader;foreach(string provider in Providers)slots.Add(provider,new Slot{Reading=new QuotaReading{Provider=provider,Status="Refresh pending"}});}
        public QuotaReading[] Readings {get{return slots.Values.Where(s=>s.Enabled).Select(s=>s.Reading).ToArray();}}
        public QuotaRefreshState GetState(string provider){var s=slots[provider];s.State.NextAttempt=s.Next==DateTimeOffset.MinValue?(DateTimeOffset?)null:s.Next;s.State.Started=s.Started;s.State.LastSuccess=s.LastSuccess;s.State.Refreshing=s.Pending!=null;s.State.TimedOut=s.TimedOut;s.State.RateLimitFailures=s.RateLimitFailures;s.State.LastDurationMilliseconds=s.LastDurationMilliseconds;s.State.LastGood=s.LastGood;return s.State;}
        public QuotaReading CachedReading(string provider,DateTimeOffset now){if(provider!="Claude")return null;var s=slots[provider];var current=s.Reading;if(current==null||current.Status!="Refresh rate limited"||s.LastGood==null||s.LastGood.Status!="Live")return null;if(string.IsNullOrEmpty(current.CacheScope)||current.CacheScope!=s.LastGood.CacheScope||current.Source!=s.LastGood.Source)return null;if(s.LastGood.Observed>now||now-s.LastGood.Observed>=TimeSpan.FromMinutes(10))return null;return s.LastGood;}
        public void Enable(string provider,bool enabled){var slot=slots[provider];if(slot.Enabled==enabled)return;slot.Enabled=enabled;slot.RefreshQueued=false;slot.RecoveryFailures=0;slot.RateLimitFailures=0;slot.LastGood=null;slot.Started=null;slot.LastSuccess=null;slot.TimedOut=false;slot.TimeoutReported=false;slot.Version++;if(slot.Cancel!=null)slot.Cancel.Cancel();slot.Reading=new QuotaReading{Provider=provider,Status="Refresh pending"};slot.Next=DateTimeOffset.MinValue;}
        public void Tick(DateTimeOffset now){if(disposed)return;foreach(var pair in slots){var slot=pair.Value;
            if(slot.Pending!=null&&slot.Started.HasValue&&now-slot.Started.Value>=TimeSpan.FromSeconds(30)&&(!slot.Pending.IsCompleted||(slot.Pending.Status!=TaskStatus.RanToCompletion&&slot.Cancel!=null&&slot.Cancel.IsCancellationRequested))){if(!slot.TimeoutReported){slot.TimedOut=true;slot.TimeoutReported=true;if(slot.Cancel!=null)slot.Cancel.Cancel();Emit(new QuotaAttempt{Provider=pair.Key,Status="Quota unavailable",FailureKind="Request timeout",Started=slot.Started.Value,Completed=now,DurationMilliseconds=(now-slot.Started.Value).TotalMilliseconds,NextAttempt=null,RateLimitFailures=slot.RateLimitFailures});}}
            if(slot.Pending!=null&&slot.Pending.IsCompleted){
                var started=slot.Started??now;var completed=now;var duration=(completed-started).TotalMilliseconds;QuotaReading result=null;
                if(!slot.TimedOut&&slot.Pending.Status==TaskStatus.RanToCompletion)result=slot.Pending.Result;
                else{var ignored=slot.Pending.Exception;result=new QuotaReading{Provider=pair.Key,Status="Quota unavailable",Observed=now};}
                if(result==null)result=new QuotaReading{Provider=pair.Key,Status="Quota unavailable",Observed=now};
                if(slot.TimedOut)result.FailureKind="Request timeout";
                if(slot.Enabled&&slot.Version==slot.PendingVersion){
                    slot.Reading=result;
                    if(result.Status=="Live"&&!slot.TimedOut){
                        if(pair.Key=="Claude"&&!string.IsNullOrEmpty(result.CacheScope)){slot.LastGood=Copy(result);slot.State.LastGood=slot.LastGood;}else if(pair.Key=="Claude")slot.LastGood=null;slot.LastSuccess=result.Observed==default(DateTimeOffset)?now:result.Observed;slot.RateLimitFailures=0;
                    } else if(result.Status=="Login required"||result.Status=="Quota access denied"||string.IsNullOrEmpty(result.CacheScope)||(slot.LastGood!=null&&(result.CacheScope!=slot.LastGood.CacheScope||result.Source!=slot.LastGood.Source))){slot.LastGood=null;}
                    if(result.Status=="Refresh rate limited")slot.RateLimitFailures=Math.Min(4,slot.RateLimitFailures+1);else if(result.Status!="Live")slot.RateLimitFailures=0;
                    slot.LastDurationMilliseconds=duration;
                    slot.TimedOut=false;slot.TimeoutReported=false;
                }
                if(slot.Enabled&&slot.Version==slot.PendingVersion) {
                    // Retry transient failures and Claude owner-login recovery quickly,
                    // sharing one budget so alternating errors cannot reset backoff.
                    // Persistent failures cap at normal cadence; local snapshots are cheap.
                    bool overdue=slot.Reading.Status=="Live"&&slot.Reading.Observed!=default(DateTimeOffset)&&now-slot.Reading.Observed>=TimeSpan.FromMinutes(5)&&now>=slot.Next;
                    bool local=slot.Reading.Source=="CLI snapshot";
                    bool recovering=!local&&(slot.Reading.Status=="Quota unavailable"||(pair.Key=="Claude"&&slot.Reading.Status=="Login required"));
                    slot.RecoveryFailures=recovering?Math.Min(5,slot.RecoveryFailures+1):0;
                    int delay=local?30:recovering?Math.Min(300,30 << (slot.RecoveryFailures-1)):slot.Reading.Status=="Refresh rate limited"?new[]{120,300,600,900}[Math.Min(3,Math.Max(0,slot.RateLimitFailures-1))]:300;
                    slot.Next=now.AddSeconds(delay);
                    if(slot.Reading.Status=="Refresh rate limited"&&slot.Reading.RetryAt.HasValue&&slot.Reading.RetryAt.Value>slot.Next)slot.Next=slot.Reading.RetryAt.Value;
                    // A completed pre-sleep observation must not postpone wake recovery.
                    if(overdue)slot.Next=now;
                    if(slot.RefreshQueued&&slot.Reading.Status!="Refresh rate limited")slot.Next=now;
                    Emit(new QuotaAttempt{Provider=pair.Key,Status=slot.Reading.Status,Source=slot.Reading.Source,FailureKind=slot.Reading.FailureKind,HttpStatus=slot.Reading.HttpStatus,Started=started,Completed=completed,DurationMilliseconds=duration,RetryAt=slot.Reading.RetryAt,NextAttempt=slot.Next,RateLimitFailures=slot.RateLimitFailures});
                }
                slot.RefreshQueued=false;slot.Pending=null;slot.Started=null;slot.Cancel.Dispose();slot.Cancel=null;
            }
            if(!slot.Enabled||slot.Pending!=null||now<slot.Next)continue;
            if(slot.Reading.Status=="Refresh rate limited"&&slot.Reading.RetryAt.HasValue&&now<slot.Reading.RetryAt.Value)continue;
            slot.Next=now.AddMinutes(5);slot.Cancel=new CancellationTokenSource(TimeSpan.FromSeconds(30));slot.PendingVersion=slot.Version;slot.Started=now;slot.TimedOut=false;slot.TimeoutReported=false;string provider=pair.Key;var token=slot.Cancel.Token;
            var source=slot.Cancel;slot.Pending=Task.Run(()=>ExecuteRead(provider,token,source));
        }}
        public void Refresh(){if(disposed)return;foreach(var slot in slots.Values){if(!slot.Enabled)continue;if(slot.Pending==null){if(slot.Reading.Status!="Refresh rate limited")slot.Next=DateTimeOffset.MinValue;}else slot.RefreshQueued=true;}}
        public void Dispose(){if(disposed)return;disposed=true;foreach(var slot in slots.Values){if(slot.Cancel!=null){slot.Cancel.Cancel();var source=slot.Cancel;if(slot.Pending!=null)slot.Pending.ContinueWith(t=>{var ignored=t.Exception;source.Dispose();},TaskScheduler.Default);else source.Dispose();}}}
        void Emit(QuotaAttempt attempt){var handler=AttemptCompleted;if(handler==null)return;foreach(Action<QuotaAttempt> subscriber in handler.GetInvocationList()){try{subscriber(attempt);}catch{}}}
        static QuotaReading Copy(QuotaReading source){if(source==null)return null;var copy=new QuotaReading{Provider=source.Provider,Status=source.Status,Source=source.Source,FailureKind=source.FailureKind,CacheScope=source.CacheScope,HttpStatus=source.HttpStatus,Observed=source.Observed,RetryAt=source.RetryAt};foreach(var w in source.Windows)copy.Windows.Add(new QuotaWindow{Label=w.Label,Remaining=w.Remaining,Reset=w.Reset});foreach(var w in source.AllWindows)copy.AllWindows.Add(new QuotaWindow{Label=w.Label,Remaining=w.Remaining,Reset=w.Reset});return copy;}
        QuotaReading ExecuteRead(string provider,CancellationToken token,CancellationTokenSource source){try{var result=read(provider,token);token.ThrowIfCancellationRequested();return result;}finally{try{source.CancelAfter(Timeout.Infinite);}catch(ObjectDisposedException){}}}
    }
}
