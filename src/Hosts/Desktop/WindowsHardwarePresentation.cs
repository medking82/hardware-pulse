namespace HardwarePulse.Desktop;

// Formatting of the existing Windows reading contract. Acquisition stays in
// the adapter and both windows receive these same immutable display snapshots.
public static class WindowsHardwarePresentation {
    static readonly (string Key,string Label,string Unit,string? Device)[] Metrics=[
        ("cpu","CPU temperature","°C","CPU"),("vcore","Vcore · Motherboard","V",null),
        ("cpuFan","CPU Fan","RPM","cpuFan"),("gpu","GPU temperature","°C","GPU"),
        ("gpuLoad","GPU utilization","%",null),("vram","VRAM Junction","°C",null),
        ("gpuVolt","Core Voltage","V",null),("gpuFan","GPU Fan 1","RPM",null),
        ("gpuFan2","GPU Fan 2","RPM",null),("ramA","Module 1","°C","ramA"),
        ("ramB","Module 2","°C","ramB"),("system","Motherboard temperature","°C","Airflow"),
        ("bottom","System Fan 1","RPM","bottom"),("top","System Fan 2","RPM","top"),
        ("diskC","Drive 1","°C","diskC"),("diskD","Drive 2","°C","diskD"),
        ("lanLink","LAN Link Speed","link",null),("wifiLink","Wi-Fi Link Speed","link",null),
        ("wifiSignal","Wi-Fi Signal","%",null)
    ];
    public static HardwareSensorSnapshot[] Capture(ReadingSession session,bool peaks) {
        var reading=session.Latest;
        if(reading.state!="LIVE"&&reading.available==null&&session.Peaks.Count==0)return [];
        var values=peaks?session.Peaks:reading.values;
        var rows=new List<HardwareSensorSnapshot>();
        foreach(var metric in Metrics) {
            // Preserve known topology during absence; never invent sensors that
            // the collector explicitly reported as unsupported.
            if(reading.available!=null&&(!reading.available.TryGetValue(metric.Key,out bool available)||!available))continue;
            bool found=(metric.Unit=="link"?reading.values:values).TryGetValue(metric.Key,out double value);
            string formatted=!found?"—":metric.Unit=="link"?NetworkRate.Link(value):ReadingFormat.SensorNumber(value,metric.Unit)+(metric.Unit=="%"?"":" ")+metric.Unit;
            string? device=metric.Device!=null&&reading.names.TryGetValue(metric.Device,out var name)?name:null;
            rows.Add(new(metric.Key,metric.Label,formatted,Device:device));
        }
        if(session.HasUsage("vram")) {
            string value=reading.usage.TryGetValue("vram",out var usage)?$"{usage.used:F1} / {usage.total:F1} GiB · {usage.percent:F1}%":"—";
            rows.Add(new("vramUsage",string.IsNullOrEmpty(usage?.label)?"GPU memory":usage.label,value));
        }
        return rows.ToArray();
    }
    public static string Status(Reading reading)=>reading.state=="LIVE"?"Live · Windows collector":reading.state=="STALE"?
        "Windows collector readings are stale. Waiting for a fresh snapshot.":"Windows collector unavailable. Waiting for hardware readings.";
}
