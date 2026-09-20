using System;
using System.Diagnostics;
using System.Threading;
using HardwarePulse;

internal static class CoreQuotaSessionTests {
    static void Check(bool value,string message){if(!value)throw new Exception("Core quota: "+message);}
    static void Pump(QuotaSession session,DateTimeOffset now,Func<bool> ready){
        var elapsed=Stopwatch.StartNew();
        do {session.Tick(now);if(ready())return;Thread.Sleep(1);}while(elapsed.ElapsedMilliseconds<5000);
        throw new Exception("Core quota completion timed out");
    }
    public static void Run(){
        Check(typeof(QuotaSession).Assembly==typeof(QuotaReading).Assembly,"session still depends on app assembly");
        var now=new DateTimeOffset(2026,9,15,0,0,0,TimeSpan.Zero);int calls=0;
        using(var session=new QuotaSession((provider,cancel)=>{
            int count=Interlocked.Increment(ref calls);
            return new QuotaReading {Provider=provider,Status="Live",Observed=now.AddSeconds(count)};
        })){
            session.Tick(now);Check(calls==0&&session.Readings.Length==0,"disabled providers started work");
            session.Enable("Codex",true);
            Pump(session,now,()=>session.Readings[0].Status=="Live");
            session.Tick(now.AddMinutes(5).AddTicks(-1));Check(calls==1,"refresh ran before deadline");
            Pump(session,now.AddMinutes(5),()=>session.Readings[0].Observed==now.AddSeconds(2));
            session.Refresh();Pump(session,now.AddMinutes(5),()=>session.Readings[0].Observed==now.AddSeconds(3));
            Check(calls==3,"manual refresh duplicated requests");
        }
        // A refresh clicked after re-login while an older read is pending must not be lost.
        foreach(string firstStatus in new[]{"Login required","Quota unavailable","Refresh rate limited"}){
            int attempts=0;
            using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim())
            using(var queued=new QuotaSession((provider,cancel)=>{
                int attempt=Interlocked.Increment(ref attempts);
                if(attempt==1){entered.Set();Check(release.Wait(5000),"queued fixture was not released");}
                return new QuotaReading{Provider=provider,Status=attempt==1?firstStatus:"Live"};
            })){
                try{
                    queued.Enable("Claude",true);queued.Tick(now);Check(entered.Wait(5000),"queued read did not start");
                    for(int i=0;i<10;i++){queued.Refresh();queued.Tick(now);}
                    Check(attempts==1,"queued clicks overlapped the running read");release.Set();
                    if(firstStatus=="Refresh rate limited"){
                        Pump(queued,now,()=>queued.Readings[0].Status==firstStatus);
                        queued.Tick(now.AddSeconds(119));Check(attempts==1,"queued click bypassed rate-limit backoff");
                    }
                    var retryTime=firstStatus=="Refresh rate limited"?now.AddSeconds(120):now;
                    Pump(queued,retryTime,()=>queued.Readings[0].Status=="Live");
                    queued.Tick(retryTime.AddSeconds(1));Check(attempts==2,"queued clicks did not coalesce into one recovery read");
                }finally{release.Set();}
            }
        }
        // The UI may not consume a completed worker until the machine wakes.
        using(var completed=new ManualResetEventSlim()){
            int wakeCalls=0;var wake=now.AddHours(1);
            using(var session=new QuotaSession((provider,cancel)=>{
                int attempt=Interlocked.Increment(ref wakeCalls);completed.Set();
                return new QuotaReading{Provider=provider,Status="Live",Observed=attempt==1?now:wake};
            })){
                session.Enable("Codex",true);session.Tick(now);Check(completed.Wait(5000),"pre-sleep read did not finish");
                Pump(session,wake,()=>session.Readings[0].Observed==wake);
                Check(wakeCalls==2,"wake recovery must refresh an expired observation exactly once");
                session.Tick(wake.AddMinutes(4));Check(wakeCalls==2,"wake recovery lost normal cadence");
            }
        }
        calls=0;CancellationToken captured=CancellationToken.None;
        foreach(string failure in new[]{"Quota unavailable","Refresh rate limited","Login required","Quota access denied"}) {
            int attempts=0;int delay=failure=="Quota unavailable"?30:failure=="Refresh rate limited"?120:300;
            using(var retry=new QuotaSession((provider,cancel)=>new QuotaReading{Provider=provider,Status=Interlocked.Increment(ref attempts)==1?failure:"Live"})) {
                retry.Enable("Claude",true);Pump(retry,now,()=>retry.Readings[0].Status==failure);
                retry.Tick(now.AddSeconds(delay).AddTicks(-1));Check(attempts==1,"retry ran before its deadline");
                Pump(retry,now.AddSeconds(delay),()=>retry.Readings[0].Status=="Live");
                Check(attempts==2,"retry duplicated requests");
            }
        }
        using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim())
        using(var session=new QuotaSession((provider,cancel)=>{
            int count=Interlocked.Increment(ref calls);
            if(count==1){captured=cancel;entered.Set();Check(release.Wait(5000),"blocked fixture was not released");}
            return new QuotaReading {Provider=provider,Status=count==1?"Old result":"New result"};
        })){
            try {
                session.Enable("Codex",true);session.Tick(now);Check(entered.Wait(5000),"reader not started");
                session.Refresh();session.Tick(now.AddMinutes(6));Check(calls==1,"pending request overlapped");
                session.Enable("Codex",false);Check(captured.IsCancellationRequested&&session.Readings.Length==0,"disable did not cancel/hide provider");
                session.Enable("Codex",true);session.Tick(now);Check(calls==1,"re-enable overlapped old request");
                release.Set();
                Pump(session,now,()=>{Check(session.Readings[0].Status!="Old result","late result escaped version boundary");return session.Readings[0].Status=="New result";});
                Check(calls==2,"re-enable did not fetch exactly once");
            } finally {release.Set();}
        }
        using(var session=new QuotaSession((provider,cancel)=>{throw new Exception("private adapter error");})){
            session.Enable("Claude",true);Pump(session,now,()=>session.Readings[0].Status=="Quota unavailable");
            Check(session.Readings[0].Observed==now,"failed result did not use host time");
        }
        using(var entered=new ManualResetEventSlim())using(var finished=new ManualResetEventSlim()){
            CancellationToken disposeToken=CancellationToken.None;
            var session=new QuotaSession((provider,cancel)=>{
                disposeToken=cancel;entered.Set();cancel.WaitHandle.WaitOne(5000);finished.Set();
                return new QuotaReading {Status="Late disposed result"};
            });
            try {
                session.Enable("Claude",true);session.Tick(now);Check(entered.Wait(5000),"dispose fixture not started");
                session.Dispose();Check(finished.Wait(5000),"dispose did not release reader");
                Check(disposeToken.IsCancellationRequested,"dispose did not cancel reader");
                session.Tick(now.AddHours(1));Check(session.Readings[0].Status=="Refresh pending","disposed session published result");
            } finally {session.Dispose();}
        }
        Console.WriteLine("PASS Core quota lifecycle: opt-in, host deadlines, manual refresh, no overlap, cancellation, re-enable late results, faults and disposal");
    }
}
