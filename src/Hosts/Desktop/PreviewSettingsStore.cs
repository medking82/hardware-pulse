using System.Text.Json;

namespace HardwarePulse.Desktop;

public sealed class PreviewSettings {
    public double Width=800,Height=560;
    public string Theme="System";
    public string StartupMode="Monitor";
    public string Language="auto";
    public string? Network;
    public bool Codex,Claude,Antigravity;
    public bool Fps;
    public GameOverlayOptions GameOverlay=new();
    public bool AutoUpdates,AutoDownload;
    public bool DesktopShortcutEnabled=true;
    public string DesktopShortcut="Ctrl+Alt+F10";
    public string FpsTarget="";
    public int CardColumns,DesktopColumns;
    public ReadingLayout Cards=new(),DesktopRows=new();
    public double FloatingWidth=440,FloatingHeight=420;
    public int FloatingX,FloatingY;
    public bool FloatingPositionSet,FloatingTopmost;
    public double FloatingBackgroundOpacity=100;
    public double FloatingFontSize=15;
    public bool FloatingBackgroundBlur;
    public bool FloatingLocalContrast;
    public double FloatingTextOpacity=100,FloatingRowSpacing=8;
    public string FloatingTextColor="";
    public bool FloatingIconsFollowApp=true;
    public static bool IsTextColor(string value)=>value.Length==0||(value.Length==7&&value[0]=='#'&&value.Skip(1).All(Uri.IsHexDigit));
}

