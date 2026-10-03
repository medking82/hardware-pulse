using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace HardwarePulse {
    public sealed partial class Shell {
        Window resourcesWindow;
        ComboBox resourcesSort;
        Action resourcesAppearance;
        Func<AppResourceSnapshot> readAppResources=WindowsAppResources.Read;
        Func<AppResourceProcess,AppCloseResult> closeResourceApp=WindowsAppResources.RequestClose;
        Func<Window,string,string,bool> confirmResourceApps=(owner,prompt,title)=>MessageBox.Show(owner,prompt,title,MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes;
        Task<AppResourceSnapshot> resourceReadTask;
        sealed class ResourceRow {
            public AppResourceProcess Process {get;set;}
            public bool CanClose {get{return Process.CanClose;}}
            public string Name {get{return Process.Name+" · "+Process.Pid.ToString(CultureInfo.InvariantCulture);}}
            public string Ram {get{return ResourceBytes(Process.RamBytes);}}
            public string Gpu {get{return Process.GpuBytes.HasValue?ResourceBytes(Process.GpuBytes.Value):"—";}}
            public string Action {get;set;}
            public string Tooltip {get{return Name+Environment.NewLine+Action;}}
        }
        static string ResourceBytes(long bytes){return bytes>=1073741824?((double)bytes/1073741824).ToString("F1",CultureInfo.InvariantCulture)+" GB":((double)bytes/1048576).ToString("F0",CultureInfo.InvariantCulture)+" MB";}
        static bool TryResourceHeadroom(RamUsage value,out double free){
            free=0;if(value==null||!value.totalGb.HasValue||!value.usedGb.HasValue)return false;
            double total=value.totalGb.Value,used=value.usedGb.Value;
            if(double.IsNaN(total)||double.IsInfinity(total)||double.IsNaN(used)||double.IsInfinity(used)||total<=0||used<0||used>total)return false;
            free=total-used;return true;
        }
        void AddResourceAction(CardView card,bool gpu){
            var button=new Button {Content=gpu?"Manage GPU Memory":"Manage RAM",Margin=new Thickness(0,8,0,0),HorizontalAlignment=HorizontalAlignment.Left};
            Catalog(button);button.Click+=delegate{ShowAppResources(gpu);};card.Body.Children.Add(button);
        }
        // One window and one on-demand read. Suggestions never close or select apps automatically.
        void ShowAppResources(bool gpu){
            if(resourcesWindow!=null){resourcesSort.SelectedIndex=gpu?1:0;resourcesWindow.Activate();return;}
            var window=new Window {Title=language.T("Memory Cleanup"),Owner=Window,Icon=Window.Icon,Width=760,Height=700,MinWidth=480,MinHeight=620,
                WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Brush(light?"#F3F6F8":"#202A33"),Foreground=Window.Foreground,
                FontFamily=Window.FontFamily,FontSize=13,UseLayoutRounding=true,ShowInTaskbar=false};
            resourcesWindow=window;window.Resources.MergedDictionaries.Add(Window.Resources);NameScope.SetNameScope(window,new NameScope());
            var root=new DockPanel {Margin=new Thickness(20)};window.Content=root;
            var heading=new StackPanel {Margin=new Thickness(0,0,0,12)};DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
            heading.Children.Add(new TextBlock {Text=language.T("Memory Cleanup"),FontSize=22,FontWeight=FontWeights.SemiBold});
            heading.Children.Add(new TextBlock {Text=language.T("Select an app you no longer need. Closing it can release RAM and GPU resources."),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)});
            var advice=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)};heading.Children.Add(advice);
            var candidates=new TextBlock {TextTrimming=TextTrimming.CharacterEllipsis,TextWrapping=TextWrapping.NoWrap,Margin=new Thickness(0,6,0,0)};heading.Children.Add(candidates);
            var status=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0)};heading.Children.Add(status);
            var sorting=new WrapPanel {Margin=new Thickness(0,10,0,0)};heading.Children.Add(sorting);
            sorting.Children.Add(new TextBlock {Text=language.T("Sort by"),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)});
            var sort=new ComboBox {MinWidth=130};sort.Items.Add(language.T("RAM"));sort.Items.Add(language.T("GPU (estimate)"));sort.Items.Add(language.T("App"));sort.SelectedIndex=gpu?1:0;sorting.Children.Add(sort);resourcesSort=sort;
            var direction=new Button {Content=language.T("Highest first"),Margin=new Thickness(8,0,0,0)};sorting.Children.Add(direction);
            var suggestedClose=new Button {Content=language.T("Close suggestions…"),Margin=new Thickness(8,0,0,0),IsEnabled=false};sorting.Children.Add(suggestedClose);
            var footer=new StackPanel {Margin=new Thickness(0,10,0,0)};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            var selectedStatus=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,6)};footer.Children.Add(selectedStatus);
            var appearance=new StackPanel {Margin=new Thickness(0,0,0,10)};footer.Children.Add(appearance);
            var follow=new CheckBox {Content=language.T("Follow App appearance"),IsChecked=settings.Flag("resourceFollowApp",true)};appearance.Children.Add(follow);
            var opacityLine=new DockPanel {Margin=new Thickness(0,6,0,0)};appearance.Children.Add(opacityLine);
            var opacityValue=new TextBlock {Margin=new Thickness(8,0,0,0)};DockPanel.SetDock(opacityValue,Dock.Right);opacityLine.Children.Add(opacityValue);
            opacityLine.Children.Add(new TextBlock {Text=language.T("Background Opacity")});
            var opacity=new Slider {Minimum=0,Maximum=100,Value=settings.Number("resourceOpacity",Control<Slider>("OpacitySlider").Value,0,100),Margin=new Thickness(0,6,0,0)};
            AutomationProperties.SetName(opacity,language.T("Background Opacity"));appearance.Children.Add(opacity);
            footer.Children.Add(new TextBlock {Text=language.T("Each row is one process. Closing an app may also close its child processes."),TextWrapping=TextWrapping.Wrap,FontSize=11,Opacity=0.85});
            footer.Children.Add(new TextBlock {Text=language.T("RAM is resident memory. GPU values are estimates and may include shared resources; — means unavailable."),TextWrapping=TextWrapping.Wrap,FontSize=11,Opacity=0.85});
            var actions=new WrapPanel {Margin=new Thickness(0,10,0,0)};footer.Children.Add(actions);
            var refresh=new Button {Content=language.T("Refresh"),Margin=new Thickness(0,0,8,4)};
            var close=new Button {Content=language.T("Close Selected Apps"),IsEnabled=false,Margin=new Thickness(0,0,0,4)};actions.Children.Add(refresh);actions.Children.Add(close);
            var list=new ListView {SelectionMode=SelectionMode.Extended,BorderBrush=Brush("#60718898"),BorderThickness=new Thickness(1)};
            ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);root.Children.Add(list);
            var columns=new GridView();list.View=columns;
            // The App's checkbox template is a wide toggle; rows need a compact selection checkbox.
            var checkStyle=new Style(typeof(CheckBox));checkStyle.Setters.Add(new Setter(FrameworkElement.WidthProperty,16.0));checkStyle.Setters.Add(new Setter(FrameworkElement.HeightProperty,16.0));
            checkStyle.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center));checkStyle.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center));
            var check=new FrameworkElementFactory(typeof(CheckBox));check.SetBinding(CheckBox.IsCheckedProperty,new Binding("IsSelected") {RelativeSource=new RelativeSource(RelativeSourceMode.FindAncestor,typeof(ListViewItem),1),Mode=BindingMode.TwoWay});
            check.SetValue(FrameworkElement.StyleProperty,checkStyle);
            check.SetBinding(UIElement.IsEnabledProperty,new Binding("CanClose"));check.SetBinding(AutomationProperties.NameProperty,new Binding("Name"));
            columns.Columns.Add(new GridViewColumn {Header="",CellTemplate=new DataTemplate {VisualTree=check},Width=28});
            foreach(var entry in new[]{new[]{"App","Name"},new[]{"RAM","Ram"},new[]{"GPU (estimate)","Gpu"}}){
                var text=new FrameworkElementFactory(typeof(TextBlock));text.SetBinding(TextBlock.TextProperty,new Binding(entry[1]));
                text.SetValue(TextBlock.TextWrappingProperty,TextWrapping.NoWrap);text.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);
                columns.Columns.Add(new GridViewColumn {Header=new GridViewColumnHeader {Content=language.T(entry[0]),Tag=entry[1]},CellTemplate=new DataTemplate {VisualTree=text},Width=entry[1]=="Name"?260:entry[1]=="Gpu"?130:80});
            }
            var rowStyle=new Style(typeof(ListViewItem));rowStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,new Binding("Tooltip")));list.ItemContainerStyle=rowStyle;
            foreach(var pair in new[]{Tuple.Create("ResourceList",(object)list),Tuple.Create("ResourceSort",(object)sort),Tuple.Create("ResourceDirection",(object)direction),Tuple.Create("ResourceSuggestions",(object)suggestedClose),Tuple.Create("ResourceCandidates",(object)candidates),Tuple.Create("ResourceRefresh",(object)refresh),Tuple.Create("ResourceClose",(object)close),Tuple.Create("ResourceAdvice",(object)advice),Tuple.Create("ResourceStatus",(object)status),Tuple.Create("ResourceFollow",(object)follow),Tuple.Create("ResourceOpacity",(object)opacity),Tuple.Create("ResourceOpacityValue",(object)opacityValue)})window.RegisterName(pair.Item1,pair.Item2);
            resourcesAppearance=delegate{
                IntPtr hwnd=new WindowInteropHelper(window).Handle;if(hwnd==IntPtr.Zero)return;
                double chosen=follow.IsChecked==true?Control<Slider>("OpacitySlider").Value:opacity.Value;
                var policy=new MaterialPolicy(chosen,false,false,Checked("Solid"),SystemParameters.HighContrast);
                bool supported=PulseBackdrop.ApplyStable(hwnd,policy.Solid,policy.Clear);
                var margins=new PulseBackdrop.Margins {Left=-1,Right=-1,Top=-1,Bottom=-1};PulseBackdrop.DwmExtendFrameIntoClientArea(hwnd,ref margins);
                var source=HwndSource.FromHwnd(hwnd);if(source!=null)source.CompositionTarget.BackgroundColor=Colors.Transparent;
                var color=(Color)ColorConverter.ConvertFromString(BackgroundHex());bool pale=(.2126*color.R+.7152*color.G+.0722*color.B)/255>.55;
                double alpha=policy.EffectiveOpacity(supported);window.Background=SystemParameters.HighContrast?SystemColors.WindowBrush:new SolidColorBrush(Color.FromArgb((byte)Math.Round(255*alpha),color.R,color.G,color.B));
                window.Foreground=SystemParameters.HighContrast?SystemColors.WindowTextBrush:Brush(pale?"#17202B":"#E5EDF3");
                int dark=pale?0:1,round=2;PulseBackdrop.DwmSetWindowAttribute(hwnd,20,ref dark,4);PulseBackdrop.DwmSetWindowAttribute(hwnd,33,ref round,4);
                list.Background=SystemParameters.HighContrast?SystemColors.WindowBrush:new SolidColorBrush(Color.FromArgb((byte)Math.Round(32*alpha),pale?(byte)255:(byte)0,pale?(byte)255:(byte)0,pale?(byte)255:(byte)0));list.Foreground=window.Foreground;
                foreach(var button in new[]{direction,suggestedClose,refresh,close})button.Foreground=window.Foreground;
                follow.Foreground=window.Foreground;opacity.IsEnabled=follow.IsChecked!=true&&policy.CanAdjustOpacity(supported);
                opacityValue.Text=Math.Round(alpha*100).ToString(CultureInfo.InvariantCulture)+"%"+(follow.IsChecked==true?" · "+language.T("App"):"");
            };
            window.SourceInitialized+=delegate{resourcesAppearance();};
            follow.Click+=delegate{settings.Data["resourceFollowApp"]=follow.IsChecked==true;resourcesAppearance();QueueSave();};
            opacity.ValueChanged+=delegate{settings.Data["resourceOpacity"]=opacity.Value;resourcesAppearance();QueueSave();};
            list.SizeChanged+=delegate{double available=Math.Max(330,list.ActualWidth-36);columns.Columns[0].Width=28;columns.Columns[2].Width=80;columns.Columns[3].Width=130;columns.Columns[1].Width=available-238;};
            bool busy=false,descending=true;AppResourceProcess[] snapshot=null;AppResourceProcess[] suggestions=new AppResourceProcess[0];
            Func<ResourceRow[]> selected=()=>list.SelectedItems.Cast<ResourceRow>().Where(r=>r.Process.CanClose).OrderBy(r=>r.Process.Pid).ToArray();
            Action selection=delegate{var rows=selected();close.IsEnabled=!busy&&rows.Length>0;selectedStatus.Text=rows.Length==0?language.T("Check apps to close. Nothing is selected automatically."):language.T("Selected apps")+": "+rows.Length+" · "+language.T("Save work before closing.");};
            list.SelectionChanged+=delegate{selection();};
            Action sortSnapshot=delegate{
                if(snapshot==null)return;
                var identities=new HashSet<string>(selected().Select(r=>r.Process.Pid+":"+r.Process.StartedUtcTicks));
                IOrderedEnumerable<AppResourceProcess> ordered;
                if(sort.SelectedIndex==2)ordered=descending?snapshot.OrderByDescending(p=>p.Name,StringComparer.OrdinalIgnoreCase):snapshot.OrderBy(p=>p.Name,StringComparer.OrdinalIgnoreCase);
                else if(sort.SelectedIndex==1)ordered=snapshot.OrderBy(p=>p.GpuBytes.HasValue?0:1).ThenBy(p=>descending?-(p.GpuBytes??0):p.GpuBytes??0);
                else ordered=descending?snapshot.OrderByDescending(p=>p.RamBytes):snapshot.OrderBy(p=>p.RamBytes);
                suggestions=WindowsAppResources.ReviewCandidates(snapshot,sort.SelectedIndex==1);var recommended=new HashSet<AppResourceProcess>(suggestions);
                var rows=ordered.ThenBy(p=>p.Name,StringComparer.OrdinalIgnoreCase).ThenBy(p=>p.Pid).Select(p=>new ResourceRow {Process=p,Action=language.T(recommended.Contains(p)?"Review if unused: high usage, normal close available. Keep it if you still need it.":p.CanClose?"Close available":"No close action")}).ToArray();
                list.ItemsSource=rows;foreach(var row in rows)if(identities.Contains(row.Process.Pid+":"+row.Process.StartedUtcTicks))list.SelectedItems.Add(row);
                candidates.Text=suggestions.Length==0?language.T("No high-usage close candidates in this snapshot."):language.T("Review if unused")+": "+string.Join(", ",suggestions.Select(p=>p.Name));
                candidates.ToolTip=suggestions.Length==0?null:language.T("Suggestions use reported usage, not proof that an app is unused. Foreground and Windows shell/input apps are excluded.")+Environment.NewLine+string.Join(Environment.NewLine,suggestions.Select(p=>p.Name+" · RAM "+ResourceBytes(p.RamBytes)+" · GPU "+(p.GpuBytes.HasValue?ResourceBytes(p.GpuBytes.Value):"—")));
                suggestedClose.IsEnabled=!busy&&suggestions.Length>0;
                direction.Content=language.T(sort.SelectedIndex==2?(descending?"Z to A":"A to Z"):(descending?"Highest first":"Lowest first"));
                for(int i=1;i<columns.Columns.Count;i++){var header=(GridViewColumnHeader)columns.Columns[i].Header;bool active=sort.SelectedIndex==(i==1?2:i==2?0:1);header.Content=language.T(i==1?"App":i==2?"RAM":"GPU (estimate)")+(active?(descending?" ↓":" ↑"):"");}selection();
            };
            sort.SelectionChanged+=delegate{descending=sort.SelectedIndex!=2;sortSnapshot();};
            direction.Click+=delegate{descending=!descending;sortSnapshot();};
            list.AddHandler(GridViewColumnHeader.ClickEvent,new RoutedEventHandler(delegate(object sender,RoutedEventArgs e){var header=e.OriginalSource as GridViewColumnHeader;if(header==null||header.Tag==null)return;int index=(string)header.Tag=="Name"?2:(string)header.Tag=="Ram"?0:1;if(sort.SelectedIndex==index){descending=!descending;sortSnapshot();}else sort.SelectedIndex=index;}));
            Func<Task> refreshSnapshot=async delegate{
                if(busy)return;busy=true;refresh.IsEnabled=false;suggestedClose.IsEnabled=false;selection();status.Text=language.T("Reading app usage…");Task<AppResourceSnapshot> pending=null;
                try{
                    if(resourceReadTask==null||resourceReadTask.IsCompleted)resourceReadTask=Task.Run(readAppResources);
                    pending=resourceReadTask;var value=await pending;if(disposed||resourcesWindow!=window)return;
                    // New snapshots require a fresh explicit selection, even when PID/birth still match.
                    list.SelectedItems.Clear();snapshot=value.Processes;sortSnapshot();double free;
                    if(!TryResourceHeadroom(value.Ram,out free))advice.Text=language.T("RAM pressure is unavailable. Choose only apps you know you no longer need.");
                    else{string key=free<1||free<2&&value.Ram.usedGb.Value/value.Ram.totalGb.Value>0.85?"RAM headroom is low. Save your work, then close apps you no longer need.":"RAM has headroom. Cleanup is not needed based on this snapshot.";advice.Text=language.T(key)+"  "+language.T("Available RAM")+": "+free.ToString("F1",CultureInfo.InvariantCulture)+" GB";}
                    status.Text=language.T(value.GpuAvailable?"Usage updated. Nothing is closed automatically.":"Usage updated. GPU per-process readings are unavailable.");
                }catch{if(resourcesWindow==window)status.Text=language.T("Could not read app usage. Try Refresh.");}
                finally{if(pending!=null&&object.ReferenceEquals(resourceReadTask,pending)&&pending.IsCompleted)resourceReadTask=null;if(resourcesWindow==window){busy=false;refresh.IsEnabled=true;suggestedClose.IsEnabled=suggestions.Length>0;selection();}}
            };
            refresh.Click+=async delegate{await refreshSnapshot();};
            Func<ResourceRow[],string,Task> closeRows=async (rows,title)=>{
                if(busy||rows.Length==0)return;
                busy=true;refresh.IsEnabled=false;suggestedClose.IsEnabled=false;list.IsEnabled=false;selection();
                try{
                    string prompt=language.T("Send normal close requests to these selected apps? Save your work first. Each app may show a save prompt or refuse to close.")+"\n\n"+string.Join("\n",rows.Take(12).Select(r=>r.Name));
                    if(rows.Length>12)prompt+="\n… "+language.T("Selected apps")+": "+rows.Length;
                    if(!confirmResourceApps(window,prompt,title))return;
                    int requested=0,unavailable=0;
                    foreach(var row in rows){
                        if(disposed||resourcesWindow!=window)break;
                        status.Text=language.T("Requesting app close…")+" "+row.Name;
                        var result=await Task.Run(()=>closeResourceApp(row.Process));
                        if(result==AppCloseResult.Requested)requested++;else unavailable++;
                        row.Process.CanClose=false;
                    }
                    if(disposed||resourcesWindow!=window)return;
                    list.SelectedItems.Clear();sortSnapshot();status.Text=language.T("Close requests sent")+": "+requested+" · "+language.T("Unavailable")+": "+unavailable+". "+language.T("Complete app prompts, then Refresh to check usage.");
                }catch{if(resourcesWindow==window)status.Text=language.T("The app could not be closed. It may have exited, denied access or opened a dialog.");}
                finally{if(resourcesWindow==window){busy=false;refresh.IsEnabled=true;list.IsEnabled=true;suggestedClose.IsEnabled=suggestions.Length>0;selection();}}
            };
            close.Click+=async delegate{await closeRows(selected(),language.T("Close Selected Apps"));};
            suggestedClose.Click+=async delegate{await closeRows(list.Items.Cast<ResourceRow>().Where(r=>suggestions.Contains(r.Process)&&r.Process.CanClose).OrderBy(r=>r.Process.Pid).ToArray(),language.T("Close Suggested Apps"));};
            window.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Escape)window.Close();};
            window.Closed+=delegate{if(resourcesWindow==window){resourcesWindow=null;resourcesSort=null;resourcesAppearance=null;}snapshot=null;list.ItemsSource=null;};
            window.Loaded+=async delegate{selection();await refreshSnapshot();};window.Show();
        }
    }
}
