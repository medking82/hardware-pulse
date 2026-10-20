using System;

namespace HardwarePulse {
    // Serial policy for the existing UI poll. A true result consumes an attempt
    // even when the caller's owned task launch fails. No IO, timers or threads.
    public sealed class CollectorRecovery {
        static readonly TimeSpan Backoff=TimeSpan.FromSeconds(60);
        bool armed;
        int attempts;
        DateTimeOffset observed,next;

        public bool Observe(DateTimeOffset now,bool healthy,bool stopping){
            // STOP takes priority over health and never refunds retry attempts.
            if(stopping){armed=false;return false;}
            if(healthy){attempts=0;armed=false;return false;}
            // A backward clock correction starts a new wait, retaining budget.
            if(!armed||now<observed){armed=true;observed=now;next=now+Backoff;return false;}
            observed=now;
            if(attempts>=3||now<next)return false;
            attempts++;next=now+Backoff;return true;
        }
    }
}
