using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace HardwarePulse {
    public sealed partial class Shell {
        DockPanel resourcesView;
        Action<bool> resourcesSort;
        Action resourcesAppearance,resourcesTeardown;
        bool resourcesVisible,returnToResources;
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
        void ApplyAppNavigation(){
            foreach(string name in new[]{"MonitorControls","CardScroll","Status"})Control<FrameworkElement>(name).Visibility=!settingsVisible&&!resourcesVisible?Visibility.Visible:Visibility.Collapsed;
            Control<FrameworkElement>("MainTabs").Visibility=settingsVisible?Visibility.Collapsed:Visibility.Visible;
            Control<ScrollViewer>("ResourcesPage").Visibility=!settingsVisible&&resourcesVisible?Visibility.Visible:Visibility.Collapsed;
            foreach(string name in new[]{"MonitorTab","ResourcesTab"}){
                bool selected=name=="ResourcesTab"?resourcesVisible:!resourcesVisible;var button=Control<Button>(name);
                button.Background=Brush(selected?"#607898A8":"#00000000");button.FontWeight=selected?FontWeights.SemiBold:FontWeights.Normal;
                AutomationProperties.SetHelpText(button,selected?language.T("Selected"):"");
            }
            if(settingsVisible||resourcesVisible)Control<TextBlock>("CardsEmpty").Visibility=Visibility.Collapsed;
        }
        void HideAppResources(){
            resourcesVisible=false;var teardown=resourcesTeardown;resourcesTeardown=null;if(teardown!=null)teardown();
            resourcesView=null;resourcesSort=null;resourcesAppearance=null;Control<ScrollViewer>("ResourcesPage").Content=null;ApplyAppNavigation();
        }
        void ShowMonitor(){returnToResources=false;HideAppResources();ShowSettings(false);}
        void WireAppNavigation(){
            Click("MonitorTab",ShowMonitor);Click("ResourcesTab",()=>{if(!resourcesVisible)ShowAppResources(false);});
            foreach(string name in new[]{"MonitorTab","ResourcesTab"})Control<Button>(name).PreviewKeyDown+=delegate(object sender,KeyEventArgs e){
                if(e.Key==Key.Left||e.Key==Key.Home){ShowMonitor();Control<Button>("MonitorTab").Focus();e.Handled=true;}
                else if(e.Key==Key.Right||e.Key==Key.End){if(!resourcesVisible)ShowAppResources(false);Control<Button>("ResourcesTab").Focus();e.Handled=true;}
            };
            ApplyAppNavigation();
        }
        // One page and one on-demand read. Leaving the page invalidates undispatched close requests.
        void ShowAppResources(bool gpu){
            if(settingsVisible){returnToResources=false;ShowSettings(false);}
            if(resourcesView!=null){resourcesSort(gpu);return;}
            var window=Window;var page=Control<ScrollViewer>("ResourcesPage");
            var root=new DockPanel {Margin=new Thickness(0,0,0,8)};TextElement.SetFontSize(root,Math.Max(12,Window.FontSize));
            resourcesView=root;resourcesVisible=true;NameScope.SetNameScope(root,new NameScope());page.Content=root;ApplyAppNavigation();
            var heading=new StackPanel {Margin=new Thickness(0,0,0,12)};DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
            var titleLine=new DockPanel();heading.Children.Add(titleLine);
            var refresh=new Button {Content=language.T("Refresh"),Margin=new Thickness(8,0,0,0)};DockPanel.SetDock(refresh,Dock.Right);titleLine.Children.Add(refresh);
            titleLine.Children.Add(new TextBlock {Text=language.T("Resources"),FontSize=20,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});
            heading.Children.Add(new TextBlock {Text=language.T("Select an app you no longer need. Closing it can release RAM and GPU resources."),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)});
            var advice=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)};heading.Children.Add(advice);
            var candidates=new TextBlock {TextTrimming=TextTrimming.CharacterEllipsis,TextWrapping=TextWrapping.NoWrap,Margin=new Thickness(0,6,0,0)};heading.Children.Add(candidates);
            var status=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0)};heading.Children.Add(status);
            var suggestedClose=new Button {Content=language.T("Close suggestions…"),Margin=new Thickness(0,0,8,4),IsEnabled=false};
            var footer=new StackPanel {Margin=new Thickness(0,10,0,0)};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            var selectedStatus=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,6)};footer.Children.Add(selectedStatus);
            footer.Children.Add(new TextBlock {Text=language.T("Each row is one process. Closing an app may also close its child processes."),TextWrapping=TextWrapping.Wrap,FontSize=11,Opacity=0.85});
            footer.Children.Add(new TextBlock {Text=language.T("RAM is resident memory. GPU values are estimates and may include shared resources; — means unavailable."),TextWrapping=TextWrapping.Wrap,FontSize=11,Opacity=0.85});
            var actions=new WrapPanel {Margin=new Thickness(0,10,0,0)};footer.Children.Add(actions);
            var close=new Button {Content=language.T("Close Selected Apps"),IsEnabled=false,Margin=new Thickness(0,0,0,4)};actions.Children.Add(suggestedClose);actions.Children.Add(close);
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
                text.SetValue(FrameworkElement.HorizontalAlignmentProperty,entry[1]=="Name"?HorizontalAlignment.Left:HorizontalAlignment.Right);
                text.SetValue(TextBlock.TextWrappingProperty,TextWrapping.NoWrap);text.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);
                columns.Columns.Add(new GridViewColumn {Header=new GridViewColumnHeader {Content=language.T(entry[0]),Tag=entry[1]},CellTemplate=new DataTemplate {VisualTree=text},Width=entry[1]=="Name"?260:entry[1]=="Gpu"?130:80});
            }
            var rowStyle=new Style(typeof(ListViewItem));rowStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,new Binding("Tooltip")));rowStyle.Setters.Add(new Setter(System.Windows.Controls.Control.PaddingProperty,new Thickness(4,5,4,5)));list.ItemContainerStyle=rowStyle;
            rowStyle.Setters.Add(new Setter(System.Windows.Controls.Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));
            var headerStyle=new Style(typeof(GridViewColumnHeader));headerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.PaddingProperty,new Thickness(8,6,8,6)));
            headerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty,new DynamicResourceExtension("ResourceHeaderBackground")));
            headerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty,new DynamicResourceExtension("ResourceForeground")));
            headerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.TemplateProperty,(ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" TargetType=\"GridViewColumnHeader\"><Border Background=\"{TemplateBinding Background}\" BorderBrush=\"{TemplateBinding BorderBrush}\" BorderThickness=\"0,0,0,1\" Padding=\"{TemplateBinding Padding}\"><ContentPresenter HorizontalAlignment=\"{TemplateBinding HorizontalContentAlignment}\" VerticalAlignment=\"Center\" /></Border><ControlTemplate.Triggers><Trigger Property=\"IsMouseOver\" Value=\"True\"><Setter Property=\"Opacity\" Value=\"0.8\" /></Trigger><Trigger Property=\"IsKeyboardFocused\" Value=\"True\"><Setter Property=\"BorderBrush\" Value=\"#A5E7D5\" /></Trigger></ControlTemplate.Triggers></ControlTemplate>")));columns.ColumnHeaderContainerStyle=headerStyle;
            for(int i=1;i<columns.Columns.Count;i++)((GridViewColumnHeader)columns.Columns[i].Header).HorizontalContentAlignment=i==1?HorizontalAlignment.Left:HorizontalAlignment.Right;
            foreach(var pair in new[]{Tuple.Create("ResourceList",(object)list),Tuple.Create("ResourceSuggestions",(object)suggestedClose),Tuple.Create("ResourceCandidates",(object)candidates),Tuple.Create("ResourceRefresh",(object)refresh),Tuple.Create("ResourceClose",(object)close),Tuple.Create("ResourceAdvice",(object)advice),Tuple.Create("ResourceStatus",(object)status)})root.RegisterName(pair.Item1,pair.Item2);
            Action sizePage=()=>root.Height=Math.Max(440*TextElement.GetFontSize(root)/12,Math.Max(1,page.ActualHeight-8));
            SizeChangedEventHandler pageSize=delegate{sizePage();};page.SizeChanged+=pageSize;
            resourcesAppearance=delegate{
                var foreground=SystemParameters.HighContrast?SystemColors.WindowTextBrush:Brush(light?"#17202B":"#E5EDF3");TextElement.SetForeground(root,foreground);
                root.Resources["ResourceForeground"]=foreground;root.Resources["ResourceHeaderBackground"]=SystemParameters.HighContrast?SystemColors.ControlBrush:Brush(light?"#DDEEF1F4":"#8031485B");
                list.Background=SystemParameters.HighContrast?SystemColors.WindowBrush:Brush("#00000000");list.Foreground=foreground;
                foreach(var button in new[]{suggestedClose,refresh,close})button.Foreground=foreground;
                foreach(var column in columns.Columns){var header=column.Header as GridViewColumnHeader;if(header!=null){header.Background=SystemParameters.HighContrast?SystemColors.ControlBrush:Brush(light?"#DDEEF1F4":"#8031485B");header.Foreground=foreground;header.BorderBrush=list.BorderBrush;}}
                sizePage();
            };
            list.SizeChanged+=delegate{double available=Math.Max(160,list.ActualWidth-36);bool narrow=available<400;columns.Columns[0].Width=24;columns.Columns[2].Width=narrow?56:80;columns.Columns[3].Width=narrow?60:130;columns.Columns[1].Width=Math.Max(20,available-columns.Columns[0].Width-columns.Columns[2].Width-columns.Columns[3].Width);};
            bool busy=false,descending=true;int sortIndex=gpu?1:0;AppResourceProcess[] snapshot=null;AppResourceProcess[] suggestions=new AppResourceProcess[0];
            Func<ResourceRow[]> selected=()=>list.SelectedItems.Cast<ResourceRow>().Where(r=>r.Process.CanClose).OrderBy(r=>r.Process.Pid).ToArray();
            Action selection=delegate{var rows=selected();close.IsEnabled=!busy&&rows.Length>0;selectedStatus.Text=rows.Length==0?language.T("Check apps to close. Nothing is selected automatically."):language.T("Selected apps")+": "+rows.Length+" · "+language.T("Save work before closing.");};
            list.SelectionChanged+=delegate{selection();};
            Action sortSnapshot=delegate{
                if(snapshot==null)return;
                var identities=new HashSet<string>(selected().Select(r=>r.Process.Pid+":"+r.Process.StartedUtcTicks));
                IOrderedEnumerable<AppResourceProcess> ordered;
                if(sortIndex==2)ordered=descending?snapshot.OrderByDescending(p=>p.Name,StringComparer.OrdinalIgnoreCase):snapshot.OrderBy(p=>p.Name,StringComparer.OrdinalIgnoreCase);
                else if(sortIndex==1)ordered=snapshot.OrderBy(p=>p.GpuBytes.HasValue?0:1).ThenBy(p=>descending?-(p.GpuBytes??0):p.GpuBytes??0);
                else ordered=descending?snapshot.OrderByDescending(p=>p.RamBytes):snapshot.OrderBy(p=>p.RamBytes);
                suggestions=WindowsAppResources.ReviewCandidates(snapshot,sortIndex==1);var recommended=new HashSet<AppResourceProcess>(suggestions);
                var rows=ordered.ThenBy(p=>p.Name,StringComparer.OrdinalIgnoreCase).ThenBy(p=>p.Pid).Select(p=>new ResourceRow {Process=p,Action=language.T(recommended.Contains(p)?"Review if unused: high usage, normal close available. Keep it if you still need it.":p.CanClose?"Close available":"No close action")}).ToArray();
                list.ItemsSource=rows;foreach(var row in rows)if(identities.Contains(row.Process.Pid+":"+row.Process.StartedUtcTicks))list.SelectedItems.Add(row);
                candidates.Text=suggestions.Length==0?language.T("No high-usage close candidates in this snapshot."):language.T("Review if unused")+": "+string.Join(", ",suggestions.Select(p=>p.Name));
                candidates.ToolTip=suggestions.Length==0?null:language.T("Suggestions use reported usage, not proof that an app is unused. Foreground and Windows shell/input apps are excluded.")+Environment.NewLine+string.Join(Environment.NewLine,suggestions.Select(p=>p.Name+" · RAM "+ResourceBytes(p.RamBytes)+" · GPU "+(p.GpuBytes.HasValue?ResourceBytes(p.GpuBytes.Value):"—")));
                suggestedClose.IsEnabled=!busy&&suggestions.Length>0;
                for(int i=1;i<columns.Columns.Count;i++){var header=(GridViewColumnHeader)columns.Columns[i].Header;bool active=sortIndex==(i==1?2:i==2?0:1);header.Content=language.T(i==1?"App":i==2?"RAM":"GPU")+(active?(descending?" ↓":" ↑"):"");header.ToolTip=language.T(i==3?"GPU (estimate)":"Click to sort; click again to reverse.");}selection();
            };
            resourcesSort=delegate(bool byGpu){sortIndex=byGpu?1:0;descending=true;sortSnapshot();};
            list.AddHandler(GridViewColumnHeader.ClickEvent,new RoutedEventHandler(delegate(object sender,RoutedEventArgs e){var header=e.OriginalSource as GridViewColumnHeader;if(header==null||header.Tag==null||busy)return;int index=(string)header.Tag=="Name"?2:(string)header.Tag=="Ram"?0:1;if(sortIndex==index)descending=!descending;else{sortIndex=index;descending=index!=2;}sortSnapshot();}));
            Func<Task> refreshSnapshot=async delegate{
                if(busy)return;busy=true;refresh.IsEnabled=false;suggestedClose.IsEnabled=false;selection();status.Text=language.T("Reading app usage…");Task<AppResourceSnapshot> pending=null;
                try{
                    if(resourceReadTask==null||resourceReadTask.IsCompleted)resourceReadTask=Task.Run(readAppResources);
                    pending=resourceReadTask;var value=await pending;if(disposed||resourcesView!=root)return;
                    // New snapshots require a fresh explicit selection, even when PID/birth still match.
                    list.SelectedItems.Clear();snapshot=value.Processes;sortSnapshot();double free;
                    if(!TryResourceHeadroom(value.Ram,out free))advice.Text=language.T("RAM pressure is unavailable. Choose only apps you know you no longer need.");
                    else{string key=free<1||free<2&&value.Ram.usedGb.Value/value.Ram.totalGb.Value>0.85?"RAM headroom is low. Save your work, then close apps you no longer need.":"RAM has headroom. Cleanup is not needed based on this snapshot.";advice.Text=language.T(key)+"  "+language.T("Available RAM")+": "+free.ToString("F1",CultureInfo.InvariantCulture)+" GB";}
                    status.Text=language.T(value.GpuAvailable?"Usage updated. Nothing is closed automatically.":"Usage updated. GPU per-process readings are unavailable.");
                }catch{if(resourcesView==root)status.Text=language.T("Could not read app usage. Try Refresh.");}
                finally{if(pending!=null&&object.ReferenceEquals(resourceReadTask,pending)&&pending.IsCompleted)resourceReadTask=null;if(resourcesView==root){busy=false;refresh.IsEnabled=true;suggestedClose.IsEnabled=suggestions.Length>0;selection();}}
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
                        if(disposed||resourcesView!=root)break;
                        status.Text=language.T("Requesting app close…")+" "+row.Name;
                        var result=await Task.Run(()=>closeResourceApp(row.Process));
                        if(result==AppCloseResult.Requested)requested++;else unavailable++;
                        row.Process.CanClose=false;
                    }
                    if(disposed||resourcesView!=root)return;
                    list.SelectedItems.Clear();sortSnapshot();status.Text=language.T("Close requests sent")+": "+requested+" · "+language.T("Unavailable")+": "+unavailable+". "+language.T("Complete app prompts, then Refresh to check usage.");
                }catch{if(resourcesView==root)status.Text=language.T("The app could not be closed. It may have exited, denied access or opened a dialog.");}
                finally{if(resourcesView==root){busy=false;refresh.IsEnabled=true;list.IsEnabled=true;suggestedClose.IsEnabled=suggestions.Length>0;selection();}}
            };
            close.Click+=async delegate{await closeRows(selected(),language.T("Close Selected Apps"));};
            suggestedClose.Click+=async delegate{await closeRows(list.Items.Cast<ResourceRow>().Where(r=>suggestions.Contains(r.Process)&&r.Process.CanClose).OrderBy(r=>r.Process.Pid).ToArray(),language.T("Close Suggested Apps"));};
            root.PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Escape){ShowMonitor();Control<Button>("MonitorTab").Focus();e.Handled=true;}};
            resourcesTeardown=delegate{page.SizeChanged-=pageSize;snapshot=null;list.ItemsSource=null;};
            resourcesAppearance();selection();refreshSnapshot();
        }
    }
}
