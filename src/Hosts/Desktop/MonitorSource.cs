using System.Net.NetworkInformation;

namespace HardwarePulse.Desktop;

// One worker owns these sessions; the UI supplies only the selected interface name.
public interface IMonitorSource {
    bool IsDemo {get;}
    string[] Interfaces();
    MonitorSnapshot Poll(string? name);
}

public sealed class MonitorSource : IMonitorSource {
    readonly bool demo;
    readonly ReadingSession? cpu,memory;
    ReadingSession? network;
    string? selected;
    LinuxHwmonReadings? hwmon;
    MacSmcReadings? smc;
    MacGpuReadings? gpu;
    MacCpuIdentityReader? cpuIdentity;
    ReadingSession? gpuSession;
    ReadingSession? sensorSession;
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
        if(demo)return new("24.0%","7.5 / 16.0 GiB · 46.9%","124.5 KiB/s","8.2 KiB/s",true,true) {PeakCpu="42.0%",PeakDownload="256.0 KiB/s",PeakUpload="16.0 KiB/s"};
        var now=DateTimeOffset.UtcNow;
        cpu!.Poll(now);if(memory!=cpu)memory!.Poll(now);
        if(selected!=name) {
            selected=name;
            network=string.IsNullOrEmpty(name)?null:new ReadingSession(OperatingSystem.IsLinux()
                ?new LinuxNetworkReadings(name).Read:OperatingSystem.IsWindows()?new WindowsNetworkReadings(name).Read:new MacNetworkReadings(name).Read);
        }
        network?.Poll(now);
        var sensors=ReadSensors(now);
        var gpus=ReadGpus(now);
        MacCpuIdentity? info=OperatingSystem.IsMacOS()?(cpuIdentity??=new MacCpuIdentityReader()).Read():null;
        return MonitorSnapshot.Capture(cpu,memory!,network) with {Sensors=sensors.Current,PeakSensors=sensors.Peaks,SensorsSupported=OperatingSystem.IsLinux()||OperatingSystem.IsMacOS(),
            Gpus=gpus.Current,PeakGpus=gpus.Peaks,GpusSupported=OperatingSystem.IsMacOS(),
            CpuModel=info?.Model,CpuPhysicalCores=info?.PhysicalCores,CpuLogicalCores=info?.LogicalCores};
    }
    (HardwareSensorSnapshot[] Current,HardwareSensorSnapshot[] Peaks) ReadGpus(DateTimeOffset now) {
        if(!OperatingSystem.IsMacOS())return ([],[]);
        if(gpu==null){gpu=new MacGpuReadings();gpuSession=new ReadingSession(gpu.Read);}
        gpuSession!.Poll(now);
        HardwareSensorSnapshot[] Format(IReadOnlyDictionary<string,double> values)=>gpu.Channels.Select(channel=>new HardwareSensorSnapshot(channel.Id,channel.Label,
            values.TryGetValue(channel.Id,out var value)?ReadingFormat.SensorNumber(value,"%")+"%":"—",channel.Cores)).ToArray();
        return (Format(gpuSession.Latest.values),Format(gpuSession.Peaks));
    }
    (HardwareSensorSnapshot[] Current,HardwareSensorSnapshot[] Peaks) ReadSensors(DateTimeOffset now) {
        if(OperatingSystem.IsMacOS()) {
            if(smc==null){smc=new MacSmcReadings();sensorSession=new ReadingSession(smc.Read);}
            sensorSession!.Poll(now);
            HardwareSensorSnapshot[] FormatMac(IReadOnlyDictionary<string,double> values)=>smc.Channels.Select(channel=>new HardwareSensorSnapshot(channel.Id,channel.Label,
                values.TryGetValue(channel.Id,out var value)?ReadingFormat.SensorNumber(value,channel.Unit)+" "+channel.Unit:"—")).ToArray();
            return (FormatMac(sensorSession.Latest.values),FormatMac(sensorSession.Peaks));
        }
        if(!OperatingSystem.IsLinux())return ([],[]);
        if(hwmon==null) {hwmon=new LinuxHwmonReadings();sensorSession=new ReadingSession(hwmon.Read);hwmonDiscovery=System.Diagnostics.Stopwatch.GetTimestamp();}
        else if(System.Diagnostics.Stopwatch.GetElapsedTime(hwmonDiscovery)>=TimeSpan.FromSeconds(30)) {
            var previous=hwmon.Channels;
            hwmon.Refresh();hwmonDiscovery=System.Diagnostics.Stopwatch.GetTimestamp();
            // hwmon ids are not persistent device identities; topology/label changes reset sensor history.
            if(!previous.SequenceEqual(hwmon.Channels))sensorSession=new ReadingSession(hwmon.Read);
        }
        sensorSession!.Poll(now);
        HardwareSensorSnapshot[] Format(IReadOnlyDictionary<string,double> values)=>hwmon.Channels.Select(channel=>new HardwareSensorSnapshot(channel.Id,channel.Label,
            values.TryGetValue(channel.Id,out var value)?ReadingFormat.SensorNumber(value,channel.Unit)+" "+channel.Unit:"—")).ToArray();
        return (Format(sensorSession.Latest.values),Format(sensorSession.Peaks));
    }
}
