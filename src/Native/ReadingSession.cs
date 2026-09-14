using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HardwarePulse {
    // One UI session owns its history; neither WPF nor the collector owns peaks.
    // The UI timer calls Poll on its own thread. No timers or threads are created here.
    public sealed class ReadingSession {
        readonly string snapshotPath;
        readonly Dictionary<string,double> peaks=new Dictionary<string,double>();
        readonly HashSet<string> usageCapabilities=new HashSet<string>();
        string identity="";

        public Reading Latest { get; private set; }
        public IReadOnlyDictionary<string,double> Peaks { get; private set; }

        public ReadingSession(string snapshotPath) {
            this.snapshotPath=snapshotPath;
            Latest=new Reading();
            Peaks=new ReadOnlyDictionary<string,double>(peaks);
        }

        public bool HasUsage(string key) { return usageCapabilities.Contains(key); }

        public void Poll(DateTimeOffset now) {
            var previousCapabilities=Latest.available;
            Latest=SensorProfile.Read(snapshotPath,now);
            if(Latest.state!="LIVE") {
                Latest.available=previousCapabilities;
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
