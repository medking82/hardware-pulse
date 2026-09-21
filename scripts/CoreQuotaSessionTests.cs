using System;
using System.Collections.Generic;
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
        foreach(string testedProvider in QuotaSession.Providers){
        calls=0;CancellationToken captured=CancellationToken.None;
        using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim())
        using(var session=new QuotaSession((provider,cancel)=>{
            int count=Interlocked.Increment(ref calls);
            if(count==1){captured=cancel;entered.Set();Check(release.Wait(5000),"blocked fixture was not released");}
            return new QuotaReading {Provider=provider,Status=count==1?"Old result":"New result"};
        })){
            try {
                session.Enable(testedProvider,true);session.Tick(now);Check(entered.Wait(5000),"reader not started");
                session.Refresh();session.Tick(now.AddMinutes(6));Check(calls==1,"pending request overlapped");
                session.Enable(testedProvider,false);Check(captured.IsCancellationRequested&&session.Readings.Length==0,"disable did not cancel/hide provider");
                session.Enable(testedProvider,true);session.Tick(now);Check(calls==1,"re-enable overlapped old request");
                release.Set();
                Pump(session,now,()=>{Check(session.Readings[0].Status!="Old result","late result escaped version boundary");return session.Readings[0].Status=="New result";});
                Check(calls==2,"re-enable did not fetch exactly once");
            } finally {release.Set();}
        }
        }
        using(var session=new QuotaSession((provider,cancel)=>{throw new Exception("private adapter error");})){
            session.Enable("Claude",true);Pump(session,now,()=>session.Readings[0].Status=="Quota unavailable");
            Check(session.Readings[0].Observed==now,"failed result did not use host time");
        }
        calls=0;
        using(var session=new QuotaSession((provider,cancel)=>{
            int count=Interlocked.Increment(ref calls);
            if(count==1)return new QuotaReading{Provider=provider,Status="Live",Observed=now,
                Windows=new List<QuotaWindow>{new QuotaWindow{Label="5-hour",Remaining=61}},
                AllWindows=new List<QuotaWindow>{new QuotaWindow{Label="5-hour",Remaining=61}}};
            if(count==2)return new QuotaReading{Provider=provider,Status="Quota unavailable",Observed=now.AddMinutes(5)};
            return new QuotaReading{Provider=provider,Status="Live",Observed=now.AddMinutes(5).AddSeconds(30),
                Windows=new List<QuotaWindow>{new QuotaWindow{Label="5-hour",Remaining=59}},
                AllWindows=new List<QuotaWindow>{new QuotaWindow{Label="5-hour",Remaining=59}}};
        })){
            session.Enable("Codex",true);Pump(session,now,()=>session.Readings[0].Status=="Live");
            Pump(session,now.AddMinutes(5),()=>session.Readings[0].Status=="Last update failed");
            Check(session.Readings[0].Observed==now&&session.Readings[0].AllWindows[0].Remaining==61,"transient failure discarded last good quota");
            session.Tick(now.AddMinutes(5).AddSeconds(29));Check(calls==2,"transient failure retried before backoff");
            Pump(session,now.AddMinutes(5).AddSeconds(30),()=>session.Readings[0].Status=="Live"&&session.Readings[0].AllWindows[0].Remaining==59);
            Check(calls==3,"transient failure did not retry after short backoff");
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
