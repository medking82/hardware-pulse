using System;
using System.Collections.Generic;

namespace HardwarePulse {
    public sealed class QuotaWindow {
        public string Label;
        public double? Remaining;
        public DateTimeOffset? Reset;
    }
    public sealed class QuotaReading {
        public string Provider,Status="Quota unavailable";
        public string Source;
        public string FailureKind;
        // Opaque revision supplied by the adapter. It is never persisted or interpreted here.
        public string CacheScope;
        public int HttpStatus;
        public DateTimeOffset Observed;
        public DateTimeOffset? RetryAt;
        public List<QuotaWindow> Windows=new List<QuotaWindow>();
        public List<QuotaWindow> AllWindows=new List<QuotaWindow>();
    }
    public sealed class QuotaRefreshState {
        public DateTimeOffset? NextAttempt {get;internal set;}
        public DateTimeOffset? Started {get;internal set;}
        public DateTimeOffset? LastSuccess {get;internal set;}
        public bool Refreshing {get;internal set;}
        public bool TimedOut {get;internal set;}
        public int RateLimitFailures {get;internal set;}
        public double? LastDurationMilliseconds {get;internal set;}
        public QuotaReading LastGood {get;internal set;}
    }
    public sealed class QuotaAttempt {
        public string Provider,Status;
        public string Source,FailureKind;
        public int HttpStatus;
        public DateTimeOffset Started,Completed;
        public double DurationMilliseconds;
        public DateTimeOffset? RetryAt,NextAttempt;
        public int RateLimitFailures;
    }
}
