namespace HardwarePulse.Desktop;

// The editable identities match the 0.6.27 Hardware Names section.
public static class HardwareNames {
    public static readonly (string Key,string Label)[] Fields=[("CPU","CPU Name"),("GPU","GPU Name"),("Memory","Memory Name"),("NVMe","NVMe Name"),("Airflow","Case / Motherboard"),
        ("ramA","Module 1"),("ramB","Module 2"),("diskC","Drive 1"),("diskD","Drive 2"),("cpuFan","CPU Fan Name"),("bottom","System Fan 1"),("top","System Fan 2")];
    public static string Normalize(string? text)=>text is {Length:<=160}&&!text.Any(char.IsControl)?text.Trim():"";
    public static string? Get(IReadOnlyDictionary<string,string>? names,string key)=>names!=null&&names.TryGetValue(key,out var value)&&value.Length>0?value:null;
}
