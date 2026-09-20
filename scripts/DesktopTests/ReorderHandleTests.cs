using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class ReorderHandleTests {
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
    public static void Run() {
        string directory=Directory.CreateTempSubdirectory("pulse-reorder-").FullName;
        var owner=new MonitorWindow(new MonitorSource(true),start:false,store:new PreviewSettingsStore(Path.Combine(directory,"settings.json")));
        try {
            owner.Height=700;owner.Show();
            owner.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="OpenSettings").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Dispatcher.UIThread.RunJobs();
            var tabs=owner.GetVisualDescendants().OfType<TabControl>().Single(x=>x.Name=="SettingsTabs");tabs.SelectedIndex=3;Dispatcher.UIThread.RunJobs();
            Control Handle(string key)=>owner.GetVisualDescendants().OfType<Border>().Single(x=>x.Name=="DragCard"+key);
            Point Point(Control c)=>c.TranslatePoint(new Point(c.Bounds.Width/2,c.Bounds.Height/2),owner)!.Value;
            var panel=owner.GetVisualDescendants().OfType<StackPanel>().Single(x=>x.Name=="CardPreferences");
            string[] Order()=>panel.Children.Select(x=>(string)x.Tag!).ToArray();
            var start=Point(Handle("CPU"));var end=Point(Handle("Memory"))+new Vector(0,10);
            owner.MouseDown(start,MouseButton.Left);owner.MouseMove(end,RawInputModifiers.LeftMouseButton);Dispatcher.UIThread.RunJobs();
            Check(Order()[0]=="CPU","Drag preview does not mutate canonical order");
            owner.MouseUp(end,MouseButton.Left);Dispatcher.UIThread.RunJobs();
            Check(Order()[2]=="CPU","Pointer drop commits row order");
            var before=Order();start=Point(Handle("CPU"));end=Point(Handle(before[0]));
            owner.MouseDown(start,MouseButton.Left);owner.MouseMove(end,RawInputModifiers.LeftMouseButton);
            owner.KeyPress(Key.Escape,RawInputModifiers.None,PhysicalKey.Escape," ");owner.KeyRelease(Key.Escape,RawInputModifiers.None,PhysicalKey.Escape," ");owner.MouseUp(end,MouseButton.Left);Dispatcher.UIThread.RunJobs();
            Check(Order().SequenceEqual(before)&&panel.Children.All(x=>x.RenderTransform==null),"Escape cancels order and preview transforms");
            Handle(before[0]).Focus();owner.KeyPress(Key.Down,RawInputModifiers.None,PhysicalKey.ArrowDown," ");owner.KeyRelease(Key.Down,RawInputModifiers.None,PhysicalKey.ArrowDown," ");Dispatcher.UIThread.RunJobs();
            Check(Order()[1]==before[0],"Focused handle supports keyboard reorder");
            var expected=Order();
            tabs.SelectedIndex=2;Dispatcher.UIThread.RunJobs();
            var desktopList=owner.GetVisualDescendants().OfType<StackPanel>().Single(x=>x.Name=="DesktopMetricPreferences");
            var desktopCpu=owner.GetVisualDescendants().OfType<Border>().Single(x=>x.Name=="DragDesktopCPU");
            var scroll=desktopCpu.GetVisualAncestors().OfType<ScrollViewer>().First();scroll.Offset=new Vector(0,desktopList.Bounds.Y);Dispatcher.UIThread.RunJobs();
            var desktopMemory=owner.GetVisualDescendants().OfType<Border>().Single(x=>x.Name=="DragDesktopMemory");
            start=Point(desktopCpu);end=Point(desktopMemory)+new Vector(0,10);
            owner.MouseDown(start,MouseButton.Left);owner.MouseMove(end,RawInputModifiers.LeftMouseButton);owner.MouseUp(end,MouseButton.Left);Dispatcher.UIThread.RunJobs();
            Check((string)desktopList.Children[3].Tag! == "CPU","Same pointer handle commits independent Desktop order");
            owner.Close();
            Check(new PreviewSettingsStore(Path.Combine(directory,"settings.json")).Load().CardOrder.SequenceEqual(expected),"Completed order survives close");
            Console.WriteLine("PASS shared reorder handle: pointer preview/drop, Escape, keyboard and persistence");
        } finally {owner.Close();Directory.Delete(directory,true);}
    }
}
