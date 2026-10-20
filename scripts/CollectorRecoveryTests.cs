using System;
using HardwarePulse;

static class CollectorRecoveryTests {
    static readonly DateTimeOffset Start=new DateTimeOffset(2026,10,21,0,0,0,TimeSpan.Zero);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static bool Observe(CollectorRecovery policy,double seconds,bool healthy=false,bool stopping=false){return policy.Observe(Start.AddSeconds(seconds),healthy,stopping);}

    static void BoundariesAndBudget(){
        var policy=new CollectorRecovery();
        Check(!Observe(policy,0),"Initial unhealthy observation must wait");
        Check(!Observe(policy,59.999),"Recovery fired before initial 60-second grace");
        Check(Observe(policy,60),"First attempt missing at boundary");
        Check(!Observe(policy,60)&&!Observe(policy,60.001),"Repeated polls bypassed backoff");
        Check(!Observe(policy,119.999)&&Observe(policy,120),"Second attempt boundary");
        Check(!Observe(policy,179.999)&&Observe(policy,180),"Third attempt boundary");
        for(int seconds=182;seconds<=600;seconds+=2)Check(!Observe(policy,seconds),"Sustained stale data exceeded three attempts");
        Check(!Observe(policy,86400),"Long outage refunded retry budget");
    }
    static void FreshDataResets(){
        var policy=new CollectorRecovery();Observe(policy,0);
        Check(Observe(policy,60)&&Observe(policy,120)&&Observe(policy,180),"Reset test did not exhaust budget");
        Check(!Observe(policy,240,true),"Healthy observation must not launch");
        Check(!Observe(policy,242)&&!Observe(policy,301.999),"New failure lost sustained-stale grace");
        Check(Observe(policy,302)&&Observe(policy,362)&&Observe(policy,422),"Fresh LIVE did not restore three attempts");
        Check(!Observe(policy,482),"Reset budget exceeded three attempts");
        policy=new CollectorRecovery();Observe(policy,0);
        Check(!Observe(policy,60,true),"Fresh data at deadline launched collector");
        Check(!Observe(policy,62)&&!Observe(policy,121.999)&&Observe(policy,122),"Transient outage did not rearm from new failure");
        policy=new CollectorRecovery();
        for(int seconds=0;seconds<=180;seconds+=2)Check(!Observe(policy,seconds,true),"Healthy polling launched collector");
    }
    static void StopSuppresses(){
        var policy=new CollectorRecovery();
        for(int seconds=0;seconds<=240;seconds+=2)Check(!Observe(policy,seconds,false,true),"STOP permitted collector launch");
        Check(!Observe(policy,242)&&!Observe(policy,301.999)&&Observe(policy,302),"Leaving STOP bypassed new grace");
        Check(Observe(policy,362),"STOP budget test second attempt missing");
        // A STOP with apparently healthy data must not reset the failure budget.
        Check(!Observe(policy,422,true,true)&&!Observe(policy,482,false,true),"STOP did not take priority over health");
        Check(!Observe(policy,484)&&Observe(policy,544),"STOP did not rearm wait while preserving remaining attempt");
        Check(!Observe(policy,604),"STOP refunded attempts without fresh LIVE");
        policy=new CollectorRecovery();Observe(policy,0);
        Check(!Observe(policy,60,false,true),"STOP at retry boundary launched collector");
        Check(!Observe(policy,61)&&!Observe(policy,120.999)&&Observe(policy,121),"STOP boundary retained an overdue retry");
    }
    static void ClockJumpsDoNotCatchUp(){
        var policy=new CollectorRecovery();Observe(policy,0);
        Check(Observe(policy,3600),"Resume did not allow one overdue attempt");
        for(int repeat=0;repeat<10;repeat++)Check(!Observe(policy,3600),"Forward clock jump caused catch-up attempts");
        Check(!Observe(policy,3659.999)&&Observe(policy,3660),"Forward jump backoff was not measured from actual attempt");
        Check(Observe(policy,7200)&&!Observe(policy,7260),"Large gaps bypassed attempt budget");
        policy=new CollectorRecovery();Observe(policy,0);Check(Observe(policy,60),"Backward test first attempt");
        Check(!Observe(policy,30)&&!Observe(policy,89.999)&&Observe(policy,90),"Backward jump did not rearm 60-second wait");
        Check(!Observe(policy,149.999)&&Observe(policy,150)&&!Observe(policy,210),"Backward jump refunded attempt budget");
        policy=new CollectorRecovery();Observe(policy,0);
        Check(!Observe(policy,-120)&&!Observe(policy,-60.001)&&Observe(policy,-60),"Backward jump during initial grace");
    }
    static int Main(){
        try{
            BoundariesAndBudget();FreshDataResets();StopSuppresses();ClockJumpsDoNotCatchUp();
            Console.WriteLine("PASS collector recovery: grace/backoff boundaries, three-attempt limit, fresh LIVE reset, STOP priority and clock jumps without catch-up");return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
