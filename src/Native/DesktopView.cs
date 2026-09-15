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
        public string Key,Title,Value,Icon,ToolTip;
        public DesktopMetric(string key,string title,string value,string icon){Key=key;Title=title;Value=value;Icon=icon;}
    }
    public sealed class DesktopView : Window {
        public const double EdgePadding=16;
        sealed class Row {public Border Border,IconHost;public TextBlock Name,Value;public FrameworkElement Icon;public string IconColor,BaseIconColor;public bool IsFps;public double IconSize;}
        readonly Dictionary<string,Row> rows=new Dictionary<string,Row>();
        readonly ResponsivePanel stack=new ResponsivePanel{RowGap=0};readonly Border surface;
        readonly ScrollViewer scroll;
        readonly Func<string,double,string,FrameworkElement> icon;
        readonly bool isolated;
        readonly StackPanel editor=new StackPanel{Visibility=Visibility.Collapsed,Margin=new Thickness(0,0,0,12)};
        readonly TextBlock editHint=new TextBlock{TextWrapping=TextWrapping.Wrap,FontSize=12,Foreground=Brushes.White,Margin=new Thickness(0,0,0,8)};
        readonly Button done=new Button{MinHeight=32,Padding=new Thickness(12,4,12,4),Margin=new Thickness(0,0,8,0)};
        readonly Button returnToApp=new Button{MinHeight=32,Padding=new Thickness(12,4,12,4)};
        public event Action EditCompleted,ReturnRequested;
        DesktopLayer layer;bool locked=true;string lastColor;double lastSize;double fpsMinimumWidth;
        Brush foreground,line,protection;
        string automaticColor="#F5F7FA";long lastSample;
        LocalContrast localContrast;bool contrastBusy;readonly System.Windows.Threading.DispatcherTimer contrastTimer=new System.Windows.Threading.DispatcherTimer();
        readonly System.Windows.Threading.DispatcherTimer screenshotTimer=new System.Windows.Threading.DispatcherTimer();
        public bool ScreenshotActive {get;private set;}
        public bool LocalContrastAvailable {get;private set;}
        public event Action PositionSaved;
        public bool LayerAvailable {get{return isolated||layer!=null&&layer.Attached;}}
        public bool Locked {get{return locked;}}
        public DesktopView(Func<string,double,string,FrameworkElement> icon,bool isolated=false) {
            this.icon=icon;this.isolated=isolated;
            Title="Pulse Desktop";FontFamily=new FontFamily("Segoe UI, Microsoft YaHei UI, Microsoft JhengHei UI");WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
            AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;
            Focusable=false;SizeToContent=SizeToContent.Height;Width=466;MinWidth=280;MinHeight=140;WindowStartupLocation=WindowStartupLocation.Manual;
            UseLayoutRounding=true;SnapsToDevicePixels=true;TextOptions.SetTextFormattingMode(this,TextFormattingMode.Display);
            scroll=new ScrollViewer{Content=stack,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Focusable=false};
            var actions=new WrapPanel();actions.Children.Add(done);actions.Children.Add(returnToApp);editor.Children.Add(editHint);editor.Children.Add(actions);
            var content=new Grid();content.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});content.RowDefinitions.Add(new RowDefinition());content.Children.Add(editor);Grid.SetRow(scroll,1);content.Children.Add(scroll);
            surface=new Border{Padding=new Thickness(16),CornerRadius=new CornerRadius(16),BorderThickness=new Thickness(1),Child=content};Content=surface;
            done.Click+=delegate{if(EditCompleted!=null)EditCompleted();};returnToApp.Click+=delegate{if(ReturnRequested!=null)ReturnRequested();};
            SourceInitialized+=delegate{HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(ResizeHook);};
            SourceInitialized+=delegate{WindowSnap.Attach(this,true,EdgePadding);if(!isolated)layer=new DesktopLayer(this);};
            contrastTimer.Interval=TimeSpan.FromMilliseconds(100);contrastTimer.Tick+=delegate{RefreshLocalContrast();};
            screenshotTimer.Interval=TimeSpan.FromSeconds(15);screenshotTimer.Tick+=delegate{screenshotTimer.Stop();ScreenshotActive=false;if(localContrast!=null)SetLocalContrast(true);};
            Closed+=delegate{screenshotTimer.Stop();contrastTimer.Stop();if(localContrast!=null)localContrast.Dispose();if(layer!=null)layer.Dispose();};
            SizeChanged+=delegate{if(IsLoaded&&SizeToContent==SizeToContent.Manual&&PositionSaved!=null)PositionSaved();};
        }
        public void SetEditorLabels(string hint,string complete,string back){editHint.Text=hint;done.Content=complete;returnToApp.Content=back;}
        void RefreshFpsIcon(Row row){
            if(!row.IsFps)return;
            string tint=row.BaseIconColor;
            var brush=row.Name.Foreground as SolidColorBrush;
            if(LocalContrastAvailable&&brush!=null){
                bool dark=DesktopContrast.Luminance(brush.Color)<.4;
                tint=tint=="#9EDFD3"?(dark?"#285A50":"#9EDFD3"):brush.Color.ToString();
            }
            if(row.IconColor==tint&&row.IconSize==lastSize)return;
            row.Icon=icon("fps",lastSize,tint);row.IconHost.Child=row.Icon;row.IconColor=tint;row.IconSize=lastSize;
        }
        void ResetLocalStyle(){
            foreach(var row in rows.Values){row.Name.Foreground=row.Value.Foreground=foreground;row.Name.Effect=row.Value.Effect=null;RefreshFpsIcon(row);}
        }
        public static int ResizeEdge(Point point,Size size,double inset=8){
            if(point.X<0||point.Y<0||point.X>size.Width||point.Y>size.Height)return 0;
            bool left=point.X<=inset,right=point.X>=size.Width-inset,top=point.Y<=inset,bottom=point.Y>=size.Height-inset;
            return top?(left?13:right?14:12):bottom?(left?16:right?17:15):left?10:right?11:0;
        }
        IntPtr ResizeHook(IntPtr hwnd,int message,IntPtr w,IntPtr l,ref bool handled){
            if(!locked&&message==0x0232){KeepOnScreen();if(PositionSaved!=null)PositionSaved();}
            if(!locked&&message==0x0214)SizeToContent=SizeToContent.Manual;
            if(locked||message!=0x0084)return IntPtr.Zero;
            long value=l.ToInt64();var point=PointFromScreen(new Point((short)(value&65535),(short)((value>>16)&65535)));
            int hit=DesktopHitTest(point);if(hit==0)return IntPtr.Zero;
            handled=true;return new IntPtr(hit);
        }
        public int DesktopHitTest(Point point){
            if(locked)return 0;
            int edge=ResizeEdge(point,new Size(ActualWidth,ActualHeight));if(edge!=0)return edge;
            if(point.X<0||point.Y<0||point.X>ActualWidth||point.Y>ActualHeight)return 0;
            var target=InputHitTest(point) as DependencyObject;
            for(var node=target;node!=null;node=VisualTreeHelper.GetParent(node)){
                if(node is System.Windows.Controls.Primitives.ButtonBase||node is System.Windows.Controls.Primitives.ScrollBar||node is System.Windows.Controls.Primitives.Thumb)return 0;
                if(!(node is Visual))break;
            }
            return 2; // HTCAPTION: Windows moves the panel before ScrollViewer handles mouse input.
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
        void FitLockedContent(){
            if(!locked)return;
            var source=PresentationSource.FromVisual(this);if(source==null||source.CompositionTarget==null)return;
            var area=System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea;
            var available=source.CompositionTarget.TransformFromDevice.Transform(new Vector(area.Width,area.Height));
            double maxHeight=Math.Max(MinHeight,available.Y-2*EdgePadding),maxWidth=Math.Max(MinWidth,available.X-2*EdgePadding);
            double width=ActualWidth,needed;
            foreach(var row in rows.Values)if(row.Border.Visibility==Visibility.Visible&&row.Value.MinWidth==fpsMinimumWidth&&fpsMinimumWidth>0){
                row.Name.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
                width=Math.Max(width,Math.Min(maxWidth,fpsMinimumWidth+row.Name.DesiredSize.Width+lastSize+60));
            }
            for(int attempt=0;;attempt++){
                stack.Measure(new Size(Math.Max(1,width-34),double.PositiveInfinity));needed=stack.DesiredSize.Height+34;
                if(needed<=maxHeight||stack.RequestedColumns!=0||stack.Columns>=3||attempt>=2)break;
                double next=Math.Min(maxWidth,(stack.Columns+1)*stack.MinimumColumnWidth+stack.Columns*10+50);
                if(next<=width+.5)break;width=next;
            }
            bool resizeWidth=width>ActualWidth+.5;
            double height=Math.Min(maxHeight,Math.Max(ActualHeight,needed));
            if(resizeWidth||Math.Abs(height-ActualHeight)>.5){SizeToContent=SizeToContent.Manual;if(resizeWidth)Width=width;Height=height;UpdateLayout();}
        }
        static void SetFpsReading(TextBlock text,string value,double size){
            text.Inlines.Clear();
            var values=value.Split(new[]{" / "},StringSplitOptions.None);
            if(values.Length!=3){text.Text=value;return;}
            var labels=new[]{"NOW","AVG","MIN"};
            for(int i=0;i<3;i++){
                if(i>0)text.Inlines.Add(new System.Windows.Documents.Run("  "));
                text.Inlines.Add(new System.Windows.Documents.Run(values[i]));
                text.Inlines.Add(new System.Windows.Documents.Run(" "+labels[i]){FontSize=Math.Max(8,size*.55),BaselineAlignment=BaselineAlignment.Superscript});
            }
        }
        public void Render(IList<DesktopMetric> metrics,double size,double spacing,string color,bool isLocked,int columns=1,Func<string,string> iconColor=null) {
            stack.RequestedColumns=columns;stack.MinimumColumnWidth=Math.Max(280,24*size);stack.InvalidateMeasure();
            stack.Width=double.NaN;
            locked=isLocked;
            editor.Visibility=isLocked?Visibility.Collapsed:Visibility.Visible;
            ResizeMode=isLocked?ResizeMode.NoResize:ResizeMode.CanResizeWithGrip;
            SetValue(WindowSnap.PositionLockedProperty,isLocked);
            bool styleChanged=lastColor!=color||lastSize!=size;
            if(lastSize!=size){var digits=new TextBlock{FontFamily=FontFamily,FontSize=size};SetFpsReading(digits,"000 / 000 / 000",size);System.Windows.Documents.Typography.SetNumeralAlignment(digits,FontNumeralAlignment.Tabular);digits.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));fpsMinimumWidth=Math.Ceiling(digits.DesiredSize.Width);}
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
                    row.Value.TextWrapping=metric.Icon=="fps"?TextWrapping.NoWrap:TextWrapping.Wrap;
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
                if(metric.Icon=="fps"){SetFpsReading(row.Value,metric.Value,size);row.Value.MinWidth=fpsMinimumWidth;row.Value.TextAlignment=TextAlignment.Right;}
                row.Value.ToolTip=metric.ToolTip??metric.Value;
                row.Name.ToolTip=metric.Title;if(localContrast==null||!LocalContrastAvailable){row.Name.Foreground=row.Value.Foreground=foreground;}row.Border.BorderBrush=line;
                row.Border.Padding=new Thickness(0,spacing/2,0,spacing/2);row.Border.Visibility=Visibility.Visible;
                string tint=iconColor==null?color:iconColor(metric.Icon);
                row.IsFps=metric.Icon=="fps";row.BaseIconColor=tint;
                if(row.IsFps&&LocalContrastAvailable){RefreshFpsIcon(row);continue;}
                if(styleChanged||row.IconColor!=tint){row.Icon=icon(metric.Icon,size,tint);row.IconHost.Child=row.Icon;row.IconColor=tint;}
            }
            // Desktop order is independent of the monitor cards.
            var ordered=metrics.Select(m=>rows[m.Key].Border).ToArray();
            if(!stack.Children.Cast<UIElement>().SequenceEqual(ordered)){stack.Children.Clear();foreach(var child in ordered)stack.Children.Add(child);}
            if(layer!=null)layer.SetLocked(locked);
            UpdateLayout();FitLockedContent();KeepOnScreen();
        }
        public void SetAlwaysOnTop(bool value){if(layer!=null)layer.SetAlwaysOnTop(value);}
        public void SetLocalContrast(bool enabled){
            if(ScreenshotActive&&enabled)return;
            if(!enabled||SystemParameters.HighContrast){contrastTimer.Stop();if(localContrast!=null){localContrast.Dispose();localContrast=null;}LocalContrastAvailable=false;ResetLocalStyle();return;}
            if(localContrast==null)localContrast=new LocalContrast(this);
            LocalContrastAvailable=localContrast.Enable();
            if(LocalContrastAvailable&&!contrastTimer.IsEnabled)contrastTimer.Start();
            if(LocalContrastAvailable)RefreshLocalContrast();
        }
        public void BeginScreenshot(){
            ScreenshotActive=true;contrastTimer.Stop();if(localContrast!=null)localContrast.Dispose();
            screenshotTimer.Stop();screenshotTimer.Start();
        }
        void RefreshLocalContrast(){
            if(localContrast==null||!IsVisible||contrastBusy||ScreenshotActive)return;
            contrastBusy=true;var capture=localContrast;
            var background=surface.Background as SolidColorBrush;
            var color=background==null?Colors.Transparent:background.Color;var bounds=capture.Bounds();
            int radius=Math.Max(2,(int)Math.Round(lastSize*.4*bounds.Width/ActualWidth));
            System.Threading.Tasks.Task.Run(()=>capture.Capture(bounds,color,radius)).ContinueWith(task=>{
                var error=task.Exception; // Observe capture failure even if the window has closed.
                if(Dispatcher.HasShutdownStarted)return;
                Dispatcher.BeginInvoke(new Action(delegate{try{
                if(localContrast!=capture||!IsVisible||ScreenshotActive||capture.Bounds()!=bounds)return;
                var image=error==null?task.Result:null;
                if(image==null){LocalContrastAvailable=false;return;}
                LocalContrastAvailable=true;
                foreach(var row in rows.Values)if(row.Border.Visibility==Visibility.Visible)foreach(var text in new[]{row.Name,row.Value}){
                    if(text.ActualWidth<=0||text.ActualHeight<=0)continue;
                    var point=text.TranslatePoint(new Point(),this);
                    var old=text.Foreground as SolidColorBrush;byte prior=old!=null&&DesktopContrast.Luminance(old.Color)<.4?(byte)20:(byte)245;
                    var region=new Int32Rect((int)(point.X*image.PixelWidth/ActualWidth),(int)(point.Y*image.PixelHeight/ActualHeight),Math.Max(1,(int)Math.Ceiling(text.ActualWidth*image.PixelWidth/ActualWidth)),Math.Max(1,(int)Math.Ceiling(text.ActualHeight*image.PixelHeight/ActualHeight)));
                    double minority;byte shade=LocalContrast.RegionColor(image,region,prior,out minority);
                    var brush=new SolidColorBrush(Color.FromRgb(shade,shade,shade));
                    brush.Freeze();text.Foreground=brush;
                    bool edge=minority>(text.Effect==null?.12:.06);
                    if(edge){
                        var effect=text.Effect as System.Windows.Media.Effects.DropShadowEffect;
                        var edgeColor=shade==20?Colors.White:Colors.Black;
                        if(effect==null||effect.Color!=edgeColor){effect=new System.Windows.Media.Effects.DropShadowEffect{Color=edgeColor,ShadowDepth=0,BlurRadius=1.5,Opacity=.85};effect.Freeze();text.Effect=effect;}
                    }else text.Effect=null;
                    if(text==row.Name)RefreshFpsIcon(row);
                }
            }catch(ArgumentException){LocalContrastAvailable=false;}
            finally{contrastBusy=false;if(!LocalContrastAvailable)ResetLocalStyle();}
                }));
            });
        }
        public void RefreshLayer(){if(layer!=null){layer.SetLocked(locked);layer.Refresh();}}
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
        public void SetTextOpacity(double percent,bool automatic=false,bool overlay=false,double backgroundOpacity=55,double desktopBackgroundOpacity=-1){
            stack.Opacity=Math.Max(0,Math.Min(1,percent/100));
            surface.VerticalAlignment=overlay&&locked?VerticalAlignment.Top:VerticalAlignment.Stretch;
            var tint=(Color)ColorConverter.ConvertFromString(lastColor??"#F5F7FA");
            editHint.Foreground=foreground;
            var previous=stack.Effect as System.Windows.Media.Effects.DropShadowEffect;
            Color outline=DesktopContrast.Outline(tint);
            Color backing=outline==Colors.Black?Color.FromArgb(220,20,29,38):Color.FromArgb(230,245,247,250);
            if(!overlay&&desktopBackgroundOpacity>=0)backing.A=(byte)Math.Round(255*Math.Max(0,Math.Min(100,desktopBackgroundOpacity))/100);
            if(overlay)backing.A=(byte)Math.Round(255*Math.Max(0,Math.Min(100,backgroundOpacity))/100);
            if(protection==null||((SolidColorBrush)protection).Color!=backing){protection=new SolidColorBrush(backing);protection.Freeze();}
            surface.Background=automatic||overlay||desktopBackgroundOpacity>0?protection:locked?Brushes.Transparent:new SolidColorBrush(Color.FromArgb(100,18,24,30));
            // A fully transparent locked panel must not leave a rounded frame behind.
            surface.BorderBrush=(automatic||overlay)&&(!locked||backing.A>0)?line:Brushes.Transparent;
            foreach(var row in rows.Values){
                row.Name.Background=row.Value.Background=row.IconHost.Background=null;
                row.Name.Padding=row.Value.Padding=new Thickness(0);
            }
            // Keep WPF glyph rendering sharp instead of blurring the whole readout.
            if(automatic||overlay){stack.Effect=null;return;}
            if(previous==null||previous.Color!=outline)stack.Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=outline,ShadowDepth=0,BlurRadius=3,Opacity=.85};
        }
    }
}
