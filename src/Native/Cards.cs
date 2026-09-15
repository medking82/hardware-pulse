using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace HardwarePulse {
    public sealed partial class Shell {
        sealed class RowView {
            public string Key,Label,Unit;public Grid Full;public StackPanel Compact;
            public TextBlock Name,Value,ShortName,ShortValue;
        }
        sealed class PairView {public string Key,Label;public StackPanel Panel;public TextBlock Name,Value;}
        sealed class CardView {
            public string Key,Subtitle,Hero,Accent,Usage,DisplayAccent;
            public Border Border;public StackPanel Body,TitleRow;public Grid Header,Compact,Pairs;
            public TextBlock Title,Sub,HeroValue,UsageText;public Grid UsageBar;
            public Thumb Grip;public MenuItem Up,Down;
            public List<RowView> Rows=new List<RowView>();public List<PairView> PairItems=new List<PairView>();
        }
        TextBlock Label(string text,double size=12,string color=null){return new TextBlock {Text=text,FontSize=size,Foreground=color==null?Window.Foreground:Brush(color),VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.NoWrap,TextTrimming=TextTrimming.CharacterEllipsis};}
        string Device(string key,string fallback){object saved;if(settings.Map("names").TryGetValue(key,out saved)&&saved is string&&!string.IsNullOrWhiteSpace((string)saved))return (string)saved;string automatic;return language.Device(readings.Latest.names.TryGetValue(key,out automatic)?automatic:fallback);}
        string Value(string key,string unit){var values=maximum&&unit!="link"?readings.Peaks:(IReadOnlyDictionary<string,double>)readings.Latest.values;double value;if(!values.TryGetValue(key,out value))return "—";if(unit=="link")return language.T(NetworkRate.Link(value));if(unit=="rate")return NetworkRate.Format(value,settings.Text("networkUnit","auto"));return value.ToString(unit=="V"?"F3":unit=="RPM"?"F0":"F1")+" "+unit;}
        bool Available(string key){if(key=="lanLink"||key=="wifiLink"||key=="wifiSignal")return readings.Latest.values.ContainsKey(key)||readings.Latest.available!=null&&readings.Latest.available.ContainsKey(key)&&readings.Latest.available[key];if(key=="netLink")return !Available("lanLink")&&!Available("wifiLink")&&(Available("netDown")||Available("netUp"));if(key=="netDown"||key=="netUp")return readings.Latest.values.ContainsKey(key)||readings.Latest.available!=null&&readings.Latest.available.ContainsKey(key)&&readings.Latest.available[key];return readings.Latest.available==null||!readings.Latest.available.ContainsKey(key)||readings.Latest.available[key];}
        void BuildCards(){
            AddCard("CPU","Processor","#A5E7D5","cpu",new[]{new[]{"Utilization","cpuLoad","%"},new[]{"Vcore · Motherboard","vcore","V"},new[]{"CPU Fan","cpuFan","RPM"}});
            AddCard("GPU","Graphics","#A7CBFF","gpu",new[]{new[]{"Utilization","gpuLoad","%"},new[]{"VRAM Junction","vram","°C"},new[]{"Core Voltage","gpuVolt","V"},new[]{"Fan Speed","gpuFan","RPM"}});
            AddCard("Memory","Memory","#E7C5A4",null,new string[0][]);AddPairs(views["Memory"],new[]{"ramA","ramB"},new[]{"Module 1","Module 2"});
            AddCard("NVMe","NVMe · Composite Temperature","#B9B7ED",null,new string[0][]);AddPairs(views["NVMe"],new[]{"diskC","diskD"},new[]{"Drive 1","Drive 2"});
            AddCard("Airflow","Case / Motherboard","#A8D4D0","system",new[]{new[]{"System Fan 1","bottom","RPM"},new[]{"System Fan 2","top","RPM"}});
            AddCard("Network","Active adapter","#A9D8E8",null,new[]{new[]{"LAN Link Speed","lanLink","link"},new[]{"Wi-Fi Link Speed","wifiLink","link"},new[]{"Wi-Fi Signal","wifiSignal","%"},new[]{"Link Speed","netLink","link"},new[]{"Download","netDown","rate"},new[]{"Upload","netUp","rate"}});
            AddUsage(views["Memory"],"ram");AddUsage(views["GPU"],"vram");
            foreach(string key in settings.Order().Concat(new[]{"CPU","GPU","Memory","NVMe","Airflow","Network"}).Distinct())if(views.ContainsKey(key))cards.Children.Add(views[key].Border);
            foreach(var view in views.Values){
                var check=new CheckBox {Content=view.Key,Margin=new Thickness(0,5,0,5),IsChecked=CardEnabled(view.Key)};
                Catalog(check);check.Click+=delegate{settings.Map("cardsVisible")[view.Key]=check.IsChecked==true;UpdateCard(view);ApplyDensity();QueueSave();};Control<StackPanel>("CardOptions").Children.Add(check);
            }
        }
        bool CardEnabled(string key){object value;return !settings.Map("cardsVisible").TryGetValue(key,out value)||!(value is bool)||(bool)value;}
        void AddCard(string key,string subtitle,string accent,string hero,string[][] rows){
            var view=new CardView {Key=key,Subtitle=subtitle,Hero=hero,Accent=accent};views[key]=view;
            view.Border=new Border {Tag=key,CornerRadius=new CornerRadius(14),BorderThickness=new Thickness(1),BorderBrush=Brush("#426D8B9F"),Background=Brush("#3031485B"),Padding=new Thickness(10,7,10,7),Margin=new Thickness(0,0,0,6)};
            view.Body=new StackPanel();view.Border.Child=view.Body;view.Header=new Grid();view.Header.ColumnDefinitions.Add(new ColumnDefinition());view.Header.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});view.Header.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});view.Header.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
            view.TitleRow=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
            view.Grip=new Thumb {Width=16,Height=20,Margin=new Thickness(0,0,6,0),Cursor=Cursors.SizeAll,Focusable=true,Foreground=Brush("#C2D8E5"),ToolTip="Drag To Reorder (Esc To Cancel)"};Catalog(view.Grip);
            view.Grip.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" TargetType=\"Thumb\"><Border Background=\"Transparent\" Padding=\"3\"><Path Data=\"M8 5H9 M15 5H16 M8 12H9 M15 12H16 M8 19H9 M15 19H16\" Stroke=\"{TemplateBinding Foreground}\" StrokeThickness=\"2\" StrokeStartLineCap=\"Round\" StrokeEndLineCap=\"Round\" Stretch=\"Uniform\"/></Border></ControlTemplate>");
            view.TitleRow.Children.Add(view.Grip);var icon=Icon(key.ToLowerInvariant(),18,accent);icon.Margin=new Thickness(0,0,8,0);view.TitleRow.Children.Add(icon);view.Title=Label(key,13);view.Title.FontWeight=FontWeights.SemiBold;view.TitleRow.Children.Add(view.Title);view.Header.Children.Add(view.TitleRow);
            if(hero!=null){view.HeroValue=Label("—",23,accent);System.Windows.Documents.Typography.SetNumeralAlignment(view.HeroValue,FontNumeralAlignment.Tabular);Grid.SetColumn(view.HeroValue,1);view.Header.Children.Add(view.HeroValue);}view.Body.Children.Add(view.Header);
            view.Sub=Label(subtitle,10,"#B0C4DE");view.Sub.Margin=new Thickness(0,3,0,6);view.Body.Children.Add(view.Sub);
            view.Compact=new Grid {Margin=new Thickness(0,2,0,0)};view.Compact.ColumnDefinitions.Add(new ColumnDefinition());view.Compact.ColumnDefinitions.Add(new ColumnDefinition());view.Body.Children.Add(view.Compact);
            foreach(var row in rows){
                var r=new RowView {Label=row[0],Key=row[1],Unit=row[2],Full=new Grid {Margin=new Thickness(0,2,0,2)}};
                r.Full.ColumnDefinitions.Add(new ColumnDefinition());r.Full.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});r.Name=Label(row[0],12,"#C0D0DD");r.Name.Margin=new Thickness(0,0,10,0);r.Value=Label("—");r.Value.FontWeight=FontWeights.SemiBold;System.Windows.Documents.Typography.SetNumeralAlignment(r.Value,FontNumeralAlignment.Tabular);Grid.SetColumn(r.Value,1);r.Full.Children.Add(r.Name);r.Full.Children.Add(r.Value);view.Body.Children.Add(r.Full);
                string shortLabel=row[1]=="cpuLoad"||row[1]=="gpuLoad"?"Load":row[1]=="vcore"?"Vcore":row[1]=="gpuVolt"?"Voltage":row[1]=="vram"?"VRAM Temp":row[2]=="RPM"?"Fan":row[0];
                r.Compact=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,1,5,1)};r.ShortName=Label(shortLabel,10);r.ShortName.Margin=new Thickness(0,0,4,0);r.ShortValue=Label("—",11);r.ShortValue.FontWeight=FontWeights.SemiBold;r.Compact.Children.Add(r.ShortName);r.Compact.Children.Add(r.ShortValue);Catalog(r.ShortName);view.Compact.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});view.Compact.Children.Add(r.Compact);view.Rows.Add(r);
            }
            var menu=new ContextMenu();view.Up=new MenuItem {Header="Move Up"};view.Down=new MenuItem {Header="Move Down"};Catalog(view.Up);Catalog(view.Down);menu.Items.Add(view.Up);menu.Items.Add(view.Down);view.Border.ContextMenu=menu;view.Grip.ContextMenu=menu;menu.Opened+=delegate{int i=cards.Children.IndexOf(view.Border);view.Up.IsEnabled=!locked&&i>0;view.Down.IsEnabled=!locked&&i<cards.Children.Count-1;};view.Up.Click+=delegate{MoveCard(view,-1);};view.Down.Click+=delegate{MoveCard(view,1);};
            ((ResponsivePanel)cards).Attach(view.Border,view.Grip,Control<ScrollViewer>("CardScroll"),delegate{QueueSave();});
        }
        void MoveCard(CardView view,int delta){if(locked)return;int i=cards.Children.IndexOf(view.Border),target=i+delta;if(target<0||target>=cards.Children.Count)return;cards.Children.RemoveAt(i);cards.Children.Insert(target,view.Border);QueueSave();}
        void AddPairs(CardView view,string[] keys,string[] labels){view.Pairs=new Grid();for(int i=0;i<2;i++){view.Pairs.ColumnDefinitions.Add(new ColumnDefinition());var pair=new PairView {Key=keys[i],Label=labels[i],Panel=new StackPanel(),Name=Label(labels[i],10,"#B0C4DE"),Value=Label("—",21,view.Accent)};pair.Name.Margin=new Thickness(0,0,6,0);pair.Panel.Children.Add(pair.Name);pair.Value.Margin=new Thickness(0,6,0,0);pair.Panel.Children.Add(pair.Value);Grid.SetColumn(pair.Panel,i);view.Pairs.Children.Add(pair.Panel);view.PairItems.Add(pair);}view.Body.Children.Add(view.Pairs);}
        void AddUsage(CardView view,string key){view.Usage=key;view.UsageText=Label(key.ToUpperInvariant(),12);view.UsageText.Margin=new Thickness(0,8,0,2);view.UsageText.TextWrapping=TextWrapping.Wrap;view.Body.Children.Add(view.UsageText);view.UsageBar=new Grid {Height=3,Background=Brush("#203C5266")};view.UsageBar.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(0)});view.UsageBar.ColumnDefinitions.Add(new ColumnDefinition());view.UsageBar.Children.Add(new Border {Background=Brush(view.Accent),CornerRadius=new CornerRadius(2)});view.Body.Children.Add(view.UsageBar);}
        void UpdateCard(CardView view){
            view.Title.Text=language.T(view.Key);view.Sub.Text=(view.Key=="Network"?language.T("Traffic adapter")+" · ":"")+Device(view.Key,view.Subtitle);view.Border.ToolTip=view.Sub.Text;
            if(view.Hero!=null){view.HeroValue.Text=Value(view.Hero,"°C");view.HeroValue.Visibility=Available(view.Hero)?Visibility.Visible:Visibility.Collapsed;}
            foreach(var row in view.Rows){row.Name.Text=Device(row.Key,row.Label);row.Name.ToolTip=row.Name.Text;string value=Value(row.Key,row.Unit);if(row.Key=="gpuFan"&&readings.Latest.gpuFanCount>1)value=Value("gpuFan","RPM").Replace(" RPM","")+" / "+Value("gpuFan2","RPM");row.Value.Text=value;row.ShortValue.Text=value;row.Value.ToolTip=value=="0 RPM"?language.T("This channel reports 0 RPM; other fans or pumps may use separate channels."):null;}
            foreach(var pair in view.PairItems){pair.Name.Text=Device(pair.Key,pair.Label);pair.Name.ToolTip=pair.Name.Text;pair.Value.Text=Value(pair.Key,"°C");pair.Panel.Visibility=Available(pair.Key)?Visibility.Visible:Visibility.Collapsed;view.Pairs.ColumnDefinitions[Grid.GetColumn(pair.Panel)].Width=Available(pair.Key)?new GridLength(1,GridUnitType.Star):new GridLength(0);}
            if(view.Usage!=null){view.UsageText.Visibility=view.UsageBar.Visibility=readings.Latest.available==null||readings.HasUsage(view.Usage)?Visibility.Visible:Visibility.Collapsed;Usage usage;bool has=readings.Latest.state=="LIVE"&&readings.Latest.usage.TryGetValue(view.Usage,out usage);usage=has?readings.Latest.usage[view.Usage]:null;view.UsageText.Text=usage==null?language.T(view.Usage.ToUpperInvariant())+" —":string.Format("{0}  {1:F1} / {2:F1} GB · {3:F1}%",language.T(usage.label??view.Usage.ToUpperInvariant()),usage.used,usage.total,usage.percent);view.UsageBar.ColumnDefinitions[0].Width=new GridLength(usage==null?0:usage.percent,GridUnitType.Star);view.UsageBar.ColumnDefinitions[1].Width=new GridLength(usage==null?100:100-usage.percent,GridUnitType.Star);}
            bool supported=readings.Latest.available==null||(view.Hero!=null&&Available(view.Hero))||view.Rows.Any(r=>Available(r.Key))||view.PairItems.Any(p=>Available(p.Key))||(view.Usage!=null&&readings.HasUsage(view.Usage));view.Border.Visibility=CardEnabled(view.Key)&&supported?Visibility.Visible:Visibility.Collapsed;
        }
        void ApplyDensity(){
            if(measuring||cards==null)return;var scroll=Control<ScrollViewer>("CardScroll");if(scroll.ActualHeight<=0||scroll.ActualWidth<=0)return;measuring=true;
            try{bool detail=settings.Flag("details");double scale=Window.FontSize/12;var layout=(ResponsivePanel)cards;layout.MinimumColumnWidth=270*scale;var quotaLayout=(ResponsivePanel)Control<StackPanel>("QuotaCards");quotaLayout.MinimumColumnWidth=270*scale;layout.Measure(new Size(Math.Max(1,scroll.ActualWidth-38),double.PositiveInfinity));double cardWidth=layout.CellWidth;Control<Button>("Details").Background=Brush(detail?"#607898A8":"#00000000");
                for(int level=0;level<=3;level++){
                    foreach(var view in views.Values){view.Border.Padding=level==0?new Thickness(10,7,10,7):new Thickness(7,level==3?3:5,7,level==3?3:5);view.Border.Margin=new Thickness(0,0,0,level==0?6:3);view.Title.FontSize=13*scale;view.Sub.FontSize=10*scale;view.Sub.Visibility=level==3&&!detail?Visibility.Collapsed:Visibility.Visible;view.Sub.TextWrapping=detail?TextWrapping.Wrap:TextWrapping.NoWrap;
                        if(view.HeroValue!=null){view.HeroValue.FontSize=(detail?23:21)*scale;view.TitleRow.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));view.HeroValue.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));bool stacked=view.TitleRow.DesiredSize.Width+view.HeroValue.DesiredSize.Width+10>cardWidth-view.Border.Padding.Left-view.Border.Padding.Right;Grid.SetRow(view.HeroValue,stacked?1:0);Grid.SetColumn(view.HeroValue,stacked?0:1);Grid.SetColumnSpan(view.HeroValue,stacked?2:1);view.HeroValue.HorizontalAlignment=stacked?HorizontalAlignment.Left:HorizontalAlignment.Right;view.HeroValue.Margin=new Thickness(stacked?0:10,stacked?3:0,0,0);}
                        bool compact=!detail&&view.Key!="Airflow";double widest=0;foreach(var row in view.Rows){row.Name.FontSize=row.Value.FontSize=12*scale;row.ShortName.FontSize=10*scale;row.ShortValue.FontSize=11*scale;row.Full.Visibility=!compact&&Available(row.Key)?Visibility.Visible:Visibility.Collapsed;row.Compact.Visibility=compact&&Available(row.Key)?Visibility.Visible:Visibility.Collapsed;row.Compact.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));if(Available(row.Key))widest=Math.Max(widest,row.Compact.DesiredSize.Width);}bool single=widest*2>cardWidth-28;int i=0;foreach(var row in view.Rows){Grid.SetRow(row.Compact,single?i:i/2);Grid.SetColumn(row.Compact,single?0:i%2);Grid.SetColumnSpan(row.Compact,single?2:1);if(Available(row.Key))i++;}
                        foreach(var pair in view.PairItems){pair.Name.FontSize=10*scale;pair.Name.TextWrapping=detail?TextWrapping.Wrap:TextWrapping.NoWrap;pair.Value.FontSize=(detail?21:18)*scale;pair.Value.Margin=new Thickness(0,detail?6:0,0,0);}if(view.Usage!=null){view.UsageText.FontSize=12*scale;view.UsageText.Margin=new Thickness(0,detail?8:3,0,2);}
                    }
                    cards.UpdateLayout();cards.Measure(new Size(Math.Max(1,scroll.ActualWidth-38),double.PositiveInfinity));if(cards.DesiredSize.Height<=scroll.ActualHeight-2)break;
                }
                Control<TextBlock>("CardsEmpty").Visibility=!settingsVisible&&views.Values.All(v=>v.Border.Visibility==Visibility.Collapsed)?Visibility.Visible:Visibility.Collapsed;
            }finally{measuring=false;}
        }
    }
}