// Host-specific persistence. Legacy import is a read-only, explicit projection.
public sealed class PreviewSettingsStore {
    readonly string path;
    readonly string? legacyPath;
    bool createImportedFile;
    Dictionary<string,JsonElement> fields=new();
    bool blocked;
    public string? Error {get;private set;}
    public bool LegacyImported {get;private set;}
    public PreviewSettingsStore(string path,string? legacyPath=null){this.path=Path.GetFullPath(path);this.legacyPath=legacyPath==null?null:Path.GetFullPath(legacyPath);if(string.Equals(this.path,this.legacyPath,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))throw new ArgumentException("Profiles must have different paths");}
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
            settings.Width=values.Number("width",800,360,2400);settings.Height=values.Number("height",560,400,1600);
            settings.FloatingWidth=values.Number("floatingWidth",440,360,2400);settings.FloatingHeight=values.Number("floatingHeight",420,240,1600);
            settings.FloatingX=(int)values.Number("floatingX",0,-100000,100000);settings.FloatingY=(int)values.Number("floatingY",0,-100000,100000);
            settings.FloatingPositionSet=values.Flag("floatingPositionSet");settings.FloatingTopmost=values.Flag("floatingTopmost");
            settings.FloatingBackgroundOpacity=values.Number("floatingBackgroundOpacity",100,0,100);
            settings.FloatingFontSize=values.Number("floatingFontSize",15,10,32);
            settings.FloatingBackgroundBlur=values.Flag("floatingBackgroundBlur");
            settings.FloatingLocalContrast=values.Flag("floatingLocalContrast");
            settings.FloatingTextOpacity=values.Number("floatingTextOpacity",100,0,100);
            settings.FloatingRowSpacing=values.Number("floatingRowSpacing",8,0,24);
            string textColor=values.Text("floatingTextColor");settings.FloatingTextColor=PreviewSettings.IsTextColor(textColor)?textColor:"";
            settings.FloatingIconsFollowApp=values.Flag("floatingIconsFollowApp",true);
            string theme=values.Text("theme","System");settings.Theme=theme is "Light" or "Dark"?theme:"System";
            string startup=values.Text("startupMode","Monitor");settings.StartupMode=startup is "Desktop" or "Tray"?startup:"Monitor";
            string network=values.Text("network");settings.Network=network.Length>0&&network.Length<=256&&!network.Any(char.IsControl)?network:null;
            settings.Codex=values.Flag("codex");
            settings.Claude=values.Flag("claude");
            settings.Antigravity=values.Flag("antigravity");
            settings.Fps=values.Flag("fps");
            if(fields.TryGetValue("gameOverlay",out var gameOverlay))settings.GameOverlay=GameOverlayOptions.Read(gameOverlay);
            settings.AutoUpdates=values.Flag("autoUpdates");settings.AutoDownload=values.Flag("autoDownload");
            settings.CardColumns=(int)values.Number("cardColumns",0,0,3);settings.DesktopColumns=(int)values.Number("desktopColumns",0,0,3);
            if(fields.TryGetValue("cardLayout",out var cardLayout))settings.Cards=ReadingLayout.Read(cardLayout);
            if(fields.TryGetValue("desktopLayout",out var desktopLayout))settings.DesktopRows=ReadingLayout.Read(desktopLayout);
            settings.DesktopShortcutEnabled=values.Flag("desktopShortcutEnabled",true);
            string shortcut=values.Text("desktopShortcut","Ctrl+Alt+F10");
            if(WindowsDesktopShortcut.TryParse(shortcut,out _,out _,out _))settings.DesktopShortcut=shortcut;
            string fpsTarget=values.Text("fpsTarget");settings.FpsTarget=fpsTarget.Length<=256&&!fpsTarget.Any(char.IsControl)&&fpsTarget.IndexOfAny(['/', '\\', ':'])<0?fpsTarget:"";
            string language=values.Text("language","auto");settings.Language=language is "en" or "zh-CN" or "zh-TW"?language:"auto";
        }catch(Exception e) when(e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException) {
            blocked=true;Error="Settings unavailable. Changes apply to this session; the original file is preserved.";
            return new PreviewSettings();
        }
        return settings;
    }
    static bool Exists(string file) {
        try {File.GetAttributes(file);return true;}
        catch(FileNotFoundException){return false;}
        catch(DirectoryNotFoundException){return false;}
    }
    public bool Save(PreviewSettings settings) {
        if(blocked)return false;
        string? temp=null;
        try {
            string directory=Path.GetDirectoryName(path)!;Directory.CreateDirectory(directory);
            var updated=new Dictionary<string,JsonElement>(fields){
                ["schema"]=JsonSerializer.SerializeToElement(1),["width"]=JsonSerializer.SerializeToElement(settings.Width),
                ["height"]=JsonSerializer.SerializeToElement(settings.Height),["theme"]=JsonSerializer.SerializeToElement(settings.Theme),
                ["startupMode"]=JsonSerializer.SerializeToElement(settings.StartupMode),
                ["floatingLocalContrast"]=JsonSerializer.SerializeToElement(settings.FloatingLocalContrast),
                ["autoUpdates"]=JsonSerializer.SerializeToElement(settings.AutoUpdates),["autoDownload"]=JsonSerializer.SerializeToElement(settings.AutoDownload),
                ["network"]=JsonSerializer.SerializeToElement(settings.Network),["codex"]=JsonSerializer.SerializeToElement(settings.Codex),
                ["claude"]=JsonSerializer.SerializeToElement(settings.Claude),
                ["antigravity"]=JsonSerializer.SerializeToElement(settings.Antigravity),
                ["fps"]=JsonSerializer.SerializeToElement(settings.Fps),["fpsTarget"]=JsonSerializer.SerializeToElement(settings.FpsTarget),
                ["gameOverlay"]=JsonSerializer.SerializeToElement(settings.GameOverlay),
                ["cardColumns"]=JsonSerializer.SerializeToElement(settings.CardColumns),["desktopColumns"]=JsonSerializer.SerializeToElement(settings.DesktopColumns),
                ["cardLayout"]=JsonSerializer.SerializeToElement(settings.Cards),["desktopLayout"]=JsonSerializer.SerializeToElement(settings.DesktopRows),
                ["desktopShortcutEnabled"]=JsonSerializer.SerializeToElement(settings.DesktopShortcutEnabled),
                ["desktopShortcut"]=JsonSerializer.SerializeToElement(settings.DesktopShortcut),
                ["floatingWidth"]=JsonSerializer.SerializeToElement(settings.FloatingWidth),["floatingHeight"]=JsonSerializer.SerializeToElement(settings.FloatingHeight),
                ["floatingX"]=JsonSerializer.SerializeToElement(settings.FloatingX),["floatingY"]=JsonSerializer.SerializeToElement(settings.FloatingY),
                ["floatingPositionSet"]=JsonSerializer.SerializeToElement(settings.FloatingPositionSet),["floatingTopmost"]=JsonSerializer.SerializeToElement(settings.FloatingTopmost),
                ["floatingBackgroundOpacity"]=JsonSerializer.SerializeToElement(settings.FloatingBackgroundOpacity),
                ["floatingFontSize"]=JsonSerializer.SerializeToElement(settings.FloatingFontSize),
                ["floatingBackgroundBlur"]=JsonSerializer.SerializeToElement(settings.FloatingBackgroundBlur),
                ["floatingTextOpacity"]=JsonSerializer.SerializeToElement(settings.FloatingTextOpacity),
                ["floatingRowSpacing"]=JsonSerializer.SerializeToElement(settings.FloatingRowSpacing),
                ["floatingTextColor"]=JsonSerializer.SerializeToElement(settings.FloatingTextColor),
                ["floatingIconsFollowApp"]=JsonSerializer.SerializeToElement(settings.FloatingIconsFollowApp),
                ["language"]=JsonSerializer.SerializeToElement(settings.Language)};
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
