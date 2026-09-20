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
        public DateTimeOffset Observed;
        public List<QuotaWindow> Windows=new List<QuotaWindow>();
        public List<QuotaWindow> AllWindows=new List<QuotaWindow>();
    }
}
