namespace HardwarePulse.Desktop;

// Keep the original Desktop per-window identities; accept earlier shared aliases.
static class DesktopQuotaPreferences {
    static readonly string[] groups=["quotaCodex","quotaAntigravity","quotaClaude"];
    public static string Group(string key)=>groups.FirstOrDefault(group=>key==group||key.StartsWith(group,StringComparison.Ordinal)&&
        int.TryParse(key[group.Length..].TrimStart(':'),out int index)&&index>=0)??key;
    public static IEnumerable<string> Expand(string key) {
        string group=Group(key);
        if(key==group&&groups.Contains(group))return group=="quotaCodex"?[group+"0"]:[group+"0",group+"1"];
        return [group!=key?key.Replace(":",""):key];
    }
    public static bool Visible(IReadOnlyDictionary<string,bool> values,string key) {
        string group=Group(key);
        if(key==group&&groups.Contains(group))return Expand(key).Any(window=>Visible(values,window));
        if(values.TryGetValue(key,out bool visible))return visible;
        if(group!=key&&values.TryGetValue(group+":"+key[group.Length..],out visible))return visible;
        return values.GetValueOrDefault(group,true);
    }
    public static int Order(string[] order,string key) {
        int index=Array.IndexOf(order,key);if(index>=0)return index;
        string group=Group(key);
        return order.Select((item,i)=>(item,i)).Where(pair=>Group(pair.item)==group).Select(pair=>pair.i).DefaultIfEmpty(int.MaxValue).Min();
    }
}
