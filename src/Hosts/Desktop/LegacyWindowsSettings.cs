using System.Text.Json;

namespace HardwarePulse.Desktop;

// Read-only projection. Unknown legacy fields never enter the new profile.
static class LegacyWindowsSettings {
    public static Dictionary<string,JsonElement> Read(string path) {
        for(string? item=path;item!=null;item=Path.GetDirectoryName(item))
            if((File.GetAttributes(item)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Linked legacy settings are not imported");
        using var stream=File.OpenRead(path);
        if(stream.Length>65536)throw new InvalidDataException();
        using var document=JsonDocument.Parse(stream,new(){MaxDepth=32});
        var source=document.RootElement;
        if(source.ValueKind!=JsonValueKind.Object)throw new InvalidDataException();
        var result=new Dictionary<string,JsonElement>();
        void Put<T>(string name,T value)=>result[name]=JsonSerializer.SerializeToElement(value);
        void Copy(string from,string to) {if(source.TryGetProperty(from,out var value))result[to]=value.Clone();}
        bool Flag(JsonElement map,string name,bool fallback=false)=>map.ValueKind==JsonValueKind.Object&&map.TryGetProperty(name,out var value)&&value.ValueKind is JsonValueKind.True or JsonValueKind.False?value.GetBoolean():fallback;
        foreach(string name in new[]{"width","height","language","desktopColumns","desktopShortcut","desktopShortcutEnabled","autoUpdates","autoDownload"})Copy(name,name);
        Copy("opacity","monitorBackgroundOpacity");
        Put("theme","Dark");
        // WPF's Solid switch disables its backdrop and makes the tint opaque.
        Put("monitorBackgroundBlur",!Flag(source,"solid"));
        if(Flag(source,"solid"))Put("monitorBackgroundOpacity",100);
        foreach(var pair in new[]{("desktopWidth","floatingWidth"),("desktopHeight","floatingHeight"),
            ("desktopAlwaysOnTop","floatingTopmost"),("desktopBackgroundOpacity","floatingBackgroundOpacity"),
            ("desktopFontSize","floatingFontSize"),("desktopSpacing","floatingRowSpacing"),
            ("desktopTextOpacity","floatingTextOpacity"),("desktopAppIconColors","floatingIconsFollowApp"),
            ("desktopLocalContrast","floatingLocalContrast"),("quotaCodex","codex"),
            ("quotaClaude","claude"),("quotaAntigravity","antigravity")})Copy(pair.Item1,pair.Item2);
        Put("startupMode",Flag(source,"desktopEnabled")?"Desktop":"Monitor");
        if(!Flag(source,"desktopAutoContrast",!source.TryGetProperty("desktopColor",out _)))Copy("desktopColor","floatingTextColor");
        if(source.TryGetProperty("overlay",out var overlay)&&overlay.ValueKind==JsonValueKind.Object) {
            Put("fps",Flag(overlay,"enabled")&&Flag(overlay,"fps",true));
            if(overlay.TryGetProperty("processName",out var name))result["fpsTarget"]=name.Clone();
        }
        if(Flag(source,"desktopEnabled")&&source.TryGetProperty("desktopVisible",out var visible)&&Flag(visible,"fps"))Put("fps",true);
        return result;
    }
}
