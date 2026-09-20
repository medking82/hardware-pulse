namespace HardwarePulse.Desktop;

// Original DesktopMode metric grouping, using only the owner's published snapshot.
internal sealed record DesktopReading(string Key,string Title,string Value,string Icon);
internal static class DesktopReadings {
    public static IReadOnlyList<DesktopReading> Create(MonitorSnapshot snapshot,bool peaks,UiLanguage language,IReadOnlyDictionary<string,string>? names=null) {
        var result=new List<DesktopReading>();var hardware=snapshot.Hardware;
        bool Has(string key)=>hardware?.available?.GetValueOrDefault(key)==true||hardware?.values.ContainsKey(key)==true;
        string Name(string key,string fallback)=>HardwareNames.Get(names,key)??hardware?.names.GetValueOrDefault(key)??language.T(fallback);
        string Value(string key,string unit,bool current=false) {
            var values=peaks&&!current?snapshot.HardwarePeaks:hardware?.state=="LIVE"?hardware.values:null;
            return values?.TryGetValue(key,out double value)==true?ReadingFormat.SensorNumber(value,unit)+unit:"—";
        }
        void Add(string key,string title,string value,string icon)=>result.Add(new(key,title,value,icon));
        if(snapshot.Fps is {Enabled:true} fps)Add("fps","FPS / AVG / MIN",fps.Status=="Live"?$"{fps.Current} / {fps.Average} / {fps.Minimum}":language.T(fps.Status),"fps");
        string Processor(string temperature,string load,string? fallback=null)=>string.Join("   ",new[]{Has(temperature)?Value(temperature," °C"):null,Has(load)?Value(load,"%"):fallback}.Where(x=>x!=null));
        Add("CPU",language.T("CPU"),Processor("cpu","cpuLoad",peaks?snapshot.PeakCpu:snapshot.Cpu),"cpu");
        if(Has("gpu")||Has("gpuLoad"))Add("GPU",language.T("GPU"),Processor("gpu","gpuLoad"),"gpu");
        if(hardware?.usage.ContainsKey("vram")==true)Add("vram",language.T(hardware.usage["vram"].label??"VRAM"),hardware.state=="LIVE"?ReadingFormat.UsageText(hardware.usage["vram"]):"—","gpu");
        Add("Memory",language.T("Memory"),snapshot.Memory,"memory");
        foreach(string disk in new[]{"diskC","diskD"})if(Has(disk))Add(disk,Name(disk,disk=="diskC"?"Drive 1":"Drive 2"),Value(disk," °C"),"nvme");
        foreach(var fan in new[]{("cpuFan","CPU Fan"),("gpuFan",hardware?.gpuFanCount>1?"GPU Fan 1":"GPU Fan"),("gpuFan2","GPU Fan 2"),("bottom","System Fan 1"),("top","System Fan 2")})
            if(Has(fan.Item1))Add(fan.Item1,Name(fan.Item1,fan.Item2),Value(fan.Item1," RPM"),fan.Item1.StartsWith("gpu")?"gpu":"airflow");
        string Link(string key) =>hardware?.state=="LIVE"&&hardware.values.TryGetValue(key,out double value)?language.T(NetworkRate.Link(value)):"—";
        if(!Has("lanLink")&&!Has("wifiLink")&&Has("netLink"))Add("netConnection",Name("netConnection","Connection"),Link("netLink"),"network");
        foreach(var link in new[]{("lanLink","LAN Link Speed"),("wifiLink","Wi-Fi Link Speed")})if(Has(link.Item1))Add(link.Item1,language.T(link.Item2),Link(link.Item1),"network");
        foreach(string signal in new[]{"wifiSignal","netSignal"})if(Has(signal))Add(signal,language.T("Wi-Fi Signal"),Value(signal,"%",current:true),"network");
        Add("netDown",language.T("Download"),peaks?snapshot.PeakDownload:snapshot.Download,"network");
        Add("netUp",language.T("Upload"),peaks?snapshot.PeakUpload:snapshot.Upload,"network");
        void Quota(QuotaReading? quota,string provider,string icon) {
            if(quota==null)return;string key="quota"+provider;
            var windows=quota.Windows;
            string state=QuotaPresentation.Status(quota,DateTimeOffset.UtcNow);
            if(state!="Live"||windows.Count==0)Add(key,provider,language.T(state),icon);
            else for(int i=0;i<windows.Count;i++) {
                var window=windows[i];
                string value=window.Remaining.HasValue?string.Format(language.T("{0}% left"),window.Remaining.Value.ToString("F1")):"—";
                if(window.Reset.HasValue)value+=" · "+string.Format(language.T("Resets {0}"),window.Reset.Value.ToLocalTime().ToString("g"));
                Add(key+i,(provider=="Antigravity"?"Gemini":provider)+" · "+language.T(window.Label),value,icon);
            }
        }
        Quota(snapshot.CodexQuota,"Codex","codex");Quota(snapshot.ClaudeQuota,"Claude","claude");Quota(snapshot.AntigravityQuota,"Antigravity","antigravity");
        foreach(var sensor in peaks?snapshot.PeakSensors:snapshot.Sensors)Add("sensor:"+sensor.Id,sensor.Label,sensor.Value,"airflow");
        if(hardware!=null&&hardware.state!="LIVE")Add("status",language.T(hardware.state),language.T("Hardware readings unavailable. Waiting for the collector."),"live");
        return result;
    }
}
