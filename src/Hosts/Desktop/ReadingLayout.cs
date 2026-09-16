using System.Text.Json;
using Avalonia.Controls;

namespace HardwarePulse.Desktop;

public sealed class ReadingLayout {
    public List<string> Order {get;set;}=[];
    public HashSet<string> Hidden {get;set;}=[];
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string,JsonElement> Extra {get;set;}=new();
    public string[] Arrange(IEnumerable<string> available) {
        var keys=available.Distinct().ToArray();var set=keys.ToHashSet();
        return Order.Where(set.Contains).Concat(keys.Where(x=>!Order.Contains(x))).Distinct().ToArray();
    }
    public bool Move(string id,int direction,IEnumerable<string> available) {
        var visible=Arrange(available);int at=Array.IndexOf(visible,id),to=at+direction;
        if(at<0||to<0||to>=visible.Length)return false;
        foreach(var key in visible)if(!Order.Contains(key))Order.Add(key);
        int a=Order.IndexOf(id),b=Order.IndexOf(visible[to]);(Order[a],Order[b])=(Order[b],Order[a]);return true;
    }
    public void Apply(Panel panel,IReadOnlyDictionary<string,Control> controls,Func<string,bool>? available=null) {
        int index=0;
        foreach(string id in Arrange(controls.Keys)) {
            var child=controls[id];child.IsVisible=!Hidden.Contains(id)&&(available?.Invoke(id)??true);
            int old=panel.Children.IndexOf(child);
            if(old!=index){if(old>=0)panel.Children.RemoveAt(old);panel.Children.Insert(index,child);}index++;
        }
    }
    public static ReadingLayout Read(JsonElement value) {
        var result=new ReadingLayout();
        if(value.ValueKind!=JsonValueKind.Object)return result;
        string[] Keys(string name)=>value.TryGetProperty(name,out var list)&&list.ValueKind==JsonValueKind.Array?
            list.EnumerateArray().Where(x=>x.ValueKind==JsonValueKind.String).Select(x=>x.GetString()!).Where(x=>x.Length is >0 and <=512&&!x.Any(char.IsControl)).Distinct().Take(512).ToArray():[];
        result.Order=Keys("Order").ToList();result.Hidden=Keys("Hidden").ToHashSet();
        foreach(var property in value.EnumerateObject())if(property.Name is not "Order" and not "Hidden")result.Extra[property.Name]=property.Value.Clone();return result;
    }
}

// The same editor serves card groups and individual Desktop metrics. No sampling.
public sealed class ReadingLayoutEditor : StackPanel {
    readonly ReadingLayout preferences;
    readonly Action changed;
    readonly UiLanguage language;
    readonly Dictionary<string,Control> entries=new();
    readonly Dictionary<string,TextBlock> labels=new();
    public ReadingLayoutEditor(ReadingLayout preferences,UiLanguage language,Action changed){this.preferences=preferences;this.language=language;this.changed=changed;Spacing=8;}
    public void Present(IEnumerable<(string Id,string Label)> items) {
        var active=items.DistinctBy(x=>x.Id).ToArray();var ids=active.Select(x=>x.Id).ToHashSet();
        foreach(var old in entries.Keys.Where(x=>!ids.Contains(x)).ToArray()){Children.Remove(entries[old]);entries.Remove(old);labels.Remove(old);}
        foreach(var item in active) {
            if(!entries.ContainsKey(item.Id)) {
                string id=item.Id;
                var row=new Grid{ColumnDefinitions=new("Auto,*,Auto,Auto"),ColumnSpacing=8,Tag=id};
                var toggle=new CheckBox{Name="ShowReading",IsChecked=!preferences.Hidden.Contains(id)};
                var label=new TextBlock{TextWrapping=Avalonia.Media.TextWrapping.Wrap,VerticalAlignment=Avalonia.Layout.VerticalAlignment.Center};
                var up=new Button{Name="MoveReadingUp",Content="↑",Width=32,Padding=new Avalonia.Thickness(0),HorizontalContentAlignment=Avalonia.Layout.HorizontalAlignment.Center};
                var down=new Button{Name="MoveReadingDown",Content="↓",Width=32,Padding=new Avalonia.Thickness(0),HorizontalContentAlignment=Avalonia.Layout.HorizontalAlignment.Center};
                toggle.IsCheckedChanged+=(_,_)=>{if(toggle.IsChecked==true)preferences.Hidden.Remove(id);else preferences.Hidden.Add(id);changed();};
                void Move(int delta){if(preferences.Move(id,delta,entries.Keys)){Reorder();changed();}}
                up.Click+=(_,_)=>{Move(-1);if(up.IsEnabled)up.Focus();else down.Focus();};down.Click+=(_,_)=>{Move(1);if(down.IsEnabled)down.Focus();else up.Focus();};
                Grid.SetColumn(label,1);Grid.SetColumn(up,2);Grid.SetColumn(down,3);
                row.Children.Add(toggle);row.Children.Add(label);row.Children.Add(up);row.Children.Add(down);
                entries.Add(id,row);labels.Add(id,label);
            }
            labels[item.Id].Text=item.Label;
            var check=((Grid)entries[item.Id]).Children[0];
            Avalonia.Automation.AutomationProperties.SetName(check,language.T("Show reading")+" · "+item.Label);
            for(int i=2;i<=3;i++){var button=((Grid)entries[item.Id]).Children[i];string action=language.T(i==2?"Move up":"Move down");Avalonia.Automation.AutomationProperties.SetName(button,action+" · "+item.Label);ToolTip.SetTip(button,action);}
        }
        Reorder();
    }
    void Reorder() {
        int index=0;
        foreach(string id in preferences.Arrange(entries.Keys)) {
            var row=entries[id];int old=Children.IndexOf(row);
            if(old!=index){if(old>=0)Children.RemoveAt(old);Children.Insert(index,row);}
            ((Button)((Grid)row).Children[2]).IsEnabled=index>0;((Button)((Grid)row).Children[3]).IsEnabled=index<entries.Count-1;index++;
        }
    }
}
