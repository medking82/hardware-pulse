using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HardwarePulse {
    // One UI session owns its history; neither WPF nor the collector owns peaks.
    // The host supplies normalized readings and calls Poll serially. No IO, timers or threads live here.
    public sealed class ReadingSession {
        readonly Func<DateTimeOffset,Reading> read;
        readonly Dictionary<string,double> peaks=new Dictionary<string,double>();
        readonly HashSet<string> usageCapabilities=new HashSet<string>();
        string identity="";

        public Reading Latest { get; private set; }
        public IReadOnlyDictionary<string,double> Peaks { get; private set; }

        public ReadingSession(Func<DateTimeOffset,Reading> read) {
            if(read==null)throw new ArgumentNullException("read");
            this.read=read;
            Latest=new Reading();
            Peaks=new ReadOnlyDictionary<string,double>(peaks);
        }

        public bool HasUsage(string key) { return usageCapabilities.Contains(key); }

        public void Poll(DateTimeOffset now) {
            var previous=Latest;
            Latest=read(now);
            if(Latest.state!="LIVE") {
                Latest.available=previous.available;
                Latest.names=previous.names;
                Latest.gpuFanCount=previous.gpuFanCount;
                return;
            }

            usageCapabilities.Clear();
            foreach(string key in Latest.usage.Keys)usageCapabilities.Add(key);
            if(Latest.identity==identity)return;
            foreach(var entry in Latest.values) {
                double peak;
                if(!peaks.TryGetValue(entry.Key,out peak)||peak<entry.Value)peaks[entry.Key]=entry.Value;
            }
            identity=Latest.identity;
        }
    }
}
