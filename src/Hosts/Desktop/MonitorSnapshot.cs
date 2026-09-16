namespace HardwarePulse.Desktop;

public sealed record HardwareSensorSnapshot(string Id,string Label,string Value,int? Cores=null) {
    public string GpuLabel(UiLanguage language)=>string.Format(language.T("{0} · {1} GPU cores"),Label,Cores?.ToString()??"—");
}
public sealed record MonitorSnapshot(string Cpu,string Memory,string Download,string Upload,bool CpuReady,bool MemoryReady) {
    public IReadOnlyList<HardwareSensorSnapshot> Sensors {get;init;}=[];
    public IReadOnlyList<HardwareSensorSnapshot> PeakSensors {get;init;}=[];
    public bool SensorsSupported {get;init;}
    public IReadOnlyList<HardwareSensorSnapshot> Gpus {get;init;}=[];
    public IReadOnlyList<HardwareSensorSnapshot> PeakGpus {get;init;}=[];
    public bool GpusSupported {get;init;}
    public string? CpuModel {get;init;}
    public int? CpuPhysicalCores {get;init;}
    public int? CpuLogicalCores {get;init;}
    public string PeakCpu {get;init;}="—";
    public string PeakDownload {get;init;}="—";
    public string PeakUpload {get;init;}="—";

    // Presentation of normalized Core sessions; no polling or platform access.
    public static MonitorSnapshot Capture(ReadingSession cpu,ReadingSession memory,ReadingSession? network) {
        bool c=cpu.Latest.values.TryGetValue("cpuLoad",out var load);
        bool m=memory.Latest.usage.TryGetValue("ram",out var ram);
        return new(c?ReadingFormat.SensorNumber(load,"%")+"%":"—",
            m?$"{ram!.used:F1} / {ram.total:F1} GiB · {ram.percent:F1}%":"—",
            Rate(network?.Latest.values,"netDown"),Rate(network?.Latest.values,"netUp"),c,m) {
            PeakCpu=cpu.Peaks.TryGetValue("cpuLoad",out var peak)?ReadingFormat.SensorNumber(peak,"%")+"%":"—",
            PeakDownload=Rate(network?.Peaks,"netDown"),PeakUpload=Rate(network?.Peaks,"netUp")};
    }
    static string Rate(IReadOnlyDictionary<string,double>? values,string key) {
        if(values==null||!values.TryGetValue(key,out var value))return "—";
        return value>=1048576?$"{value/1048576:F1} MiB/s":value>=1024?$"{value/1024:F1} KiB/s":$"{value:F1} B/s";
    }
}
