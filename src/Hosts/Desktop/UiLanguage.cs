using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;

namespace HardwarePulse.Desktop;

// Per-window presentation state. Never translate device identities or persisted keys.
public sealed class UiLanguage {
    static readonly Dictionary<string,string> chinese=Load("Chinese.txt"),traditional=Load("ChineseTraditional.txt");
    readonly ConditionalWeakTable<Control,Entry> labels=new();
    readonly string systemLanguage;
    sealed class Entry(string key){public string Key=key;}
    public string Choice {get;private set;}="en";
    public string EffectiveLanguage {get;private set;}="en";
    public event Action? Changed;
    public UiLanguage(string choice="auto",string? systemLanguage=null){this.systemLanguage=systemLanguage??CultureInfo.CurrentUICulture.Name;Select(choice);}
    static Dictionary<string,string> Load(string name) {
        using var stream=typeof(UiLanguage).Assembly.GetManifestResourceStream("Pulse.Desktop."+name)!;
        using var reader=new StreamReader(stream);var result=new Dictionary<string,string>();
        while(reader.ReadLine() is string line){if(line.Length==0)continue;var parts=line.Split('|');if(parts.Length!=2)throw new InvalidDataException("Invalid UI language entry");result.Add(parts[0],parts[1]);}
        return result;
    }
    public string T(string key)=>EffectiveLanguage!="en"&&(EffectiveLanguage=="zh-TW"?traditional:chinese).TryGetValue(key,out var text)?text:key;
    internal static IEnumerable<string> Catalog(string language)=>language=="en"?chinese.Keys:language=="zh-TW"?traditional.Values:chinese.Values;
    static string ResolveSystem(string name) {
        var parts=name.Replace('_','-').Split('-');
        if(!parts[0].Equals("zh",StringComparison.OrdinalIgnoreCase))return "en";
        bool Has(string part)=>parts.Contains(part,StringComparer.OrdinalIgnoreCase);
        // An explicit script takes precedence over region (for example zh-Hans-HK).
        if(Has("Hant")||Has("CHT"))return "zh-TW";
        if(Has("Hans")||Has("CHS"))return "zh-CN";
        return Has("TW")||Has("HK")||Has("MO")?"zh-TW":"zh-CN";
    }
    public void Select(string choice) {
        Choice=choice is "en" or "zh-CN" or "zh-TW"?choice:"auto";
        EffectiveLanguage=Choice=="auto"?ResolveSystem(systemLanguage):Choice;
        foreach(var pair in labels)Apply(pair.Key,T(pair.Value.Key));
        Changed?.Invoke();
    }
    public TControl Set<TControl>(TControl control,string? key) where TControl:Control {
        key??="";
        labels.GetValue(control,_=>new Entry(key)).Key=key;Apply(control,T(key));return control;
    }
    static void Apply(Control control,string text) {
        switch(control){case Window w:w.Title=text;break;case TextBlock label:label.Text=text;break;case HeaderedContentControl item:item.Header=text;break;case ContentControl content:content.Content=text;break;default:throw new ArgumentException("Unsupported localized control");}
    }
    public IDataTemplate Choices()=>new FuncDataTemplate<string>((value,_)=>Set(new TextBlock(),value??""));
}
