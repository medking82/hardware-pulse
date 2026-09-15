using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace HardwarePulse {
    // Windows wire parsing remains bounded before the decoded graph enters Core.
    public static class QuotaData {
        public static Dictionary<string,object> Parse(string text){return new JavaScriptSerializer{MaxJsonLength=1048576,RecursionLimit=32}.Deserialize<Dictionary<string,object>>(text);}
    }
}