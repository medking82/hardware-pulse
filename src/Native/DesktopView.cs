using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace HardwarePulse {
    public sealed class DesktopMetric {
        public string Key,Title,Value,Icon;
        public DesktopMetric(string key,string title,string value,string icon){Key=key;Title=title;Value=value;Icon=icon;}
    }
    public sealed class DesktopView : Window {
        sealed class Row {public Border Border;public TextBlock Name,Value;public FrameworkElement Icon;}
        readonly Dictionary<string,Row> rows=new Dictionary<string,Row>();
        readonly StackPanel stack=new StackPanel();readonly Border surface;
        readonly Func<string,double,string,FrameworkElement> icon;
        readonly bool isolated;
        DesktopLayer layer;bool locked=true;string lastColor;double lastSize;
        Brush foreground,line;
        string automaticColor="#F5F7FA";long lastSample;
        public event Action PositionSaved;
        public bool LayerAvailable {get{return isolated||layer!=null&&layer.Attached;}}
        public bool Locked {get{return locked;}}
        public DesktopView(Func<string,double,string,FrameworkElement> icon,bool isolated=false) {
            this.icon=icon;this.isolated=isolated;
            Title="Pulse Desktop";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
            AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;
            Focusable=false;SizeToContent=SizeToContent.WidthAndHeight;WindowStartupLocation=WindowStartupLocation.Manual;
            UseLayoutRounding=true;SnapsToDevicePixels=true;TextOptions.SetTextFormattingMode(this,TextFormattingMode.Display);
            surface=new Border{Padding=new Thickness(12),Child=stack};Content=surface;
            SourceInitialized+=delegate{if(!isolated)layer=new DesktopLayer(this);};
            Closed+=delegate{if(layer!=null)layer.Dispose();};
            MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){if(locked||e.ButtonState!=MouseButtonState.Pressed)return;DragMove();KeepOnScreen();if(PositionSaved!=null)PositionSaved();};
        }
        public static Point Clamp(Point position,Size size,IEnumerable<Rect> screens) {
            var areas=screens.ToArray();if(areas.Length==0)return position;
            var footprint=new Rect(position,size);
            var target=areas.OrderByDescending(a=>{var overlap=Rect.Intersect(a,footprint);return overlap.IsEmpty?0:overlap.Width*overlap.Height;})
                .ThenBy(a=>Math.Pow(position.X-(a.Left+a.Width/2),2)+Math.Pow(position.Y-(a.Top+a.Height/2),2)).First();
            return new Point(Math.Max(target.Left,Math.Min(position.X,target.Right-size.Width)),Math.Max(target.Top,Math.Min(position.Y,target.Bottom-size.Height)));
        }
        public void KeepOnScreen() {
            var source=PresentationSource.FromVisual(this);if(source==null||source.CompositionTarget==null)return;
            var matrix=source.CompositionTarget.TransformFromDevice;
            var areas=System.Windows.Forms.Screen.AllScreens.Select(s=>{
                var a=s.WorkingArea;return new Rect(matrix.Transform(new Point(a.Left,a.Top)),matrix.Transform(new Point(a.Right,a.Bottom)));
            }).ToArray();
            var location=Clamp(new Point(Left,Top),new Size(ActualWidth,ActualHeight),areas);
            Left=location.X;Top=location.Y;
        }
        public void Render(IList<DesktopMetric> metrics,double size,double spacing,string color,bool isLocked) {
            locked=isLocked;
            bool styleChanged=lastColor!=color||lastSize!=size;
            if(styleChanged){var tint=(Color)ColorConverter.ConvertFromString(color);foreground=new SolidColorBrush(tint);foreground.Freeze();line=new SolidColorBrush(Color.FromArgb(50,tint.R,tint.G,tint.B));line.Freeze();lastColor=color;lastSize=size;}
            if(isLocked)surface.Background=Brushes.Transparent;
            else if(surface.Background==Brushes.Transparent||surface.Background==null)surface.Background=new SolidColorBrush(Color.FromArgb(100,18,24,30));
            Cursor=isLocked?Cursors.Arrow:Cursors.SizeAll;
            var active=new HashSet<string>(metrics.Select(metric=>metric.Key));
            foreach(var entry in rows)if(!active.Contains(entry.Key))entry.Value.Border.Visibility=Visibility.Collapsed;
            foreach(var metric in metrics){
                Row row;if(!rows.TryGetValue(metric.Key,out row)){
                    row=new Row{Border=new Border{BorderThickness=new Thickness(0,0,0,1)},Name=new TextBlock(),Value=new TextBlock()};
                    var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
                    row.Icon=icon(metric.Icon,size,color);row.Icon.Margin=new Thickness(0,0,10,0);grid.Children.Add(row.Icon);
                    row.Name.VerticalAlignment=row.Value.VerticalAlignment=VerticalAlignment.Center;
                    row.Name.Margin=new Thickness(0,0,20,0);row.Name.MaxWidth=220;row.Name.TextTrimming=TextTrimming.CharacterEllipsis;Grid.SetColumn(row.Name,1);Grid.SetColumn(row.Value,2);grid.Children.Add(row.Name);grid.Children.Add(row.Value);
                    System.Windows.Documents.Typography.SetNumeralAlignment(row.Value,FontNumeralAlignment.Tabular);
                    row.Border.Child=grid;rows.Add(metric.Key,row);
                }
                row.Name.Text=metric.Title;row.Value.Text=metric.Value;row.Name.FontSize=size;row.Value.FontSize=size;
                row.Name.ToolTip=metric.Title;row.Name.Foreground=row.Value.Foreground=foreground;row.Border.BorderBrush=line;
                row.Border.Padding=new Thickness(0,spacing/2,0,spacing/2);row.Border.Visibility=Visibility.Visible;
                if(styleChanged){var gridRow=(Grid)row.Border.Child;gridRow.Children.Remove(row.Icon);row.Icon=icon(metric.Icon,size,color);row.Icon.Margin=new Thickness(0,0,10,0);gridRow.Children.Add(row.Icon);}
            }
            // Order follows the existing Cards preference, including after a reorder.
            var ordered=metrics.Select(m=>rows[m.Key].Border).ToArray();
            if(!stack.Children.Cast<UIElement>().SequenceEqual(ordered)){stack.Children.Clear();foreach(var child in ordered)stack.Children.Add(child);}
            if(layer!=null)layer.SetLocked(locked);
            UpdateLayout();KeepOnScreen();
        }
        public void RefreshLayer(){if(layer!=null)layer.Refresh();}
        public string ResolveColor(bool automatic,string custom){
            if(!automatic)return custom;
            long now=System.Diagnostics.Stopwatch.GetTimestamp();
            if(layer!=null&&layer.CanSampleBackground&&now-lastSample>=System.Diagnostics.Stopwatch.Frequency*2){
                lastSample=now;var luminance=DesktopContrast.Sample(this);
                if(luminance.HasValue)automaticColor=DesktopContrast.Choose(luminance.Value,automaticColor);
            }
            return automaticColor;
        }
        public void SetTextOpacity(double percent){
            stack.Opacity=Math.Max(.3,Math.Min(1,percent/100));
            var tint=(Color)ColorConverter.ConvertFromString(lastColor??"#F5F7FA");
            var previous=stack.Effect as System.Windows.Media.Effects.DropShadowEffect;
            Color outline=DesktopContrast.Outline(tint);
            if(previous==null||previous.Color!=outline)stack.Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=outline,ShadowDepth=0,BlurRadius=3,Opacity=.85};
        }
    }
}
