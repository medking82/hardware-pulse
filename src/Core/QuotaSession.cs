using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HardwarePulse {
    // UI-thread owned orchestration; adapters execute on workers. No files or credentials retained here.
    public sealed class QuotaSession:IDisposable {
        sealed class Slot {
            internal bool Enabled,Local,PendingLocal,RefreshQueued,TimedOut,TimeoutReported;
            internal CancellationTokenSource Cancel;
            internal Task<QuotaReading> Pending;
            internal QuotaReading Reading,LastGood;
            internal QuotaRefreshState State=new QuotaRefreshState();
            internal QuotaSchedule Remote;
            internal DateTimeOffset LocalNext;
            internal DateTimeOffset Next {get{return Local?LocalNext:Remote.NextAttempt;}set{if(Local)LocalNext=value;else Remote.NextAttempt=value;}}
            internal int Version,PendingVersion;
            internal DateTimeOffset? Started,LastSuccess;
            internal double? LastDurationMilliseconds;
        }
        readonly Dictionary<string,Slot> slots=new Dictionary<string,Slot>();
        readonly Func<string,CancellationToken,QuotaReading> read;
        bool disposed;
        public static readonly string[] Providers={"Codex","Antigravity","Claude"};
        public event Action<QuotaAttempt> AttemptCompleted;
        public event Action<QuotaSchedule[]> ScheduleChanged;
        public QuotaSession(Func<string,CancellationToken,QuotaReading> reader){read=reader;foreach(string provider in Providers)slots.Add(provider,new Slot{Reading=new QuotaReading{Provider=provider,Status="Refresh pending"},Remote=new QuotaSchedule{Provider=provider,Status="Refresh pending"}});}
        public QuotaReading[] Readings {get{return slots.Values.Where(s=>s.Enabled).Select(s=>s.Reading).ToArray();}}
        public QuotaRefreshState GetState(string provider){var s=slots[provider];s.State.NextAttempt=s.Next==DateTimeOffset.MinValue?(DateTimeOffset?)null:s.Next;s.State.Started=s.Started;s.State.LastSuccess=s.LastSuccess;s.State.Refreshing=s.Pending!=null;s.State.TimedOut=s.TimedOut;s.State.RateLimitFailures=s.Local?0:s.Remote.RateLimitFailures;s.State.LastDurationMilliseconds=s.LastDurationMilliseconds;s.State.LastGood=s.LastGood;return s.State;}
        public QuotaReading CachedReading(string provider,DateTimeOffset now){if(provider!="Claude")return null;var s=slots[provider];var current=s.Reading;if(current==null||current.Status!="Refresh rate limited"||s.LastGood==null||s.LastGood.Status!="Live")return null;if(string.IsNullOrEmpty(current.CacheScope)||current.CacheScope!=s.LastGood.CacheScope||current.Source!=s.LastGood.Source)return null;if(s.LastGood.Observed>now||now-s.LastGood.Observed>=TimeSpan.FromMinutes(10))return null;return s.LastGood;}
        public QuotaSchedule[] CaptureSchedules(){return slots.Values.Select(s=>CopySchedule(s.Remote)).ToArray();}
        public void RestoreSchedules(IEnumerable<QuotaSchedule> schedules){
            if(disposed||schedules==null)return;
            foreach(var saved in schedules){Slot slot;if(saved==null||saved.Provider==null||!slots.TryGetValue(saved.Provider,out slot)||slot.Pending!=null)continue;
                if(saved.RecoveryFailures<0||saved.RecoveryFailures>4||saved.RateLimitFailures<0||saved.RateLimitFailures>4)continue;
                slot.Remote=CopySchedule(saved);if(slot.Remote.NextAttempt<slot.Remote.NotBefore)slot.Remote.NextAttempt=slot.Remote.NotBefore;
                if(!slot.Local)slot.Reading=RestoredReading(slot.Remote);
            }
        }
        static QuotaReading RestoredReading(QuotaSchedule saved){return new QuotaReading{Provider=saved.Provider,Status=saved.RecoveryFailures>0||saved.RateLimitFailures>0?saved.Status:"Refresh pending",Observed=saved.Observed};}
        public void Enable(string provider,bool enabled,bool localSnapshot=false){
            var slot=slots[provider];if(slot.Enabled==enabled&&slot.Local==localSnapshot)return;
            slot.Enabled=enabled;slot.Local=localSnapshot;slot.RefreshQueued=false;slot.LastGood=null;slot.LastSuccess=null;slot.TimedOut=false;slot.TimeoutReported=false;slot.Version++;
            if(slot.Cancel!=null)slot.Cancel.Cancel();
            slot.Reading=localSnapshot?new QuotaReading{Provider=provider,Status="Refresh pending"}:RestoredReading(slot.Remote);slot.LocalNext=DateTimeOffset.MinValue;
            // Provider/source toggles do not erase a remote request's reservation or cooldown.
        }
        public void Tick(DateTimeOffset now){if(disposed)return;foreach(var pair in slots){var slot=pair.Value;
            if(slot.Pending!=null&&slot.Started.HasValue&&now-slot.Started.Value>=TimeSpan.FromSeconds(30)&&(!slot.Pending.IsCompleted||(slot.Pending.Status!=TaskStatus.RanToCompletion&&slot.Cancel!=null&&slot.Cancel.IsCancellationRequested))){if(!slot.TimeoutReported){slot.TimedOut=true;slot.TimeoutReported=true;if(slot.Cancel!=null)slot.Cancel.Cancel();Emit(new QuotaAttempt{Provider=pair.Key,Status="Quota unavailable",FailureKind="Request timeout",Started=slot.Started.Value,Completed=now,DurationMilliseconds=(now-slot.Started.Value).TotalMilliseconds,NextAttempt=null,RateLimitFailures=slot.Remote.RateLimitFailures});}}
            if(slot.Pending!=null&&slot.Pending.IsCompleted){
                var started=slot.Started??now;var duration=(now-started).TotalMilliseconds;QuotaReading result=null;
                if(slot.Pending.Status==TaskStatus.RanToCompletion)result=slot.Pending.Result;else{var ignored=slot.Pending.Exception;}
                // Preserve a server cooldown even when a toggle/timeout rejects the observation.
                if(result==null||(slot.TimedOut&&result.Status!="Refresh rate limited"))result=new QuotaReading{Provider=pair.Key,Status="Quota unavailable",Observed=now};
                if(slot.TimedOut)result.FailureKind="Request timeout";
                bool accepted=slot.Enabled&&slot.Version==slot.PendingVersion;
                if(!slot.PendingLocal)ScheduleRemote(slot.Remote,result,now,accepted&&!slot.TimedOut);
                if(accepted){
                    slot.Reading=result;
                    if(result.Status=="Live"&&!slot.TimedOut){
                        if(pair.Key=="Claude"&&!string.IsNullOrEmpty(result.CacheScope)){slot.LastGood=Copy(result);slot.State.LastGood=slot.LastGood;}else if(pair.Key=="Claude")slot.LastGood=null;slot.LastSuccess=result.Observed==default(DateTimeOffset)?now:result.Observed;
                    }else if(result.Status=="Login required"||result.Status=="Quota access denied"||string.IsNullOrEmpty(result.CacheScope)||(slot.LastGood!=null&&(result.CacheScope!=slot.LastGood.CacheScope||result.Source!=slot.LastGood.Source))){slot.LastGood=null;}
                    slot.LastDurationMilliseconds=duration;
                    if(slot.Local)slot.Next=now.AddSeconds(30);
                    // A completed pre-sleep observation must not postpone wake recovery.
                    bool overdue=result.Status=="Live"&&result.Observed!=default(DateTimeOffset)&&now-result.Observed>=TimeSpan.FromMinutes(5)&&now>=started.AddMinutes(5);
                    if(overdue){slot.Next=now;if(!slot.Local)slot.Remote.NotBefore=now;}
                    if(slot.RefreshQueued&&(slot.Local||result.Status=="Live"))slot.Next=slot.Local?now:slot.Remote.NotBefore;
                    Emit(new QuotaAttempt{Provider=pair.Key,Status=result.Status,Source=result.Source,FailureKind=result.FailureKind,HttpStatus=result.HttpStatus,Started=started,Completed=now,DurationMilliseconds=duration,RetryAt=result.RetryAt,NextAttempt=slot.Next,RateLimitFailures=slot.Local?0:slot.Remote.RateLimitFailures});
                }
                if(!slot.PendingLocal)EmitSchedule();
                slot.TimedOut=false;slot.TimeoutReported=false;slot.RefreshQueued=false;slot.Pending=null;slot.Started=null;slot.Cancel.Dispose();slot.Cancel=null;
            }
            if(!slot.Enabled||slot.Pending!=null||now<slot.Next||(!slot.Local&&now<slot.Remote.NotBefore))continue;
            slot.Next=now.AddMinutes(5);
            if(!slot.Local){slot.Remote.NotBefore=slot.Next;EmitSchedule();} // Reserve before IO, including process shutdown/crash.
            slot.Cancel=new CancellationTokenSource(TimeSpan.FromSeconds(30));slot.PendingVersion=slot.Version;slot.PendingLocal=slot.Local;slot.Started=now;slot.TimedOut=false;slot.TimeoutReported=false;string provider=pair.Key;var token=slot.Cancel.Token;
            var source=slot.Cancel;slot.Pending=Task.Run(()=>ExecuteRead(provider,token,source));
        }}
        static void ScheduleRemote(QuotaSchedule schedule,QuotaReading reading,DateTimeOffset now,bool accepted){
            bool live=accepted&&reading.Status=="Live";
            schedule.Status=live?"Live":reading.Status=="Live"?"Quota unavailable":reading.Status;schedule.Observed=reading.Observed;
            if(live){schedule.RecoveryFailures=0;schedule.RateLimitFailures=0;schedule.NextAttempt=now.AddMinutes(5);schedule.NotBefore=now.AddSeconds(30);return;}
            schedule.RecoveryFailures=Math.Min(4,schedule.RecoveryFailures+1);
            if(reading.Status=="Refresh rate limited")schedule.RateLimitFailures=Math.Min(4,schedule.RateLimitFailures+1);
            // Only success clears throttling debt; a 401/timeout after 429 must not restore fast polling.
            int minutes=Math.Min(30,5 << (schedule.RecoveryFailures-1));
            if(schedule.RateLimitFailures>0)minutes=Math.Max(minutes,Math.Min(60,15 << (schedule.RateLimitFailures-1)));
            var deadline=now.AddMinutes(minutes);
            if(reading.RetryAt.HasValue&&reading.RetryAt.Value>deadline)deadline=reading.RetryAt.Value;
            if(schedule.NotBefore>deadline)deadline=schedule.NotBefore;
            schedule.NextAttempt=deadline;schedule.NotBefore=deadline;
        }
        public void Refresh(){if(disposed)return;foreach(var slot in slots.Values){if(!slot.Enabled)continue;if(slot.Pending!=null){slot.RefreshQueued=true;continue;}if(slot.Local)slot.Next=DateTimeOffset.MinValue;else if(slot.Remote.RecoveryFailures==0&&slot.Remote.RateLimitFailures==0)slot.Next=slot.Remote.NotBefore;}}
        public void Dispose(){if(disposed)return;disposed=true;foreach(var slot in slots.Values){if(slot.Cancel!=null){slot.Cancel.Cancel();var source=slot.Cancel;if(slot.Pending!=null)slot.Pending.ContinueWith(t=>{var ignored=t.Exception;source.Dispose();},TaskScheduler.Default);else source.Dispose();}}}
        void Emit(QuotaAttempt attempt){var handler=AttemptCompleted;if(handler==null)return;foreach(Action<QuotaAttempt> subscriber in handler.GetInvocationList()){try{subscriber(attempt);}catch{}}}
        void EmitSchedule(){var handler=ScheduleChanged;if(handler==null)return;foreach(Action<QuotaSchedule[]> subscriber in handler.GetInvocationList()){try{subscriber(CaptureSchedules());}catch{}}}
        static QuotaSchedule CopySchedule(QuotaSchedule source){return new QuotaSchedule{Provider=source.Provider,Status=source.Status,Observed=source.Observed,NextAttempt=source.NextAttempt,NotBefore=source.NotBefore,RecoveryFailures=source.RecoveryFailures,RateLimitFailures=source.RateLimitFailures};}
        static QuotaReading Copy(QuotaReading source){if(source==null)return null;var copy=new QuotaReading{Provider=source.Provider,Status=source.Status,Source=source.Source,FailureKind=source.FailureKind,CacheScope=source.CacheScope,HttpStatus=source.HttpStatus,Observed=source.Observed,RetryAt=source.RetryAt};foreach(var w in source.Windows)copy.Windows.Add(new QuotaWindow{Label=w.Label,Remaining=w.Remaining,Reset=w.Reset});foreach(var w in source.AllWindows)copy.AllWindows.Add(new QuotaWindow{Label=w.Label,Remaining=w.Remaining,Reset=w.Reset});return copy;}
        QuotaReading ExecuteRead(string provider,CancellationToken token,CancellationTokenSource source){try{var result=read(provider,token);if(result==null||result.Status!="Refresh rate limited")token.ThrowIfCancellationRequested();return result;}finally{try{source.CancelAfter(Timeout.Infinite);}catch(ObjectDisposedException){}}}
    }
}
