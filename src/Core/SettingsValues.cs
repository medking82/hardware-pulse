using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HardwarePulse {
    // Storage and defaults belong to the host. Keep unknown fields in the supplied map.
    public class SettingsValues {
        public readonly Dictionary<string,object> Data;
        public SettingsValues(Dictionary<string,object> data){Data=data??new Dictionary<string,object>();}
        public string Text(string key,string fallback=""){object value;return Data.TryGetValue(key,out value)&&value is string?(string)value:fallback;}
        public bool Flag(string key,bool fallback=false){object value;return Data.TryGetValue(key,out value)&&value is bool?(bool)value:fallback;}
        public double Number(string key,double fallback,double min,double max){object value;double n;if(!Data.TryGetValue(key,out value)||!double.TryParse(Convert.ToString(value,CultureInfo.InvariantCulture),NumberStyles.Float,CultureInfo.InvariantCulture,out n)||double.IsNaN(n)||double.IsInfinity(n))return fallback;return Math.Max(min,Math.Min(max,n));}
        public Dictionary<string,object> Map(string key){object value;var map=Data.TryGetValue(key,out value)?value as Dictionary<string,object>:null;if(map==null){map=new Dictionary<string,object>();Data[key]=map;}return map;}
        public string[] Order(){object value;if(!Data.TryGetValue("cardOrder",out value)||!(value is IEnumerable))return new string[0];return ((IEnumerable)value).Cast<object>().OfType<string>().ToArray();}
        // The host supplies supported keys in default order; reading never rewrites saved data.
        public string[] Order(string key,IEnumerable<string> defaults){
            var supported=defaults.ToArray();var result=new List<string>();object value;
            if(Data.TryGetValue(key,out value)&&value is IEnumerable&&!(value is string))
                foreach(object item in (IEnumerable)value){var name=item as string;if(name!=null&&supported.Contains(name)&&!result.Contains(name))result.Add(name);}
            foreach(string name in supported)if(name!=null&&!result.Contains(name))result.Add(name);
            return result.ToArray();
        }
    }
}
