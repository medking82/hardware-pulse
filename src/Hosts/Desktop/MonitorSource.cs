using System.Net.NetworkInformation;

namespace HardwarePulse.Desktop;

public sealed record HardwareSensorSnapshot(string Id,string Label,string Value);
public sealed record MonitorSnapshot(string Cpu,string Memory,string Download,string Upload,bool CpuReady,bool MemoryReady) {
    public IReadOnlyList<HardwareSensorSnapshot> Sensors {get;init;}=[];
    public bool SensorsSupported {get;init;}
}

// One worker owns these sessions; the UI supplies only the selected interface name.
public sealed class MonitorSource {
    readonly bool demo;
    readonly ReadingSession? cpu,memory;
    ReadingSession? network;
    string? selected;
    LinuxHwmonReadings? hwmon;
    long hwmonDiscovery;
    public bool IsDemo=>demo;
    public MonitorSource(bool demo) {
        this.demo=demo;
        if(demo)return;
        if(OperatingSystem.IsLinux())cpu=memory=new ReadingSession(new LinuxReadings().Read);
        else if(OperatingSystem.IsWindows())cpu=memory=new ReadingSession(new WindowsSystemReadings().Read);
        else if(OperatingSystem.IsMacOS()) {
            cpu=new ReadingSession(new MacCpuReadings().Read);
            memory=new ReadingSession(new MacMemoryReadings().Read);
        } else throw new PlatformNotSupportedException();
    }
    public string[] Interfaces() {
        if(demo)return ["Demo network"];
        try{return NetworkInterface.GetAllNetworkInterfaces().Select(x=>x.Name).Distinct().Order().ToArray();}
        catch(NetworkInformationException){return [];}
    }
    public MonitorSnapshot Poll(string? name) {
        if(demo)return new("24.0%","7.5 / 16.0 GiB · 46.9%","124.5 KiB/s","8.2 KiB/s",true,true);
        var now=DateTimeOffset.UtcNow;
        cpu!.Poll(now);if(memory!=cpu)memory!.Poll(now);
        if(selected!=name) {
            selected=name;
            network=string.IsNullOrEmpty(name)?null:new ReadingSession(OperatingSystem.IsLinux()
                ?new LinuxNetworkReadings(name).Read:OperatingSystem.IsWindows()?new WindowsNetworkReadings(name).Read:new MacNetworkReadings(name).Read);
        }
        network?.Poll(now);
        bool c=cpu.Latest.values.TryGetValue("cpuLoad",out var load);
        bool m=memory!.Latest.usage.TryGetValue("ram",out var ram);
        var sensors=ReadSensors(now);
        return new(c?ReadingFormat.SensorNumber(load,"%")+"%":"—",
            m?$"{ram!.used:F1} / {ram.total:F1} GiB · {ram.percent:F1}%":"—",
            Rate(network,"netDown"),Rate(network,"netUp"),c,m) {Sensors=sensors,SensorsSupported=OperatingSystem.IsLinux()};
    }
    IReadOnlyList<HardwareSensorSnapshot> ReadSensors(DateTimeOffset now) {
        if(!OperatingSystem.IsLinux())return [];
        if(hwmon==null) {hwmon=new LinuxHwmonReadings();hwmonDiscovery=System.Diagnostics.Stopwatch.GetTimestamp();}
        else if(System.Diagnostics.Stopwatch.GetElapsedTime(hwmonDiscovery)>=TimeSpan.FromSeconds(30)) {
            hwmon.Refresh();hwmonDiscovery=System.Diagnostics.Stopwatch.GetTimestamp();
        }
        var reading=hwmon.Read(now);
        return hwmon.Channels.Select(channel=>new HardwareSensorSnapshot(channel.Id,channel.Label,
            reading.values.TryGetValue(channel.Id,out var value)?ReadingFormat.SensorNumber(value,channel.Unit)+" "+channel.Unit:"—")).ToArray();
    }
    static string Rate(ReadingSession? session,string key) {
        if(session==null||!session.Latest.values.TryGetValue(key,out var value))return "—";
        return value>=1048576?$"{value/1048576:F1} MiB/s":value>=1024?$"{value/1024:F1} KiB/s":$"{value:F1} B/s";
    }
}
