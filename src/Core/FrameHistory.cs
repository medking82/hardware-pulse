using System;
using System.Collections.Generic;

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
    static readonly double[] Empty = new double[0];
    double[] sorted = Empty;
    public void Clear() { streams.Clear(); sorted = Empty; }
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
        Queue<Frame> best=null;int bestRecent=0;
        foreach(var queue in streams.Values) {
            while(queue.Count>0 && queue.Peek().Time<now-60)queue.Dequeue();
            int recent=0;foreach(var frame in queue)if(frame.Time>=now-1)recent++;
            if(recent>bestRecent){best=queue;bestRecent=recent;}
        }
        if(best==null)return new FrameMetrics{Status="Waiting for frames"};
        int length=best.Count;
        if(length>=100 && sorted.Length<length)sorted=new double[Math.Min(90000,Math.Max(length,sorted.Length*2))];
        double total=0,recentTotal=0,maximum=0;int index=0;
        foreach(var frame in best){
            total+=frame.Ms;if(frame.Time>=now-1)recentTotal+=frame.Ms;
            if(frame.Ms>maximum)maximum=frame.Ms;
            if(length>=100)sorted[index++]=frame.Ms;
        }
        double low=Double.NaN;
        if(length>=100){
            Array.Sort(sorted,0,length);
            int count=Math.Max(1,(int)Math.Ceiling(length*.01));double slowTotal=0;
            // Sum slowest frames in descending order, preserving previous Low semantics.
            for(int i=length-1;i>=length-count;i--)slowTotal+=sorted[i];
            low=1000/(slowTotal/count);
        }
        return new FrameMetrics{Ready=true,Status="Live",Count=length,
            Current=1000/(recentTotal/bestRecent),Average=1000/(total/length),Minimum=1000/maximum,Low=low};
    }
}
}