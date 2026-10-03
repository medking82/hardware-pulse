using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace HardwarePulse {
    public sealed partial class Shell {
        Window resourcesWindow;
        ComboBox resourcesSort;
        Func<AppResourceSnapshot> readAppResources=WindowsAppResources.Read;
        Task<AppResourceSnapshot> resourceReadTask;
        sealed class ResourceRow {
            public AppResourceProcess Process;
            public string Name {get{return Process.Name+" · "+Process.Pid.ToString(CultureInfo.InvariantCulture);}}
            public string Ram {get{return ResourceBytes(Process.RamBytes);}}
            public string Gpu {get{return Process.GpuBytes.HasValue?ResourceBytes(Process.GpuBytes.Value):"—";}}
            public string Action {get;set;}
            public string Tooltip {get{return Name+Environment.NewLine+Action;}}
        }
        static string ResourceBytes(long bytes){return bytes>=1073741824?((double)bytes/1073741824).ToString("F1",CultureInfo.InvariantCulture)+" GB":((double)bytes/1048576).ToString("F0",CultureInfo.InvariantCulture)+" MB";}
        static bool TryResourceHeadroom(RamUsage value,out double free){
            free=0;
            if(value==null||!value.totalGb.HasValue||!value.usedGb.HasValue)return false;
            double total=value.totalGb.Value,used=value.usedGb.Value;
            if(double.IsNaN(total)||double.IsInfinity(total)||double.IsNaN(used)||double.IsInfinity(used)||total<=0||used<0||used>total)return false;
            free=total-used;return true;
        }
        void AddResourceAction(CardView card,bool gpu){
            var button=new Button {Content=gpu?"Manage GPU Memory":"Manage RAM",Margin=new Thickness(0,8,0,0),HorizontalAlignment=HorizontalAlignment.Left};
            Catalog(button);button.Click+=delegate{ShowAppResources(gpu);};card.Body.Children.Add(button);
        }
        // One window and one on-demand operation; no persistent sampling timer.
        void ShowAppResources(bool gpu) {
            if(resourcesWindow!=null){resourcesSort.SelectedIndex=gpu?1:0;resourcesWindow.Activate();return;}
            var window=new Window {Title=language.T("Memory Cleanup"),Owner=Window,Width=720,Height=530,MinWidth=440,MinHeight=500,
                WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Brush(light?"#F3F6F8":"#202A33"),Foreground=Window.Foreground,
                FontFamily=Window.FontFamily,FontSize=13,UseLayoutRounding=true,ShowInTaskbar=false};
            resourcesWindow=window;window.Resources.MergedDictionaries.Add(Window.Resources);
            NameScope.SetNameScope(window,new NameScope());
            var root=new DockPanel {Margin=new Thickness(20)};window.Content=root;
            var heading=new StackPanel {Margin=new Thickness(0,0,0,14)};DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
            heading.Children.Add(new TextBlock {Text=language.T("Memory Cleanup"),FontSize=22,FontWeight=FontWeights.SemiBold});
            heading.Children.Add(new TextBlock {Text=language.T("Select an app you no longer need. Closing it can release RAM and GPU resources."),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)});
            var advice=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,0)};heading.Children.Add(advice);
            var status=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)};heading.Children.Add(status);
            var sorting=new WrapPanel {Margin=new Thickness(0,10,0,0)};heading.Children.Add(sorting);
            sorting.Children.Add(new TextBlock {Text=language.T("Sort by"),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,10,0)});
            var sort=new ComboBox {MinWidth=140};sort.Items.Add(language.T("RAM"));sort.Items.Add(language.T("GPU (estimate)"));sort.SelectedIndex=gpu?1:0;sorting.Children.Add(sort);resourcesSort=sort;
            var footer=new StackPanel {Margin=new Thickness(0,12,0,0)};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            var selectedStatus=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,6)};footer.Children.Add(selectedStatus);
            footer.Children.Add(new TextBlock {Text=language.T("Each row is one process. Closing an app may also close its child processes."),TextWrapping=TextWrapping.Wrap,FontSize=11,Opacity=0.85,Margin=new Thickness(0,0,0,4)});
            footer.Children.Add(new TextBlock {Text=language.T("RAM is resident memory. GPU values are estimates and may include shared resources; — means unavailable."),TextWrapping=TextWrapping.Wrap,FontSize=11,Opacity=0.85});
            var actions=new WrapPanel {Margin=new Thickness(0,10,0,0)};footer.Children.Add(actions);
            var refresh=new Button {Content=language.T("Refresh"),Foreground=window.Foreground,Margin=new Thickness(0,0,8,4)};
            var close=new Button {Content=language.T("Close Selected App"),Foreground=window.Foreground,IsEnabled=false,Margin=new Thickness(0,0,0,4)};actions.Children.Add(refresh);actions.Children.Add(close);
            var list=new ListView {Background=Brush(light?"#FFFFFF":"#18232D"),Foreground=window.Foreground,BorderBrush=Brush("#60718898"),BorderThickness=new Thickness(1)};
            ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);root.Children.Add(list);
            var columns=new GridView();list.View=columns;
            foreach(var entry in new[]{new[]{"App","Name"},new[]{"RAM","Ram"},new[]{"GPU (estimate)","Gpu"}}){
                var text=new FrameworkElementFactory(typeof(TextBlock));text.SetBinding(TextBlock.TextProperty,new Binding(entry[1]));
                text.SetValue(TextBlock.TextWrappingProperty,TextWrapping.NoWrap);text.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);
                columns.Columns.Add(new GridViewColumn {Header=language.T(entry[0]),CellTemplate=new DataTemplate {VisualTree=text},Width=entry[1]=="Name"?260:entry[1]=="Gpu"?130:90});
            }
            var rowStyle=new Style(typeof(ListViewItem));rowStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,new Binding("Tooltip")));list.ItemContainerStyle=rowStyle;
            window.RegisterName("ResourceList",list);window.RegisterName("ResourceSort",sort);window.RegisterName("ResourceRefresh",refresh);
            window.RegisterName("ResourceClose",close);window.RegisterName("ResourceAdvice",advice);window.RegisterName("ResourceStatus",status);
            list.SizeChanged+=delegate{
                double available=Math.Max(300,list.ActualWidth-36);
                columns.Columns[1].Width=80;columns.Columns[2].Width=130;columns.Columns[0].Width=available-210;
            };
            bool busy=false;
            AppResourceProcess[] snapshot=null;
            Action selection=delegate{var row=list.SelectedItem as ResourceRow;close.IsEnabled=!busy&&row!=null&&row.Process.CanClose;selectedStatus.Text=row==null?"":row.Name+" · "+row.Action;};
            list.SelectionChanged+=delegate{selection();};
            Action sortSnapshot=delegate{
                if(snapshot==null)return;
                var ordered=sort.SelectedIndex==1?snapshot.OrderByDescending(p=>p.GpuBytes??-1):snapshot.OrderByDescending(p=>p.RamBytes);
                list.ItemsSource=ordered.ThenBy(p=>p.Name,StringComparer.OrdinalIgnoreCase).ThenBy(p=>p.Pid).Select(p=>new ResourceRow {Process=p,Action=language.T(p.CanClose?"Close available":"No close action")}).ToArray();
            };
            sort.SelectionChanged+=delegate{sortSnapshot();};
            Func<Task> refreshSnapshot=async delegate{
                if(busy)return;busy=true;refresh.IsEnabled=false;selection();status.Text=language.T("Reading app usage…");
                Task<AppResourceSnapshot> pending=null;
                try{
                    // Reopening the window cannot start another read while the old one is finishing.
                    if(resourceReadTask==null||resourceReadTask.IsCompleted)resourceReadTask=Task.Run(readAppResources);
                    pending=resourceReadTask;var value=await pending;
                    if(disposed||resourcesWindow!=window)return;
                    snapshot=value.Processes;sortSnapshot();double free;
                    if(!TryResourceHeadroom(value.Ram,out free))advice.Text=language.T("RAM pressure is unavailable. Choose only apps you know you no longer need.");
                    else{
                        string key=free<1||free<2&&value.Ram.usedGb.Value/value.Ram.totalGb.Value>0.85?"RAM headroom is low. Save your work, then close apps you no longer need.":"RAM has headroom. Cleanup is not needed based on this snapshot.";
                        advice.Text=language.T(key)+"  "+language.T("Available RAM")+": "+free.ToString("F1",CultureInfo.InvariantCulture)+" GB";
                    }
                    status.Text=language.T(value.GpuAvailable?"Usage updated. Nothing is closed automatically.":"Usage updated. GPU per-process readings are unavailable.");
                }catch{if(resourcesWindow==window)status.Text=language.T("Could not read app usage. Try Refresh.");}
                finally{
                    if(pending!=null&&object.ReferenceEquals(resourceReadTask,pending)&&pending.IsCompleted)resourceReadTask=null;
                    if(resourcesWindow==window){busy=false;refresh.IsEnabled=true;selection();}
                }
            };
            refresh.Click+=async delegate{await refreshSnapshot();};
            close.Click+=async delegate{
                var row=list.SelectedItem as ResourceRow;if(busy||row==null||!row.Process.CanClose)return;
                string prompt=language.T("Send a normal close request to this app? Save any work first. The app may show a save prompt or refuse to close.")+"\n\n"+row.Name;
                if(MessageBox.Show(window,prompt,language.T("Close Selected App"),MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
                busy=true;refresh.IsEnabled=false;selection();status.Text=language.T("Requesting app close…");
                try{
                    var result=await Task.Run(()=>WindowsAppResources.RequestClose(row.Process));
                    if(disposed||resourcesWindow!=window)return;
                    status.Text=language.T(result==AppCloseResult.Requested?"Close requested. Complete any app prompt, then Refresh to check usage.":result==AppCloseResult.IdentityChanged?"This process changed. Refresh before selecting it again.":"The app could not be closed. It may have exited, denied access or opened a dialog.");
                    row.Process.CanClose=false;sortSnapshot();list.SelectedItem=null;
                }catch{if(resourcesWindow==window)status.Text=language.T("The app could not be closed. It may have exited, denied access or opened a dialog.");}
                finally{if(resourcesWindow==window){busy=false;refresh.IsEnabled=true;selection();}}
            };
            window.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Escape)window.Close();};
            window.Closed+=delegate{if(resourcesWindow==window){resourcesWindow=null;resourcesSort=null;}snapshot=null;list.ItemsSource=null;};
            window.Loaded+=async delegate{await refreshSnapshot();};window.Show();
        }
    }
}
