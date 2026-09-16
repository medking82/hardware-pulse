using System.Text.Json;

namespace HardwarePulse.Desktop;

public sealed class PreviewSettings {
    public double Width=800,Height=560;
    public string Theme="System";
    public string Language="auto";
    public string? Network;
    public bool Codex;
}

// Host-specific persistence. No credentials or installed WPF settings are stored here.
public sealed class PreviewSettingsStore {
    readonly string path;
    Dictionary<string,JsonElement> fields=new();
    bool blocked;
    public string? Error {get;private set;}
    public PreviewSettingsStore(string path){this.path=Path.GetFullPath(path);}
    public static PreviewSettingsStore? Default() {
        string root=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.IsPathFullyQualified(root)?new(Path.Combine(root,"HardwarePulse.Preview","settings.json")):null;
    }
    public PreviewSettings Load() {
        var settings=new PreviewSettings();
        try {
            if(!File.Exists(path))return settings;
            using var stream=File.OpenRead(path);
            if(stream.Length>65536)throw new InvalidDataException();
            fields=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(stream)??throw new InvalidDataException();
            if(fields.TryGetValue("schema",out var schema)&&(!schema.TryGetInt32(out var version)||version!=1))throw new InvalidDataException();
            var map=new Dictionary<string,object>();
            foreach(var field in fields) {
                if(field.Value.ValueKind==JsonValueKind.String)map[field.Key]=field.Value.GetString()!;
                else if(field.Value.ValueKind==JsonValueKind.Number&&field.Value.TryGetDouble(out var number))map[field.Key]=number;
                else if(field.Value.ValueKind==JsonValueKind.True||field.Value.ValueKind==JsonValueKind.False)map[field.Key]=field.Value.GetBoolean();
            }
            var values=new SettingsValues(map);
            settings.Width=values.Number("width",800,360,2400);settings.Height=values.Number("height",560,400,1600);
            string theme=values.Text("theme","System");settings.Theme=theme is "Light" or "Dark"?theme:"System";
            string network=values.Text("network");settings.Network=network.Length>0&&network.Length<=256&&!network.Any(char.IsControl)?network:null;
            settings.Codex=values.Flag("codex");
            string language=values.Text("language","auto");settings.Language=language is "en" or "zh-CN" or "zh-TW"?language:"auto";
        }catch(Exception e) when(e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException) {
            blocked=true;Error="Settings unavailable. Changes apply to this session; the original file is preserved.";
            return new PreviewSettings();
        }
        return settings;
    }
    public bool Save(PreviewSettings settings) {
        if(blocked)return false;
        string? temp=null;
        try {
            string directory=Path.GetDirectoryName(path)!;Directory.CreateDirectory(directory);
            var updated=new Dictionary<string,JsonElement>(fields){
                ["schema"]=JsonSerializer.SerializeToElement(1),["width"]=JsonSerializer.SerializeToElement(settings.Width),
                ["height"]=JsonSerializer.SerializeToElement(settings.Height),["theme"]=JsonSerializer.SerializeToElement(settings.Theme),
                ["network"]=JsonSerializer.SerializeToElement(settings.Network),["codex"]=JsonSerializer.SerializeToElement(settings.Codex),
                ["language"]=JsonSerializer.SerializeToElement(settings.Language)};
            var bytes=JsonSerializer.SerializeToUtf8Bytes(updated);
            if(bytes.Length>65536)throw new InvalidDataException();
            temp=Path.Combine(directory,".settings-"+Guid.NewGuid().ToString("N")+".tmp");
            var options=new FileStreamOptions{Mode=FileMode.CreateNew,Access=FileAccess.Write,Share=FileShare.None};
            if(!OperatingSystem.IsWindows())options.UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite;
            using(var stream=new FileStream(temp,options)){stream.Write(bytes);stream.Flush(true);}
            File.Move(temp,path,true);temp=null;fields=updated;Error=null;return true;
        }catch(Exception e) when(e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException) {
            Error="Could not save settings. Changes apply to this session.";return false;
        }finally {if(temp!=null)try{File.Delete(temp);}catch(IOException){}catch(UnauthorizedAccessException){}}
    }
}
