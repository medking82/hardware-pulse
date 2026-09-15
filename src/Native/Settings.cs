using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace HardwarePulse {
    public sealed class Settings : SettingsValues {
        readonly string path;
        public Settings(string path):base(Read(path)){this.path=path;}
        static Dictionary<string,object> Read(string path){try{return Json.Serializer().Deserialize<Dictionary<string,object>>(Json.Read(path))??new Dictionary<string,object>();}catch{return new Dictionary<string,object>();}}
        public void Save(){Json.WriteAtomic(path,Data);}
    }
    public sealed class Languages {
        readonly Dictionary<string,string[]> catalog=new Dictionary<string,string[]>();
        public string Preference="auto";public string Current {get{return Resolve(Preference,CultureInfo.CurrentUICulture.Name);}}
        public Languages(string path){foreach(string line in File.ReadAllLines(path)){var parts=line.Split(new[]{'|'},3);if(parts.Length==3)catalog[parts[0]]=new[]{parts[1],parts[2]};}}
        public static string Resolve(string preference,string culture){if(preference=="auto"){if(Regex.IsMatch(culture,@"^zh-(TW|HK|MO|Hant)",RegexOptions.IgnoreCase))return "zh-TW";if(Regex.IsMatch(culture,@"^zh(?:-|$)",RegexOptions.IgnoreCase))return "zh-CN";return "en";}return preference=="zh-CN"||preference=="zh-TW"?preference:"en";}
        public bool Contains(string text){return catalog.ContainsKey(text);}
        public string T(string text){string[] values;return text!=null&&Current!="en"&&catalog.TryGetValue(text,out values)?values[Current=="zh-TW"?1:0]:text;}
        public string Device(string text){string result=T(text)??"";foreach(string phrase in new[]{"System Temperature","Composite Temperature","Bottom Intake","Top Exhaust","Pump Fan","System Fan","Slots","configured"})result=Regex.Replace(result,@"(?<![\p{L}\p{N}])"+Regex.Escape(phrase)+@"(?![\p{L}\p{N}])",m=>T(phrase));return result;}
    }
}
