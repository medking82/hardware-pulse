namespace HardwarePulse.Desktop;

// Original ReadingColors/QuotaView/DesktopPaletteColor policy, shared by all views.
public sealed record ReadingPalette(bool Unified=false,string Color="#DDE9F0") {
    public static bool IsColor(string? value)=>value is {Length:7}&&value[0]=='#'&&value.Skip(1).All(Uri.IsHexDigit);
    public string ForIcon(string name,bool light=false,string fallback="#F5F7FA") {
        if(name=="fps")return "#9EDFD3";
        if(light)return "#17202B";
        if(Unified)return Color;
        return name switch {
            "cpu" or "codex"=>"#A5E7D5","gpu" or "antigravity"=>"#A7CBFF",
            "memory"=>"#E7C5A4","claude"=>"#E7B497","nvme"=>"#B9B7ED",
            "airflow"=>"#A8D4D0","network" or "ethernet" or "wifi" or "signal"=>"#A9D8E8",_=>fallback};
    }
}
