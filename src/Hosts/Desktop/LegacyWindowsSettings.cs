using System.Text.Json;

namespace HardwarePulse.Desktop;

// One-way projection from the 0.6.27 profile. Source bytes are never rewritten.
static class LegacyWindowsSettings {
    public static void RequireRegularPath(string path) {
        for(string? item=path;item!=null;item=Path.GetDirectoryName(item)) {
            try {if((File.GetAttributes(item)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Linked settings paths are not supported");}
            catch(FileNotFoundException){}
            catch(DirectoryNotFoundException){}
        }
    }
    public static Dictionary<string,JsonElement> Read(string path) {
        RequireRegularPath(path);
        using var stream=File.OpenRead(path);
        if(stream.Length>65536)throw new InvalidDataException();
        using var document=JsonDocument.Parse(stream,new(){MaxDepth=32});
        var source=document.RootElement;
        if(source.ValueKind!=JsonValueKind.Object)throw new InvalidDataException();
        var result=new Dictionary<string,JsonElement>();
        void Put<T>(string name,T value)=>result[name]=JsonSerializer.SerializeToElement(value);
        void Copy(string from,string to) {if(source.TryGetProperty(from,out var value))result[to]=value.Clone();}
        bool Flag(JsonElement map,string name,bool fallback=false)=>map.ValueKind==JsonValueKind.Object&&map.TryGetProperty(name,out var value)&&value.ValueKind is JsonValueKind.True or JsonValueKind.False?value.GetBoolean():fallback;
        foreach(string name in new[]{"width","height","language","fontSize","solid","background","details","cardOrder","networkUnit",
            "desktopWidth","desktopHeight","desktopFontSize","desktopSpacing","desktopColumns","desktopEnabled","desktopLocked",
            "desktopAlwaysOnTop","desktopBackgroundOpacity","desktopOverlayOpacity","desktopTextOpacity","desktopColor",
            "desktopAppIconColors","desktopAutoContrast","desktopLocalContrast","desktopShortcut","desktopShortcutEnabled",
            "unifiedReadingColors","readingColor","quotaFull","autoUpdates","autoDownload"})Copy(name,name);
        foreach(var pair in new[]{("opacity","appOpacity"),("pin","topmost"),("positionLocked","lockPosition"),
            ("quotaCodex","codex"),("quotaClaude","claude"),("quotaAntigravity","antigravity")})Copy(pair.Item1,pair.Item2);
        if(!source.TryGetProperty("fontSize",out _)&&Flag(source,"large"))Put("fontSize",14);
        if(source.TryGetProperty("background",out var background)&&background.ValueKind==JsonValueKind.String&&ReadingPalette.IsColor(background.GetString())) {
            var color=Avalonia.Media.Color.Parse(background.GetString()!);
            Put("theme",(.2126*color.R+.7152*color.G+.0722*color.B)/255>.55?"Light":"Dark");
        }
        if(source.TryGetProperty("cardsVisible",out var visible)&&visible.ValueKind==JsonValueKind.Object)
            Put("hiddenCards",PreviewSettings.CardKeys.Where(key=>!Flag(visible,key,true)).ToArray());
        Copy("desktopOrder","desktopOrder");Copy("desktopVisible","desktopVisible");
        Copy("desktopOrder","legacyDesktopOrder");Copy("desktopVisible","legacyDesktopVisible");
        // Retain recognized legacy geometry for the explicit DPI migration
        // step. Do not reinterpret WPF DIPs as physical pixels or drop these values.
        foreach(string name in new[]{"left","top","desktopLeft","desktopTop"})Copy(name,"legacy"+char.ToUpperInvariant(name[0])+name[1..]);
        if(source.TryGetProperty("names",out var names)&&names.ValueKind==JsonValueKind.Object) {
            var retained=new Dictionary<string,string>();
            var keys=HardwareNames.Fields.Select(field=>field.Key).ToArray();
            foreach(var name in names.EnumerateObject())if(keys.Contains(name.Name)&&name.Value.ValueKind==JsonValueKind.String) {
                string value=name.Value.GetString()!;if(value.Length<=160&&!value.Any(char.IsControl))retained[name.Name]=value;
            }
            Put("names",retained);
        }
        if(source.TryGetProperty("overlay",out var overlay)&&overlay.ValueKind==JsonValueKind.Object) {
            var mapped=new Dictionary<string,JsonElement>();
            foreach(var pair in new[]{("enabled","Enabled"),("detail","Detailed"),("fps","Fps"),("cpu","Cpu"),("gpu","Gpu"),
                ("memory","Memory"),("fans","Fans"),("storage","Storage"),("position","Position"),("background","Background"),("opacity","Opacity")})
                if(overlay.TryGetProperty(pair.Item1,out var value))mapped[pair.Item2]=value.Clone();
            Put("gameOverlay",mapped);
            Put("fps",Flag(overlay,"enabled")&&Flag(overlay,"fps",true));
            if(overlay.TryGetProperty("processName",out var name))result["fpsTarget"]=name.Clone();
        }
        if(Flag(source,"desktopEnabled")&&source.TryGetProperty("desktopVisible",out var metrics)&&Flag(metrics,"fps"))Put("fps",true);
        return result;
    }
}
