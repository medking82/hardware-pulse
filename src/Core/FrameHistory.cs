using System;
using System.Collections.Generic;
using System.Linq;

public sealed class FrameMetrics {
    public double Current, Average, Minimum, Low;
    public int Count;
    public bool Ready;
    public string Status;
}
namespace HardwarePulse {
// Host owns synchronization and supplies monotonic timestamps. No process or clock here.
public sealed class FrameHistory {
    sealed class Frame { public double Ms; public double Time; }
    readonly Dictionary<string, Queue<Frame>> streams = new Dictionary<string, Queue<Frame>>();
    public void Clear() { streams.Clear(); }
    public bool Add(string stream, double ms, double time) {

        if (Double.IsNaN(ms) || Double.IsInfinity(ms) || ms <= 0 || Double.IsNaN(time) || Double.IsInfinity(time)) return false;
        Queue<Frame> frames;
        if (!streams.TryGetValue(stream,out frames)) {
            if (streams.Count >= 16) return false;
            streams[stream] = frames = new Queue<Frame>();
        }
        frames.Enqueue(new Frame { Ms = ms, Time = time });
        while(frames.Count > 90000 || (frames.Count > 0 && frames.Peek().Time < time-60)) frames.Dequeue();
        return true;
    }
    public FrameMetrics ReadAt(double now) {

        foreach(var queue in streams.Values) while(queue.Count > 0 && queue.Peek().Time < now-60) queue.Dequeue();
        var best = streams.Values.Select(q=>new{Frames=q,Recent=q.Count(f=>f.Time>=now-1)})
            .Where(s=>s.Recent>0).OrderByDescending(s=>s.Recent).Select(s=>s.Frames).FirstOrDefault();
        if (best == null) return new FrameMetrics { Status = "Waiting for frames" };
        var frames = best.ToArray(); var latest = frames.Where(f => f.Time >= now-1).ToArray();
        var sorted = frames.Select(f => f.Ms).OrderByDescending(x => x).ToArray();
        int count = Math.Max(1,(int)Math.Ceiling(frames.Length*.01));
        return new FrameMetrics { Ready = latest.Length > 0, Status = latest.Length > 0 ? "Live" : "Waiting for frames", Count = frames.Length,
            Current = latest.Length == 0 ? 0 : 1000/latest.Average(f=>f.Ms), Average = 1000/frames.Average(f=>f.Ms),
            Minimum = 1000/sorted[0], Low = frames.Length < 100 ? Double.NaN : 1000/sorted.Take(count).Average() };
    }
}
}
