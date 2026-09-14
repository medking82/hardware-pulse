using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace HardwarePulse {
    public sealed class Settings {
        public readonly Dictionary<string,object> Data;readonly string path;
        public Settings(string path){this.path=path;try{Data=Json.Serializer().Deserialize<Dictionary<string,object>>(Json.Read(path))??new Dictionary<string,object>();}catch{Data=new Dictionary<string,object>();}}
        public string Text(string key,string fallback=""){object value;return Data.TryGetValue(key,out value)&&value is string?(string)value:fallback;}
        public bool Flag(string key,bool fallback=false){object value;return Data.TryGetValue(key,out value)&&value is bool?(bool)value:fallback;}
        public double Number(string key,double fallback,double min,double max){object value;double n;if(!Data.TryGetValue(key,out value)||!double.TryParse(Convert.ToString(value,CultureInfo.InvariantCulture),NumberStyles.Float,CultureInfo.InvariantCulture,out n)||double.IsNaN(n)||double.IsInfinity(n))return fallback;return Math.Max(min,Math.Min(max,n));}
        public Dictionary<string,object> Map(string key){object value;var map=Data.TryGetValue(key,out value)?value as Dictionary<string,object>:null;if(map==null){map=new Dictionary<string,object>();Data[key]=map;}return map;}
        public string[] Order(){object value;if(!Data.TryGetValue("cardOrder",out value)||!(value is IEnumerable))return new string[0];return ((IEnumerable)value).Cast<object>().OfType<string>().ToArray();}
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
