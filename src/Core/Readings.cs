using System;
using System.Collections.Generic;

namespace HardwarePulse {
    public sealed class Usage { public double used, total, percent; public string label; }
    public sealed class Reading {
        public string state="OFFLINE", identity, error;
        public DateTimeOffset time;
        public Dictionary<string,double> values=new Dictionary<string,double>();
        public Dictionary<string,bool> available;
        public Dictionary<string,string> names=new Dictionary<string,string>();
        public Dictionary<string,Usage> usage=new Dictionary<string,Usage>();
        public int gpuFanCount;
    }
}
