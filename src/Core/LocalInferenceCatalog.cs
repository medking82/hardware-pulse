using System.Collections.Generic;
using System.Linq;

namespace HardwarePulse {
    public sealed class LocalInferenceSpec {
        public readonly string Name,Icon,Accent,DefaultEndpoint;
        public LocalInferenceSpec(string name,string icon,string accent,string endpoint){Name=name;Icon=icon;Accent=accent;DefaultEndpoint=endpoint;}
    }
    public static class LocalInferenceCatalog {
        static readonly IList<LocalInferenceSpec> sources=new List<LocalInferenceSpec>{new LocalInferenceSpec("Strata","strata","#10B981","http://127.0.0.1:8080")}.AsReadOnly();
        public static IEnumerable<LocalInferenceSpec> All {get{return sources;}}
        public static LocalInferenceSpec Find(string name){return sources.FirstOrDefault(source=>source.Name==name);}
    }
}
