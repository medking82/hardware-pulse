using System.Text.Json;

namespace HardwarePulse.Desktop;

public sealed class PreviewSettings {
    public static readonly string[] CardKeys=["CPU","GPU","Memory","NVMe","Airflow","Network"];
    public static readonly string[] DesktopKeys=["CPU","GPU","vram","Memory","diskC","diskD","cpuFan","gpuFan","gpuFan2","bottom","top","netConnection","lanLink","wifiLink","wifiSignal","netSignal","netDown","netUp","fps","quotaCodex0","quotaAntigravity0","quotaAntigravity1","quotaClaude0","quotaClaude1"];
    public List<string> DesktopOrder=new(DesktopKeys);
    public Dictionary<string,bool> DesktopVisible=new(StringComparer.Ordinal);
    public List<string> CardOrder=new(CardKeys);
    public HashSet<string> HiddenCards=new(StringComparer.Ordinal);
    public Dictionary<string,string> Names=new(StringComparer.Ordinal);
    public double Width=280,Height=650;
    public int? AppX,AppY;
    public double? LegacyLeft,LegacyTop,LegacyDesktopLeft,LegacyDesktopTop;
    public string Theme="Dark";
    public double AppOpacity=85;
    public string? BackgroundColor;
    public double FontSize=12;
    public double DesktopFontSize=16,DesktopSpacing=14;
    public int DesktopColumns;
    public double DesktopWidth=466,DesktopHeight=400;
    public int? DesktopX,DesktopY;
    public bool DesktopTopmost;
    public bool DesktopEnabled,DesktopLocked=true;
    public double DesktopBackgroundOpacity=86,DesktopOverlayOpacity=55,DesktopTextOpacity=100;
    public bool Solid;
    public bool Details;
    public bool UnifiedReadingColors;
    public string ReadingColor="#DDE9F0",DesktopColor="#F5F7FA";
    public bool DesktopAppIconColors=true;
    public bool DesktopLocalContrast;
    public bool DesktopAutoContrast;
    public bool DesktopShortcutEnabled=true;
    public string DesktopShortcut="Ctrl+Alt+F10";
    public bool Topmost,LockPosition;
    public string Language="auto";
    public string? Network;
    public string NetworkUnit="auto";
    public bool Codex,Claude,Antigravity;
    public bool QuotaFull;
    public bool AutoUpdates,AutoDownload;
    public bool Fps;
    public string FpsTarget="";
    public GameOverlayOptions GameOverlay=new();
}

