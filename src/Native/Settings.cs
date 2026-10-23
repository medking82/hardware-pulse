using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace HardwarePulse {
    public sealed class Settings : SettingsValues {
        readonly string path;
        string savedJson;
        public bool LoadFailed {get;private set;}
        public bool ChangedOnDisk {get;private set;}
        public Settings(string path):base(null) {
            this.path=path;
            try {
                savedJson=Json.Read(path);
                var saved=Json.Serializer().DeserializeObject(savedJson) as Dictionary<string,object>;
                if(saved==null)throw new InvalidDataException("Settings must be a JSON object.");
                foreach(var entry in saved)Data.Add(entry.Key,entry.Value);
            } catch(FileNotFoundException) {
                // A new profile starts empty and may be saved normally.
            } catch(DirectoryNotFoundException) {
            } catch {
                // Defaults are safe to display, but must never replace an unread profile.
                LoadFailed=true;
            }
        }
        public void CheckForChanges() {
            if(LoadFailed||ChangedOnDisk)return;
            string current=null;
            try{current=Json.Read(path);}catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
            // Content, not timestamps: restores can retain an old modification time.
            ChangedOnDisk=!string.Equals(current,savedJson,StringComparison.Ordinal);
        }
        public void Save() {
            if(LoadFailed)throw new InvalidDataException("Original settings could not be read; refusing to overwrite them.");
            CheckForChanges();
            if(ChangedOnDisk)throw new IOException("Settings changed on disk; reopen Hardware Pulse before saving.");
            string next=Json.Serializer().Serialize(Data);
            Json.WriteAtomic(path,Data);
            savedJson=next;
        }
    }
    public sealed class Languages {
        readonly Dictionary<string,string[]> catalog=new Dictionary<string,string[]>();
        public string Preference="auto";public string Current {get{return Resolve(Preference,CultureInfo.CurrentUICulture.Name);}}
        public Languages(string path){foreach(string line in File.ReadAllLines(path)){var parts=line.Split(new[]{'|'},3);if(parts.Length==3)catalog[parts[0]]=new[]{parts[1],parts[2]};}}
        public static string Resolve(string preference,string culture){if(preference=="auto"){if(Regex.IsMatch(culture,@"^zh-(TW|HK|MO|Hant)",RegexOptions.IgnoreCase))return "zh-TW";if(Regex.IsMatch(culture,@"^zh(?:-|$)",RegexOptions.IgnoreCase))return "zh-CN";return "en";}return preference=="zh-CN"||preference=="zh-TW"?preference:"en";}
        public bool Contains(string text){return catalog.ContainsKey(text);}
        public string T(string text){string[] values;return text!=null&&Current!="en"&&catalog.TryGetValue(text,out values)?values[Current=="zh-TW"?1:0]:text;}
        // Presentation only: never change translation keys, persisted values, brands or units.
        public string Label(string text){
            string translated=T(text);
            if(Current!="en"||text==null||!catalog.ContainsKey(text)||text.Length>64||text.Split(' ').Length>8||text.IndexOfAny(new[]{'.',';','!','?'})>=0)return translated;
            return Regex.Replace(translated,@"(?<![\p{L}\p{N}])\p{Ll}",m=>m.Value.ToUpperInvariant());
        }
        public string Device(string text){string result=T(text)??"";foreach(string phrase in new[]{"System Temperature","Composite Temperature","Bottom Intake","Top Exhaust","Pump Fan","System Fan","Slots","configured"})result=Regex.Replace(result,@"(?<![\p{L}\p{N}])"+Regex.Escape(phrase)+@"(?![\p{L}\p{N}])",m=>T(phrase));return result;}
    }
}
