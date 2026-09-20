namespace HardwarePulse.Desktop;

public sealed record HardwareSensorSnapshot(string Id,string Label,string Value);
public sealed record MonitorSnapshot(string Cpu,string Memory,string Download,string Upload,bool CpuReady,bool MemoryReady) {
    // Core readings are immutable to consumers; peaks are copied at publication.
    public Reading? Hardware {get;init;}
    public QuotaReading? CodexQuota {get;init;}
    public QuotaReading? ClaudeQuota {get;init;}
    public QuotaReading? AntigravityQuota {get;init;}
    public DesktopFpsSnapshot? Fps {get;init;}
    public string? NetworkName {get;init;}
    public IReadOnlyDictionary<string,double> HardwarePeaks {get;init;}=new Dictionary<string,double>();
    public IReadOnlyList<HardwareSensorSnapshot> Sensors {get;init;}=[];
    public IReadOnlyList<HardwareSensorSnapshot> PeakSensors {get;init;}=[];
    public bool SensorsSupported {get;init;}
    public string PeakCpu {get;init;}="—";
    public string PeakDownload {get;init;}="—";
    public string PeakUpload {get;init;}="—";

    public double? DownloadBytes {get;init;}
    public double? UploadBytes {get;init;}
    public double? PeakDownloadBytes {get;init;}
    public double? PeakUploadBytes {get;init;}
    public MonitorSnapshot WithNetworkUnit(string unit)=>this with {
        Download=DownloadBytes.HasValue?NetworkRate.Format(DownloadBytes.Value,unit):Download,
        Upload=UploadBytes.HasValue?NetworkRate.Format(UploadBytes.Value,unit):Upload,
        PeakDownload=PeakDownloadBytes.HasValue?NetworkRate.Format(PeakDownloadBytes.Value,unit):PeakDownload,
        PeakUpload=PeakUploadBytes.HasValue?NetworkRate.Format(PeakUploadBytes.Value,unit):PeakUpload};

    // Presentation of normalized Core sessions; no polling or platform access.
    public static MonitorSnapshot Capture(ReadingSession cpu,ReadingSession memory,ReadingSession? network) {
        bool c=cpu.Latest.values.TryGetValue("cpuLoad",out var load);
        bool m=memory.Latest.usage.TryGetValue("ram",out var ram);
        return new(c?ReadingFormat.SensorNumber(load,"%")+"%":"—",
            m?$"{ram!.used:F1} / {ram.total:F1} GiB · {ram.percent:F1}%":"—",
            Rate(network?.Latest.values,"netDown"),Rate(network?.Latest.values,"netUp"),c,m) {
            DownloadBytes=Bytes(network?.Latest.values,"netDown"),UploadBytes=Bytes(network?.Latest.values,"netUp"),
            PeakDownloadBytes=Bytes(network?.Peaks,"netDown"),PeakUploadBytes=Bytes(network?.Peaks,"netUp"),
            PeakCpu=cpu.Peaks.TryGetValue("cpuLoad",out var peak)?ReadingFormat.SensorNumber(peak,"%")+"%":"—",
            PeakDownload=Rate(network?.Peaks,"netDown"),PeakUpload=Rate(network?.Peaks,"netUp")};
    }
    static double? Bytes(IReadOnlyDictionary<string,double>? values,string key)=>values!=null&&values.TryGetValue(key,out var value)?value:null;
    static string Rate(IReadOnlyDictionary<string,double>? values,string key) {
        if(values==null||!values.TryGetValue(key,out var value))return "—";
        return value>=1048576?$"{value/1048576:F1} MiB/s":value>=1024?$"{value/1024:F1} KiB/s":$"{value:F1} B/s";
    }
}
