namespace HardwarePulse.Desktop;

public sealed record DesktopRow(string Id,string Label,string Value,string? Icon=null) {
    public string EditorLabel=>Id.StartsWith("quota/",StringComparison.Ordinal)&&Icon!=null?Id.Split('/')[1]+" · "+Label:Label;
}

// Stable source IDs, never translated text or formatted values, own preferences.
public static class DesktopRows {
    public static IReadOnlyList<DesktopRow> Capture(MonitorSnapshot? snapshot,bool peaks,UiLanguage language,IReadOnlyDictionary<string,QuotaReading> quotas,DesktopFpsSnapshot? fps) {
        var rows=new List<DesktopRow>{new("CPU",language.T("CPU"),snapshot==null?"—":peaks?snapshot.PeakCpu:snapshot.Cpu,"cpu"),
            new("Memory",language.T("Memory"),snapshot?.Memory??"—","memory"),
            new("Download",language.T("Download"),snapshot==null?"—":peaks?snapshot.PeakDownload:snapshot.Download,"down"),
            new("Upload",language.T("Upload"),snapshot==null?"—":peaks?snapshot.PeakUpload:snapshot.Upload,"up")};
        if(snapshot!=null) {
            foreach(var item in peaks?snapshot.PeakSensors:snapshot.Sensors)rows.Add(new("sensor/"+item.Id,item.Label,item.Value));
            foreach(var item in peaks?snapshot.PeakGpus:snapshot.Gpus)rows.Add(new("gpu/"+item.Id,item.GpuLabel(language),item.Value));
            if(snapshot.WindowsHardwareSupported&&snapshot.WindowsHardwareStatus.Length>0)rows.Add(new("hardware/status",language.T("Windows hardware"),language.T(snapshot.WindowsHardwareStatus)));
            foreach(var item in peaks?snapshot.PeakWindowsHardware:snapshot.WindowsHardware)rows.Add(new("hardware/"+item.Id,item.DisplayLabel(language),language.T(item.Value)));
        }
        foreach(string provider in QuotaSession.Providers) {
            if(!quotas.TryGetValue(provider,out var reading))continue;
            rows.Add(new("quota/"+provider+"/status",provider+" · "+language.T(reading.Status),""));
            var counts=new Dictionary<string,int>();
            foreach(var item in reading.AllWindows.Count>0?reading.AllWindows:reading.Windows) {
                counts.TryGetValue(item.Label,out int duplicate);counts[item.Label]=duplicate+1;
                rows.Add(new("quota/"+provider+"/window/"+item.Label+"/"+duplicate,language.T(item.Label),item.Remaining.HasValue?string.Format(language.T("{0}% left"),item.Remaining.Value.ToString("F1")):"—",provider.ToLowerInvariant()));
            }
        }
        if(fps?.Enabled==true)rows.Add(new("FPS","FPS",$"FPS {fps.Current} · AVG {fps.Average} · MIN {fps.Minimum} · 1% LOW {fps.Low}\n{language.T(fps.Status)}"));
        return rows;
    }
}
