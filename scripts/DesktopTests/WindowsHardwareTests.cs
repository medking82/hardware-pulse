using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse;
using HardwarePulse.Desktop;

static class WindowsHardwareTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    // Explicit physical-machine acceptance. Never part of an unattended CI suite:
    // it requires an already running collector and does not start or configure one.
    public static void Live() {
        Check(OperatingSystem.IsWindows(),"Live Windows hardware acceptance requires Windows");
        var adapter=WindowsSnapshotReadings.Default();
        var session=new ReadingSession(adapter.Read);
        session.Poll(DateTimeOffset.UtcNow);
        Check(session.Latest.state=="LIVE","Existing collector must provide a fresh snapshot");
        var owner=new MonitorWindow(new MonitorSource(true),start:false);
        var identities=new HashSet<string>();
        HardwareSensorSnapshot[] rows=[];
        try {
            owner.Language.Select("en");owner.Show();owner.OpenFloatingMonitor();
            void PresentAndCheck() {
                rows=WindowsHardwarePresentation.Capture(session,false);
                var snapshot=MonitorSnapshot.Capture(session,session,null) with {
                    WindowsHardwareSupported=true,WindowsHardware=rows,
                    PeakWindowsHardware=WindowsHardwarePresentation.Capture(session,true),
                    WindowsHardwareStatus=WindowsHardwarePresentation.Status(session.Latest)};
                owner.Present(snapshot);Dispatcher.UIThread.RunJobs();
                var controls=owner.GetVisualDescendants().OfType<DeviceReadingCard>().SelectMany(x=>x.GetVisualDescendants().OfType<TextBlock>()).Where(x=>x.Tag is string).ToArray();
                for(int i=0;i<rows.Length;i++) {
                    Check(controls.Single(x=>Equals(x.Tag,"hardware/"+rows[i].Id)).Text==rows[i].Value,"Live Monitor hardware value differs from snapshot");
                    var floating=owner.FloatingMonitor!.GetVisualDescendants().OfType<Grid>().Single(x=>Equals(x.Tag,"hardware/"+rows[i].Id));
                    Check(((TextBlock)floating.Children[1]).Text==rows[i].Value,"Live Desktop hardware value differs from snapshot");
                }
            }
            for(int sample=0;sample<4;sample++) {
                if(sample>0)Thread.Sleep(2200);
                session.Poll(DateTimeOffset.UtcNow);
                Check(session.Latest.state=="LIVE","Collector became unavailable during live acceptance");
                Check(session.Latest.values.Values.All(double.IsFinite),"Collector mapping contains non-finite readings");
                identities.Add(session.Latest.identity);
                PresentAndCheck();
                Check(rows.Length>0&&rows.Any(x=>x.Value.EndsWith(" °C"))&&rows.Any(x=>x.Value.EndsWith(" RPM")),"Live acceptance requires actual temperature and fan channels");
                // Only generic presentation IDs/values are exported. Device labels,
                // identities, paths, raw snapshots and account data stay in memory.
                Console.WriteLine("WINDOWS_LIVE_HARDWARE "+JsonSerializer.Serialize(new{sample,ageSeconds=(DateTimeOffset.UtcNow-session.Latest.time).TotalSeconds,metrics=rows.ToDictionary(x=>x.Id,x=>x.Value)}));
            }
            Check(identities.Count>=3,"Collector snapshot did not advance during live acceptance");
            var liveIds=rows.Select(x=>x.Id).ToArray();
            session.Poll(DateTimeOffset.UtcNow.AddSeconds(30));
            Check(session.Latest.state=="STALE","Future-clock read must reject stale live metrics");
            PresentAndCheck();
            Check(rows.Select(x=>x.Id).SequenceEqual(liveIds)&&rows.All(x=>x.Value=="—"),"Stale hardware values must clear without losing known topology");
            Check(session.Peaks.Count>0,"Staleness must preserve session history");
        } finally {owner.Close();}
        Console.WriteLine("PASS live Windows hardware: advancing existing collector, Monitor/Desktop parity and stale clearing; no source writes");
    }
    public static void Run(string? output) {
        string folder=Directory.CreateTempSubdirectory("pulse-windows-snapshot-").FullName;
        try {
            string path=Path.Combine(folder,"snapshot.json");
            var adapter=new WindowsSnapshotReadings(path);var now=DateTimeOffset.UtcNow;
            Check(adapter.Read(now).state=="OFFLINE","Absent collector must be offline");
            var sensors=new List<Sensor>();
            void Add(string hardwareType,string hardwareId,string name,string type,double value,string? id=null)=>sensors.Add(new(){hardwareType=hardwareType,hardwareId=hardwareId,hardware="Fixture "+hardwareType,name=name,type=type,value=value,id=id??hardwareId+"/"+type+"/"+sensors.Count});
            Add("Cpu","/intelcpu/0","CPU Package","Temperature",55);
            Add("Cpu","/intelcpu/0","CPU Total","Load",25);
            Add("GpuNvidia","/gpu-nvidia/0","GPU Core","Temperature",61);
            Add("GpuNvidia","/gpu-nvidia/0","GPU Core","Load",40);
            Add("GpuNvidia","/gpu-nvidia/0","GPU Memory Junction","Temperature",70);
            Add("GpuNvidia","/gpu-nvidia/0","GPU Core Voltage","Voltage",.95);
            Add("GpuNvidia","/gpu-nvidia/0","GPU Fan 1","Fan",0);
            Add("GpuNvidia","/gpu-nvidia/0","GPU Fan 2","Fan",900);
            Add("GpuNvidia","/gpu-nvidia/0","GPU Memory Used","SmallData",2048);
            Add("GpuNvidia","/gpu-nvidia/0","GPU Memory Total","SmallData",8192);
            Add("SuperIO","/lpc/fixture/0","Vcore","Voltage",1.1);
            Add("SuperIO","/lpc/fixture/0","CPU Fan","Fan",1100);
            Add("SuperIO","/lpc/fixture/0","System","Temperature",35);
            Add("SuperIO","/lpc/fixture/0","System Fan 1","Fan",800);
            Add("SuperIO","/lpc/fixture/0","System Fan 2","Fan",850);
            Add("Memory","/memory/dimm/0","DIMM #1","Temperature",42,"/memory/dimm/0/temperature/0");
            Add("Memory","/memory/dimm/1","DIMM #2","Temperature",43,"/memory/dimm/1/temperature/0");
            Add("Storage","/nvme/0","Composite","Temperature",48);
            Add("Storage","/nvme/1","Composite","Temperature",49);
            var raw=new RawSnapshot{schema=2,time=now.ToString("o"),sequence=1,pid=123,sensors=sensors.ToArray(),boardName="Fixture board",ramUsage=new(){usedGb=4,totalGb=16},
                networkLinks=[new(){hardwareId="wired",connectionType="Ethernet",physical=true,connected=true,bitsPerSecond=1000000000},new(){hardwareId="wifi",connectionType="Wi-Fi",physical=true,connected=true,bitsPerSecond=866000000,signalPercent=75}]};
            void Save()=>File.WriteAllText(path,JsonSerializer.Serialize(raw,new JsonSerializerOptions{IncludeFields=true}));
            Save();var session=new ReadingSession(adapter.Read);session.Poll(now);
            Check(session.Latest.state=="LIVE"&&session.Latest.values["gpuFan"]==0,"Modern snapshot preserves live and valid zero RPM");
            Check(session.Latest.values["diskC"]==48&&session.Latest.values["ramA"]==42&&session.Latest.values["vcore"]==1.1,"WPF hardware mapping reused");
            var rows=WindowsHardwarePresentation.Capture(session,false);
            Check(rows.Any(x=>x.Id=="gpuFan"&&x.Value=="0 RPM")&&rows.Any(x=>x.Id=="vramUsage"&&x.Value=="2.0 / 8.0 GiB · 25.0%"),"GPU fan and current VRAM usage formatting");
            Check(rows.Any(x=>x.Id=="wifiSignal"&&x.Value=="75.0%")&&rows.Any(x=>x.Id=="lanLink"),"Link and signal metrics retained");
            var owner=new MonitorWindow(new MonitorSource(true),start:false);owner.Show();
            var snapshot=new MonitorSnapshot("25.0%","4.0 / 16.0 GiB","1 KiB/s","2 KiB/s",true,true){WindowsHardwareSupported=true,WindowsHardware=rows,PeakWindowsHardware=WindowsHardwarePresentation.Capture(session,true),WindowsHardwareStatus=WindowsHardwarePresentation.Status(session.Latest)};
            owner.Present(snapshot);owner.OpenFloatingMonitor();Dispatcher.UIThread.RunJobs();
            Check(owner.GetVisualDescendants().OfType<Border>().Count(x=>x.Name?.StartsWith("DeviceCard_")==true)==6,"Windows readings must use the six 0.6.27 device cards");
            var panel=owner.GetVisualDescendants().OfType<DeviceReadingCard>().Single(x=>x.Name=="DeviceCard_GPU");
            foreach(var sensor in rows)Check(owner.GetVisualDescendants().OfType<TextBlock>().Single(x=>Equals(x.Tag,"hardware/"+sensor.Id)).Text==sensor.Value,"Grouped Monitor dropped or duplicated a sensor");
            Check(panel.IsVisible&&panel.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="61.0 °C"),"Monitor shows collector GPU temperature");
            Check(owner.FloatingMonitor!.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="61.0 °C"),"Floating Desktop shares collector reading");
            var retained=panel.GetVisualDescendants().OfType<TextBlock>().Single(x=>x.Text=="61.0 °C");
            sensors[2].value=50;raw.sequence++;Save();session.Poll(now.AddSeconds(2));
            Check(WindowsHardwarePresentation.Capture(session,true).Single(x=>x.Id=="gpu").Value=="61.0 °C","Session Max retains previous GPU peak");
            snapshot=snapshot with{WindowsHardware=WindowsHardwarePresentation.Capture(session,false),PeakWindowsHardware=WindowsHardwarePresentation.Capture(session,true)};
            owner.Present(snapshot);Check(retained.Text=="50.0 °C","Live updates reuse Monitor controls");
            owner.Width=360;Dispatcher.UIThread.RunJobs();
            foreach(var row in owner.GetVisualDescendants().OfType<Grid>().Where(x=>Equals(x.Tag,"device-reading"))) {
                var label=(TextBlock)row.Children[0];var value=(TextBlock)row.Children[1];
                Check(label.Bounds.Right<=value.Bounds.Left&&value.Bounds.Right<=row.Bounds.Width+.1,"Windows label/value overlap or clipping at narrow width");
            }
            if(output!=null){owner.Width=360;owner.Height=850;Dispatcher.UIThread.RunJobs();var scroll=panel.GetVisualAncestors().OfType<ScrollViewer>().First();scroll.Offset=new Avalonia.Vector(0,panel.Bounds.Top);Dispatcher.UIThread.RunJobs();using var frame=owner.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"windows-hardware-360.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
            session.Poll(now.AddSeconds(20));Check(session.Latest.state=="STALE","Old collector snapshot rejected");
            snapshot=snapshot with{WindowsHardware=WindowsHardwarePresentation.Capture(session,false),PeakWindowsHardware=WindowsHardwarePresentation.Capture(session,true),WindowsHardwareStatus=WindowsHardwarePresentation.Status(session.Latest)};
            owner.Present(snapshot);Check(retained.Text=="—"&&!owner.FloatingMonitor!.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="50.0 °C"),"Stale live values cleared from both consumers");
            Check(snapshot.PeakWindowsHardware.Single(x=>x.Id=="gpu").Value=="61.0 °C","Stale source preserves explicitly selected peaks");
            owner.Close();
            foreach(string invalid in new[]{"{broken","null",new string(' ',8*1024*1024+1),"{\"schema\":99}"}){File.WriteAllText(path,invalid);Check(adapter.Read(now).state=="OFFLINE","Invalid/bounded snapshot rejected");}
            if(OperatingSystem.IsWindows()) {
                var live=WindowsSnapshotReadings.Default().Read(DateTimeOffset.UtcNow);
                Check(live.state is "LIVE" or "OFFLINE" or "STALE","Native snapshot state is explicit");
                Console.WriteLine($"WINDOWS_COLLECTOR_SOURCE state={live.state} validMetrics={live.values.Count}; no identifiers exported");
            }
        } finally {Directory.Delete(folder,true);}
        Console.WriteLine("PASS Windows collector snapshot: shared mapping, JSON bounds, live/zero/stale/peaks, Monitor/Desktop and retained controls");
    }
}
