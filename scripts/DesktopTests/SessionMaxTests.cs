using HardwarePulse;
using HardwarePulse.Desktop;

static class SessionMaxTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run() {
        Reading reading=new(){state="LIVE",identity="1",values={{"cpuLoad",80},{"netDown",2048},{"netUp",1048576}},usage={{"ram",new Usage{used=8,total=16,percent=50}}}};
        var session=new ReadingSession(_=>reading);session.Poll(DateTimeOffset.UtcNow);
        reading=new(){state="LIVE",identity="2",values={{"cpuLoad",20},{"netDown",1024},{"netUp",100}},usage={{"ram",new Usage{used=4,total=16,percent=25}}}};
        session.Poll(DateTimeOffset.UtcNow);
        var snapshot=MonitorSnapshot.Capture(session,session,session);
        Check(snapshot.Cpu=="20.0%"&&snapshot.PeakCpu=="80.0%","Core peak distinct from current CPU");
        Check(snapshot.Download=="1.0 KiB/s"&&snapshot.PeakDownload=="2.0 KiB/s"&&snapshot.PeakUpload=="1.0 MiB/s","Network peaks keep byte-rate units");
        Check(snapshot.Memory=="4.0 / 16.0 GiB · 25.0%","RAM remains current, not a synthetic mix of peaks");
        reading=new();session.Poll(DateTimeOffset.UtcNow);
        snapshot=MonitorSnapshot.Capture(session,session,session);
        Check(snapshot.Cpu=="—"&&snapshot.PeakCpu=="80.0%"&&!snapshot.CpuReady,"Unavailable live readings preserve clearly separate history");
        var otherNetwork=new ReadingSession(_=>new Reading());otherNetwork.Poll(DateTimeOffset.UtcNow);
        Check(MonitorSnapshot.Capture(session,session,otherNetwork).PeakDownload=="—","New interface session does not inherit peaks");
        Console.WriteLine("PASS shared Session Max presentation: Core history, units, current RAM and unavailable readings");
    }
}
