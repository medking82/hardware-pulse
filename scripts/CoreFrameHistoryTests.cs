using System;
using HardwarePulse;

internal static class CoreFrameHistoryTests {
    static void Check(bool value,string message){if(!value)throw new Exception("Core FPS: "+message);}
    static bool Near(double a,double b){return Math.Abs(a-b)<0.000001;}
    public static void Run(){
        var history=new FrameHistory();
        Check(!history.ReadAt(0).Ready,"empty history unavailable");
        Check(!history.Add("main",0,0)&&!history.Add("main",double.NaN,0)&&!history.Add("main",10,double.PositiveInfinity),"invalid observations rejected");
        history.Add("main",20,1);var first=history.ReadAt(1);
        Check(first.Ready&&first.Current==50&&first.Average==50&&first.Minimum==50&&double.IsNaN(first.Low),"single frame and insufficient Low samples");
        history.Clear();for(int i=0;i<199;i++)history.Add("main",10,10);history.Add("main",100,10);
        var metrics=history.ReadAt(10);
        Check(metrics.Count==200&&Near(metrics.Current,1000/10.45)&&Near(metrics.Average,1000/10.45)&&metrics.Minimum==10&&Near(metrics.Low,1000/55.0),"Current/Average/Minimum/slowest-one-percent semantics");
        history.Add("active",20,11.5);Check(history.ReadAt(11.5).Current==50,"stale large stream cannot hide fresh active stream");
        Check(!history.ReadAt(13).Ready,"ended stream unavailable");
        history.Clear();history.Add("main",10,0);history.Add("main",20,60);
        Check(history.ReadAt(60).Count==2,"60-second boundary retained");
        Check(history.ReadAt(60.01).Count==1,"expired history removed on read");
        history.Clear();history.Add("main",10,0);history.Add("main",20,61);
        Check(history.ReadAt(61).Count==1,"expired history removed on add");
        history.Clear();for(int i=0;i<90001;i++)history.Add("main",10,1);
        Check(history.ReadAt(1).Count==90000,"frame count bounded");
        history.Clear();for(int i=0;i<16;i++)Check(history.Add("stream"+i,10,1),"admitted stream");
        Check(!history.Add("extra",20,1),"stream count bounded");
        history.Clear();Check(history.Add("extra",20,1)&&history.ReadAt(1).Count==1,"reset releases history and stream slots");
        Check(typeof(FrameMetrics).Assembly==typeof(Reading).Assembly,"metrics must live in Core");
        Console.WriteLine("PASS Core FPS: stale selection, statistics, minimum vs Low, invalid input, 60-second/90000-frame/16-stream bounds and reset");
    }
}