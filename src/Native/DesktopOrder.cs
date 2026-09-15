using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace HardwarePulse {
    public sealed partial class Shell {
        static readonly string[] desktopDefaultOrder={"CPU","GPU","vram","Memory","diskC","diskD","cpuFan","gpuFan","gpuFan2","bottom","top","netConnection","lanLink","wifiLink","wifiSignal","netSignal","netDown","netUp","fps","quotaCodex0","quotaAntigravity0","quotaAntigravity1","quotaClaude0","quotaClaude1"};
        string[] DesktopOrderKeys(){
            return settings.Order("desktopOrder",desktopDefaultOrder);
        }
        void SaveDesktopOrder(){
            settings.Data["desktopOrder"]=Control<StackPanel>("DesktopOrderList").Children.Cast<Border>().Select(row=>(string)row.Tag).ToArray();
            UpdateDesktop();QueueSave();
        }
        void BuildDesktopOrder(){
            var panel=Control<StackPanel>("DesktopOrderList");
            foreach(string key in DesktopOrderKeys()){
                var label=new TextBlock{VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};
                var handle=new Thumb{Width=30,Height=34,Cursor=Cursors.SizeAll,Focusable=true,ToolTip="Drag To Reorder (Esc To Cancel)"};Catalog(handle);
                handle.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" TargetType=\"Thumb\"><Border Background=\"Transparent\"><TextBlock Text=\"⠿\" Foreground=\"#C2D8E5\" FontSize=\"20\" HorizontalAlignment=\"Center\" VerticalAlignment=\"Center\"/></Border></ControlTemplate>");
                var visible=new CheckBox{IsChecked=DesktopMetricEnabled(key),VerticalAlignment=VerticalAlignment.Center,ToolTip="Show on Desktop"};visible.Click+=delegate{settings.Map("desktopVisible")[key]=visible.IsChecked==true;if(key=="fps")StartOverlay();UpdateDesktop();QueueSave();};
                var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});grid.ColumnDefinitions.Add(new ColumnDefinition());
                grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});Grid.SetColumn(visible,2);
                grid.Children.Add(handle);Grid.SetColumn(label,1);grid.Children.Add(label);grid.Children.Add(visible);
                var row=new Border{Tag=key,Child=grid,BorderThickness=new Thickness(1),BorderBrush=new SolidColorBrush(Color.FromArgb(60,160,190,205)),CornerRadius=new CornerRadius(6),Margin=new Thickness(0,0,0,4),Padding=new Thickness(0,2,8,2)};
                panel.Children.Add(row);
                CardDrag.Attach(panel,row,handle,Control<ScrollViewer>("SettingsPage"),SaveDesktopOrder);
                handle.PreviewKeyDown+=delegate(object sender,KeyEventArgs e){
                    if(handle.IsDragging||e.Key!=Key.Up&&e.Key!=Key.Down)return;
                    int index=panel.Children.IndexOf(row),target=index+(e.Key==Key.Up?-1:1);
                    if(target>=0&&target<panel.Children.Count){panel.Children.RemoveAt(index);panel.Children.Insert(target,row);SaveDesktopOrder();handle.Focus();row.BringIntoView();}e.Handled=true;
                };
            }
        }
        void UpdateDesktopOrderLabels(){
            foreach(Border row in Control<StackPanel>("DesktopOrderList").Children){
                string key=(string)row.Tag;
                string title=key=="fps"?"FPS · "+language.T("Current / Average / Minimum"):key=="lanLink"?language.T("LAN Link Speed"):key=="wifiLink"?language.T("Wi-Fi Link Speed"):key=="wifiSignal"?language.T("Wi-Fi Signal"):key=="netConnection"?language.T("Connection"):key=="netSignal"?language.T("Wi-Fi Signal"):key.StartsWith("quota")?QuotaDesktopTitle(key):key=="netDown"?language.T("Download"):key=="netUp"?language.T("Upload"):key=="vram"?language.T("VRAM"):key=="diskC"||key=="diskD"?Device(key,key=="diskC"?"Drive 1":"Drive 2"):key=="CPU"||key=="GPU"||key=="Memory"?language.T(key):DesktopFanTitle(key);
                var grid=(Grid)row.Child;((TextBlock)grid.Children[1]).Text=title;
                System.Windows.Automation.AutomationProperties.SetName(grid.Children[0],title);System.Windows.Automation.AutomationProperties.SetName(grid.Children[2],language.T("Show on Desktop")+" · "+title);
            }
        }
    }
}
