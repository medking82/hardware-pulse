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
        public const double EdgePadding=16;
        sealed class Row {public Border Border,IconHost;public TextBlock Name,Value;public FrameworkElement Icon;public string IconColor;}
        readonly Dictionary<string,Row> rows=new Dictionary<string,Row>();
        readonly ResponsivePanel stack=new ResponsivePanel{RowGap=0};readonly Border surface;
        readonly Func<string,double,string,FrameworkElement> icon;
        readonly bool isolated;
        DesktopLayer layer;bool locked=true;string lastColor;double lastSize;
        Brush foreground,line,protection;
        string automaticColor="#F5F7FA";long lastSample;
        public event Action PositionSaved;
        public bool LayerAvailable {get{return isolated||layer!=null&&layer.Attached;}}
        public bool Locked {get{return locked;}}
        public DesktopView(Func<string,double,string,FrameworkElement> icon,bool isolated=false) {
            this.icon=icon;this.isolated=isolated;
            Title="Pulse Desktop";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
            AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;
            Focusable=false;SizeToContent=SizeToContent.Height;Width=466;MinWidth=280;MinHeight=140;WindowStartupLocation=WindowStartupLocation.Manual;
            UseLayoutRounding=true;SnapsToDevicePixels=true;TextOptions.SetTextFormattingMode(this,TextFormattingMode.Display);
            var scroll=new ScrollViewer{Content=stack,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Focusable=false};
            surface=new Border{Padding=new Thickness(16),CornerRadius=new CornerRadius(16),BorderThickness=new Thickness(1),Child=scroll};Content=surface;
            SourceInitialized+=delegate{WindowSnap.Attach(this,false,EdgePadding);if(!isolated)layer=new DesktopLayer(this);};
            Closed+=delegate{if(layer!=null)layer.Dispose();};
            SizeChanged+=delegate{if(IsLoaded&&SizeToContent==SizeToContent.Manual&&PositionSaved!=null)PositionSaved();};
            MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){if(locked||e.Handled||e.ButtonState!=MouseButtonState.Pressed)return;DragMove();KeepOnScreen();if(PositionSaved!=null)PositionSaved();};
        }
        public static Point Clamp(Point position,Size size,IEnumerable<Rect> screens,double padding=0) {
            var areas=screens.ToArray();if(areas.Length==0)return position;
            var footprint=new Rect(position,size);
            var target=areas.OrderByDescending(a=>{var overlap=Rect.Intersect(a,footprint);return overlap.IsEmpty?0:overlap.Width*overlap.Height;})
                .ThenBy(a=>Math.Pow(position.X-(a.Left+a.Width/2),2)+Math.Pow(position.Y-(a.Top+a.Height/2),2)).First();
            double x=Math.Min(Math.Max(0,padding),Math.Max(0,(target.Width-size.Width)/2));
            double y=Math.Min(Math.Max(0,padding),Math.Max(0,(target.Height-size.Height)/2));
            return new Point(Math.Max(target.Left+x,Math.Min(position.X,target.Right-size.Width-x)),Math.Max(target.Top+y,Math.Min(position.Y,target.Bottom-size.Height-y)));
        }
        public void KeepOnScreen() {
            var source=PresentationSource.FromVisual(this);if(source==null||source.CompositionTarget==null)return;
            var matrix=source.CompositionTarget.TransformFromDevice;
            var areas=System.Windows.Forms.Screen.AllScreens.Select(s=>{
                var a=s.WorkingArea;return new Rect(matrix.Transform(new Point(a.Left,a.Top)),matrix.Transform(new Point(a.Right,a.Bottom)));
            }).ToArray();
            var location=Clamp(new Point(Left,Top),new Size(ActualWidth,ActualHeight),areas,EdgePadding);
            Left=location.X;Top=location.Y;
        }
        public void Render(IList<DesktopMetric> metrics,double size,double spacing,string color,bool isLocked,int columns=1,Func<string,string> iconColor=null) {
            stack.RequestedColumns=columns;stack.MinimumColumnWidth=Math.Max(280,24*size);stack.InvalidateMeasure();
            stack.Width=double.NaN;
            locked=isLocked;
            ResizeMode=isLocked?ResizeMode.NoResize:ResizeMode.CanResizeWithGrip;
            SetValue(WindowSnap.PositionLockedProperty,isLocked);
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
                    row.Value.TextWrapping=TextWrapping.Wrap;
                    row.Icon=icon(metric.Icon,size,color);row.IconHost=new Border{Child=row.Icon,Padding=new Thickness(2),Margin=new Thickness(0,0,8,0),VerticalAlignment=VerticalAlignment.Center};grid.Children.Add(row.IconHost);
                    row.Name.VerticalAlignment=row.Value.VerticalAlignment=VerticalAlignment.Center;
                    row.Name.HorizontalAlignment=HorizontalAlignment.Left;row.Value.HorizontalAlignment=HorizontalAlignment.Right;
                    row.Name.Margin=new Thickness(0,0,12,0);row.Name.TextWrapping=TextWrapping.Wrap;Grid.SetColumn(row.Name,1);Grid.SetColumn(row.Value,2);grid.Children.Add(row.Name);grid.Children.Add(row.Value);
                    System.Windows.Documents.Typography.SetNumeralAlignment(row.Value,FontNumeralAlignment.Tabular);
                                        // Let the reading take its natural width, but reserve usable space for
                    // the name when a long reading meets a narrow panel. No fixed label cap.
                    grid.SizeChanged+=delegate{
                        double available=Math.Max(1,grid.ActualWidth-row.IconHost.ActualWidth-row.IconHost.Margin.Right-row.Name.Margin.Right);
                        double limit=Math.Max(1,available*.65);
                        if(double.IsInfinity(row.Value.MaxWidth)||Math.Abs(row.Value.MaxWidth-limit)>.5)row.Value.MaxWidth=limit;
                    };
                    row.Border.Child=grid;rows.Add(metric.Key,row);
                }
                row.Name.Text=metric.Title;row.Value.Text=metric.Value;row.Name.FontSize=size;row.Value.FontSize=size;
                row.Name.ToolTip=metric.Title;row.Name.Foreground=row.Value.Foreground=foreground;row.Border.BorderBrush=line;
                row.Border.Padding=new Thickness(0,spacing/2,0,spacing/2);row.Border.Visibility=Visibility.Visible;
                string tint=iconColor==null?color:iconColor(metric.Icon);
                if(styleChanged||row.IconColor!=tint){row.Icon=icon(metric.Icon,size,tint);row.IconHost.Child=row.Icon;row.IconColor=tint;}
            }
            // Desktop order is independent of the monitor cards.
            var ordered=metrics.Select(m=>rows[m.Key].Border).ToArray();
            if(!stack.Children.Cast<UIElement>().SequenceEqual(ordered)){stack.Children.Clear();foreach(var child in ordered)stack.Children.Add(child);}
            if(layer!=null)layer.SetLocked(locked);
            UpdateLayout();KeepOnScreen();
        }
        public void RefreshLayer(){if(layer!=null)layer.Refresh();}
        public string ResolveColor(bool automatic,string custom){
            if(!automatic)return custom;
            // Sampling can pause when another app owns focus. Do not keep an old dark
            // choice indefinitely after the wallpaper changes; use protected light text.
            if(layer==null||!layer.CanSampleBackground)return "#F5F7FA";
            long now=System.Diagnostics.Stopwatch.GetTimestamp();
            if(layer!=null&&layer.CanSampleBackground&&now-lastSample>=System.Diagnostics.Stopwatch.Frequency*2){
                lastSample=now;var luminance=DesktopContrast.Sample(this);
                automaticColor=luminance.HasValue?DesktopContrast.Choose(luminance.Value,automaticColor):"#F5F7FA";
            }
            return automaticColor;
        }
        public void SetTextOpacity(double percent,bool automatic=false){
            stack.Opacity=Math.Max(automatic?.9:.3,Math.Min(1,percent/100));
            var tint=(Color)ColorConverter.ConvertFromString(lastColor??"#F5F7FA");
            var previous=stack.Effect as System.Windows.Media.Effects.DropShadowEffect;
            Color outline=DesktopContrast.Outline(tint);
            Color backing=outline==Colors.Black?Color.FromArgb(220,20,29,38):Color.FromArgb(230,245,247,250);
            if(protection==null||((SolidColorBrush)protection).Color!=backing){protection=new SolidColorBrush(backing);protection.Freeze();}
            surface.Background=automatic?protection:locked?Brushes.Transparent:new SolidColorBrush(Color.FromArgb(100,18,24,30));
            surface.BorderBrush=automatic?line:Brushes.Transparent;
            foreach(var row in rows.Values){
                row.Name.Background=row.Value.Background=row.IconHost.Background=null;
                row.Name.Padding=row.Value.Padding=new Thickness(0);
            }
            // Keep WPF glyph rendering sharp instead of blurring the whole readout.
            if(automatic){stack.Effect=null;return;}
            if(previous==null||previous.Color!=outline)stack.Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=outline,ShadowDepth=0,BlurRadius=3,Opacity=.85};
        }
    }
}
