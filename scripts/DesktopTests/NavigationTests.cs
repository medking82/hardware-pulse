using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class NavigationTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run(string? output) {
        var source=new MonitorSource(true);
        var window=new MonitorWindow(source,start:false){Width=240,Height=340};window.Show();window.Present(source.Poll(null));Dispatcher.UIThread.RunJobs();
        try {
            T Find<T>(string name) where T:Control=>window.GetVisualDescendants().OfType<T>().Single(x=>x.Name==name);
            void Press(Button button){button.Focus();window.KeyPress(Key.Space,RawInputModifiers.None,PhysicalKey.Space," ");window.KeyRelease(Key.Space,RawInputModifiers.None,PhysicalKey.Space," ");Dispatcher.UIThread.RunJobs();}
            var live=Find<Avalonia.Controls.Primitives.ToggleButton>("Live");
            var max=Find<Avalonia.Controls.Primitives.ToggleButton>("SessionMax");
            var details=Find<Avalonia.Controls.Primitives.ToggleButton>("Details");
            void SelectedPlate(Avalonia.Controls.Primitives.ToggleButton button,bool selected) {
                var plate=button.GetVisualDescendants().OfType<Border>().Single(x=>x.Name=="ModePlate");
                Check(plate.CornerRadius==new CornerRadius(15),"Original rounded mode plate");
                var color=((Avalonia.Media.ISolidColorBrush)plate.Background!).Color;
                Check(selected?color==Avalonia.Media.Color.Parse("#607898A8"):color.A==0,"Mode plate reflects selection without Fluent accent");
            }
            SelectedPlate(live,true);SelectedPlate(max,false);SelectedPlate(details,false);
            Press(max);Check(max.IsChecked==true&&live.IsChecked==false,"Keyboard selects exclusive Session Max");SelectedPlate(max,true);SelectedPlate(live,false);
            Press(live);Press(live);Check(live.IsChecked==true&&max.IsChecked==false,"Active Live remains selected on repeated keyboard activation");
            Press(details);Check(details.IsChecked==true,"Keyboard enables Details");SelectedPlate(details,true);
            Press(details);Check(details.IsChecked==false,"Keyboard disables Details");SelectedPlate(details,false);
            var cards=Find<Grid>("ReadingCards");var settings=Find<Button>("OpenSettings");
            var monitorScroll=window.GetVisualDescendants().OfType<ScrollViewer>().Single();
            var desktop=Find<Button>("OpenFloatingMonitor");
            Check(desktop.GetVisualAncestors().OfType<WrapPanel>().Any(x=>x.Name=="MonitorControls"),"Desktop quick action belongs to fixed Monitor controls");
            var desktopPosition=desktop.TranslatePoint(new Point(),window)!.Value;
            var before=settings.TranslatePoint(new Point(),window)!.Value;
            monitorScroll.Offset=new Vector(0,10000);Dispatcher.UIThread.RunJobs();
            Check(settings.TranslatePoint(new Point(),window)!.Value==before,"Settings stays fixed below scrolling cards");
            Check(desktop.TranslatePoint(new Point(),window)!.Value==desktopPosition,"Desktop quick action stays reachable while cards scroll");
            Press(desktop);Check(window.FloatingMonitor?.IsVisible==true,"Keyboard Desktop quick action opens existing shared Desktop");
            var floating=window.FloatingMonitor!;
            floating.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="ReturnToApp").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();Check(window.FloatingMonitor==null&&window.IsVisible,"Return restores Monitor after quick action");
            Press(settings);
            var back=Find<Button>("Back");
            Check(back.IsFocused,"Settings keyboard activation transfers focus to Back");
            Check(!window.GetVisualDescendants().OfType<Grid>().Any(x=>x.Name=="ReadingCards"),"Monitor is removed from Settings navigation and focus tree");
            var tabs=Find<TabControl>("SettingsTabs");tabs.SelectedIndex=1;Dispatcher.UIThread.RunJobs();
            ((TabItem)tabs.Items[1]!).Focus();
            window.KeyPress(Key.Right,RawInputModifiers.None,PhysicalKey.ArrowRight,null);window.KeyRelease(Key.Right,RawInputModifiers.None,PhysicalKey.ArrowRight,null);Dispatcher.UIThread.RunJobs();
            Check(tabs.SelectedIndex==2,"Arrow key selects next category with custom template");
            window.KeyPress(Key.Left,RawInputModifiers.None,PhysicalKey.ArrowLeft,null);window.KeyRelease(Key.Left,RawInputModifiers.None,PhysicalKey.ArrowLeft,null);Dispatcher.UIThread.RunJobs();
            Check(tabs.SelectedIndex==1,"Arrow key returns to previous category");
            foreach(var item in tabs.Items.OfType<TabItem>()) {
                var plate=item.GetVisualDescendants().OfType<Border>().Single(x=>x.Name=="CategoryPlate");
                Check(plate.CornerRadius==new CornerRadius(15)&&plate.BorderThickness==new Thickness(item.IsSelected?2:1),"Category plate follows original rounded selection treatment");
                Check(!item.IsSelected||item.FontWeight==Avalonia.Media.FontWeight.Bold,"Selected category is bold");
                var position=item.TranslatePoint(new Point(),window)!.Value;
                Check(position.X>=0&&position.X+item.Bounds.Width<=window.ClientSize.Width,"Wrapped category stays inside minimum window width");
            }
            var category=(TabItem)tabs.Items[1]!;var categoryPosition=category.TranslatePoint(new Point(),window)!.Value;
            var scroll=window.GetVisualDescendants().OfType<ScrollViewer>().First(x=>x.Content is StackPanel);
            before=back.TranslatePoint(new Point(),window)!.Value;scroll.Offset=new Vector(0,10000);Dispatcher.UIThread.RunJobs();
            Check(back.TranslatePoint(new Point(),window)!.Value==before,"Back stays fixed above scrolling Settings");
            Check(scroll.Offset.Y>0&&category.TranslatePoint(new Point(),window)!.Value==categoryPosition,"Category navigation remains fixed while content really scrolls");
            window.Present(source.Poll(null) with {Cpu="33.0%"});
            if(output!=null){using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"navigation-settings.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
            Press(back);
            Check(settings.IsFocused&&ReferenceEquals(cards,Find<Grid>("ReadingCards")),"Back restores focus and existing card instances");
            Check(window.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="33.0%"),"Readings received while Settings is open survive return");
            Press(settings);Check(Find<TabControl>("SettingsTabs").SelectedIndex==1,"Reopening Settings retains category selection");
        } finally {window.Close();}
        Console.WriteLine("PASS navigation: fixed Settings/Back, keyboard focus, preserved controls and readings");
    }
}