// Host-specific persistence. A stable first-run import never writes the WPF source.
public sealed class PreviewSettingsStore {
    readonly string path;
    readonly string? legacyPath;
    bool createImportedFile;
    Dictionary<string,JsonElement> fields=new();
    bool blocked;
    public string? Error {get;private set;}
    public bool LegacyImported {get;private set;}
    public PreviewSettingsStore(string path,string? legacyPath=null){
        this.path=Path.GetFullPath(path);this.legacyPath=legacyPath==null?null:Path.GetFullPath(legacyPath);
        if(string.Equals(this.path,this.legacyPath,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))throw new ArgumentException("Profiles must have different paths");
    }
    public static PreviewSettingsStore? Default() {
        return DesktopProfile.CreateStore(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),DesktopProfile.IsInstalledStable);
    }
    public PreviewSettings Load() {
        var settings=new PreviewSettings();
        fields=new();blocked=false;Error=null;LegacyImported=false;createImportedFile=false;
        try {
            if(!Exists(path)) {
                if(legacyPath==null||!Exists(legacyPath))return settings;
                fields=LegacyWindowsSettings.Read(legacyPath);LegacyImported=createImportedFile=true;
            } else {
                if(legacyPath!=null)LegacyWindowsSettings.RequireRegularPath(path);
                using var stream=File.OpenRead(path);
                if(stream.Length>65536)throw new InvalidDataException();
                fields=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(stream)??throw new InvalidDataException();
            }
            if(fields.TryGetValue("schema",out var schema)&&(!schema.TryGetInt32(out var version)||version!=1))throw new InvalidDataException();
            var map=new Dictionary<string,object>();
            foreach(var field in fields) {
                if(field.Value.ValueKind==JsonValueKind.String)map[field.Key]=field.Value.GetString()!;
                else if(field.Value.ValueKind==JsonValueKind.Number&&field.Value.TryGetDouble(out var number))map[field.Key]=number;
                else if(field.Value.ValueKind==JsonValueKind.True||field.Value.ValueKind==JsonValueKind.False)map[field.Key]=field.Value.GetBoolean();
            }
            var values=new SettingsValues(map);
            settings.Width=values.Number("width",280,240,2400);settings.Height=values.Number("height",650,340,1600);
            double? Coordinate(string key)=>map.TryGetValue(key,out var value)&&value is double number&&double.IsFinite(number)&&Math.Abs(number)<=100000?number:null;
            settings.AppX=(int?)Coordinate("appX");settings.AppY=(int?)Coordinate("appY");
            settings.LegacyLeft=Coordinate("legacyLeft");settings.LegacyTop=Coordinate("legacyTop");
            settings.LegacyDesktopLeft=Coordinate("legacyDesktopLeft");settings.LegacyDesktopTop=Coordinate("legacyDesktopTop");
            string theme=values.Text("theme","Dark");settings.Theme=theme is "Light" or "Dark"?theme:"System";
            settings.AppOpacity=values.Number("appOpacity",settings.AppOpacity,0,100);settings.Solid=values.Flag("solid");
            string background=values.Text("background","");settings.BackgroundColor=ReadingPalette.IsColor(background)?background:null;
            settings.Details=values.Flag("details");
            settings.FontSize=values.Number("fontSize",12,10,16);
            settings.DesktopWidth=values.Number("desktopWidth",466,280,10000);settings.DesktopHeight=values.Number("desktopHeight",400,140,10000);
            if(map.ContainsKey("desktopX")&&map.ContainsKey("desktopY")){settings.DesktopX=(int)values.Number("desktopX",0,-100000,100000);settings.DesktopY=(int)values.Number("desktopY",0,-100000,100000);}
            settings.DesktopFontSize=values.Number("desktopFontSize",16,10,32);
            settings.DesktopSpacing=values.Number("desktopSpacing",14,4,40);
            settings.DesktopColumns=(int)values.Number("desktopColumns",0,0,3);
            settings.DesktopTopmost=values.Flag("desktopAlwaysOnTop");
            settings.DesktopEnabled=values.Flag("desktopEnabled");settings.DesktopLocked=values.Flag("desktopLocked",true);
            settings.DesktopBackgroundOpacity=values.Number("desktopBackgroundOpacity",86,0,100);
            settings.DesktopOverlayOpacity=values.Number("desktopOverlayOpacity",55,0,100);
            settings.DesktopTextOpacity=values.Number("desktopTextOpacity",100,0,100);
            settings.Topmost=values.Flag("topmost");settings.LockPosition=values.Flag("lockPosition");
            string[] Cards(string name)=>fields.TryGetValue(name,out var list)&&list.ValueKind==JsonValueKind.Array
                ?list.EnumerateArray().Where(x=>x.ValueKind==JsonValueKind.String).Select(x=>x.GetString()!).Where(PreviewSettings.CardKeys.Contains).Distinct().ToArray():[];
            settings.DesktopOrder=new(PreviewSettings.DesktopKeys);
            if(fields.TryGetValue("desktopOrder",out var order)&&order.ValueKind==JsonValueKind.Array)
                settings.DesktopOrder=order.EnumerateArray().Where(x=>x.ValueKind==JsonValueKind.String).SelectMany(x=>DesktopQuotaPreferences.Expand(x.GetString()!)).Where(PreviewSettings.DesktopKeys.Contains).Concat(PreviewSettings.DesktopKeys).Distinct().ToList();
            if(fields.TryGetValue("desktopVisible",out var visible)&&visible.ValueKind==JsonValueKind.Object)
                foreach(var item in visible.EnumerateObject())if(item.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)settings.DesktopVisible[item.Name]=item.Value.GetBoolean();
            settings.CardOrder=Cards("cardOrder").Concat(PreviewSettings.CardKeys).Distinct().ToList();
            settings.HiddenCards=new(Cards("hiddenCards"),StringComparer.Ordinal);
            if(fields.TryGetValue("names",out var names)&&names.ValueKind==JsonValueKind.Object)
                foreach(var field in HardwareNames.Fields)if(names.TryGetProperty(field.Key,out var value)&&value.ValueKind==JsonValueKind.String) {
                    string name=HardwareNames.Normalize(value.GetString());if(name.Length>0)settings.Names[field.Key]=name;
                }
            string unit=values.Text("networkUnit","auto");settings.NetworkUnit=unit is "KB/s" or "MB/s" or "Mbit/s"?unit:"auto";
            string network=values.Text("network");settings.Network=network.Length>0&&network.Length<=256&&!network.Any(char.IsControl)?network:null;
            settings.Codex=values.Flag("codex");settings.Claude=values.Flag("claude");settings.Antigravity=values.Flag("antigravity");
            settings.QuotaFull=values.Flag("quotaFull");
            settings.AutoUpdates=values.Flag("autoUpdates");settings.AutoDownload=values.Flag("autoDownload");
            settings.UnifiedReadingColors=values.Flag("unifiedReadingColors");
            settings.DesktopAppIconColors=values.Flag("desktopAppIconColors",true);
            settings.DesktopLocalContrast=values.Flag("desktopLocalContrast");
            settings.DesktopAutoContrast=values.Flag("desktopAutoContrast");
            settings.DesktopShortcutEnabled=values.Flag("desktopShortcutEnabled",true);
            string shortcut=values.Text("desktopShortcut",settings.DesktopShortcut);
            if(DesktopShortcutPanel.TryGesture(shortcut,out _,out _,out _))settings.DesktopShortcut=shortcut;
            string readingColor=values.Text("readingColor","#DDE9F0"),desktopColor=values.Text("desktopColor","#F5F7FA");
            if(ReadingPalette.IsColor(readingColor))settings.ReadingColor=readingColor;
            if(ReadingPalette.IsColor(desktopColor))settings.DesktopColor=desktopColor;
            settings.Fps=values.Flag("fps");
            if(fields.TryGetValue("gameOverlay",out var overlay))settings.GameOverlay=GameOverlayOptions.Read(overlay);
            string fpsTarget=values.Text("fpsTarget","");
            settings.FpsTarget=fpsTarget.Length<=256&&!fpsTarget.Any(c=>char.IsControl(c)||"/\\:".Contains(c))?fpsTarget:"";
            string language=values.Text("language","auto");settings.Language=language is "en" or "zh-CN" or "zh-TW"?language:"auto";
        }catch(Exception e) when(e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException) {
            blocked=true;Error="Settings unavailable. Changes apply to this session; the original file is preserved.";
            return new PreviewSettings();
        }
        return settings;
    }
    static bool Exists(string file) {
        try{File.GetAttributes(file);return true;}
        catch(FileNotFoundException){return false;}
        catch(DirectoryNotFoundException){return false;}
    }
    public bool Save(PreviewSettings settings) {
        if(blocked)return false;
        string? temp=null;
        try {
            string directory=Path.GetDirectoryName(path)!;
            if(legacyPath!=null)LegacyWindowsSettings.RequireRegularPath(path);
            Directory.CreateDirectory(directory);
            var updated=new Dictionary<string,JsonElement>(fields){
                ["schema"]=JsonSerializer.SerializeToElement(1),["width"]=JsonSerializer.SerializeToElement(settings.Width),
                ["height"]=JsonSerializer.SerializeToElement(settings.Height),["theme"]=JsonSerializer.SerializeToElement(settings.Theme),
                ["appX"]=JsonSerializer.SerializeToElement(settings.AppX),["appY"]=JsonSerializer.SerializeToElement(settings.AppY),
                ["networkUnit"]=JsonSerializer.SerializeToElement(settings.NetworkUnit),
                ["network"]=JsonSerializer.SerializeToElement(settings.Network),["codex"]=JsonSerializer.SerializeToElement(settings.Codex),
                ["language"]=JsonSerializer.SerializeToElement(settings.Language),["claude"]=JsonSerializer.SerializeToElement(settings.Claude),["antigravity"]=JsonSerializer.SerializeToElement(settings.Antigravity),
                ["appOpacity"]=JsonSerializer.SerializeToElement(settings.AppOpacity),["solid"]=JsonSerializer.SerializeToElement(settings.Solid),
                ["background"]=JsonSerializer.SerializeToElement(settings.BackgroundColor),
                ["desktopAutoContrast"]=JsonSerializer.SerializeToElement(settings.DesktopAutoContrast),
                ["desktopShortcutEnabled"]=JsonSerializer.SerializeToElement(settings.DesktopShortcutEnabled),["desktopShortcut"]=JsonSerializer.SerializeToElement(settings.DesktopShortcut),
                ["details"]=JsonSerializer.SerializeToElement(settings.Details),
                ["quotaFull"]=JsonSerializer.SerializeToElement(settings.QuotaFull),
                ["autoUpdates"]=JsonSerializer.SerializeToElement(settings.AutoUpdates),["autoDownload"]=JsonSerializer.SerializeToElement(settings.AutoDownload),
                ["unifiedReadingColors"]=JsonSerializer.SerializeToElement(settings.UnifiedReadingColors),["readingColor"]=JsonSerializer.SerializeToElement(settings.ReadingColor),
                ["desktopAppIconColors"]=JsonSerializer.SerializeToElement(settings.DesktopAppIconColors),["desktopColor"]=JsonSerializer.SerializeToElement(settings.DesktopColor),
                ["desktopLocalContrast"]=JsonSerializer.SerializeToElement(settings.DesktopLocalContrast),
                ["fps"]=JsonSerializer.SerializeToElement(settings.Fps),["fpsTarget"]=JsonSerializer.SerializeToElement(settings.FpsTarget),
                ["gameOverlay"]=JsonSerializer.SerializeToElement(settings.GameOverlay),
                ["fontSize"]=JsonSerializer.SerializeToElement(settings.FontSize),
                ["desktopOrder"]=JsonSerializer.SerializeToElement(settings.DesktopOrder),["desktopVisible"]=JsonSerializer.SerializeToElement(settings.DesktopVisible),
                ["desktopWidth"]=JsonSerializer.SerializeToElement(settings.DesktopWidth),["desktopHeight"]=JsonSerializer.SerializeToElement(settings.DesktopHeight),
                ["desktopX"]=JsonSerializer.SerializeToElement(settings.DesktopX),["desktopY"]=JsonSerializer.SerializeToElement(settings.DesktopY),
                ["desktopFontSize"]=JsonSerializer.SerializeToElement(settings.DesktopFontSize),
                ["desktopSpacing"]=JsonSerializer.SerializeToElement(settings.DesktopSpacing),
                ["desktopColumns"]=JsonSerializer.SerializeToElement(settings.DesktopColumns),
                ["desktopAlwaysOnTop"]=JsonSerializer.SerializeToElement(settings.DesktopTopmost),
                ["desktopEnabled"]=JsonSerializer.SerializeToElement(settings.DesktopEnabled),["desktopLocked"]=JsonSerializer.SerializeToElement(settings.DesktopLocked),
                ["desktopBackgroundOpacity"]=JsonSerializer.SerializeToElement(settings.DesktopBackgroundOpacity),
                ["desktopOverlayOpacity"]=JsonSerializer.SerializeToElement(settings.DesktopOverlayOpacity),
                ["desktopTextOpacity"]=JsonSerializer.SerializeToElement(settings.DesktopTextOpacity),
                ["cardOrder"]=JsonSerializer.SerializeToElement(settings.CardOrder),["hiddenCards"]=JsonSerializer.SerializeToElement(settings.HiddenCards),
                ["names"]=JsonSerializer.SerializeToElement(settings.Names),
                ["topmost"]=JsonSerializer.SerializeToElement(settings.Topmost),["lockPosition"]=JsonSerializer.SerializeToElement(settings.LockPosition)};
            var bytes=JsonSerializer.SerializeToUtf8Bytes(updated);
            if(bytes.Length>65536)throw new InvalidDataException();
            temp=Path.Combine(directory,".settings-"+Guid.NewGuid().ToString("N")+".tmp");
            var options=new FileStreamOptions{Mode=FileMode.CreateNew,Access=FileAccess.Write,Share=FileShare.None};
            if(!OperatingSystem.IsWindows())options.UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite;
            using(var stream=new FileStream(temp,options)){stream.Write(bytes);stream.Flush(true);}
            File.Move(temp,path,!createImportedFile);temp=null;createImportedFile=false;fields=updated;Error=null;return true;
        }catch(Exception e) when(e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException) {
            Error="Could not save settings. Changes apply to this session.";return false;
        }finally {if(temp!=null)try{File.Delete(temp);}catch(IOException){}catch(UnauthorizedAccessException){}}
    }
}
