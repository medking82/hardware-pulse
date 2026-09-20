using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class DesktopModeTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Click(Window window,string name)=>window.GetVisualDescendants().OfType<Button>().Single(x=>x.Name==name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    public static void Run() {
        string directory=Path.Combine(Path.GetTempPath(),"pulse-desktop-mode-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);string path=Path.Combine(directory,"settings.json");
        MonitorWindow? owner=null;
        bool Flag(string name){using var json=JsonDocument.Parse(File.ReadAllText(path));return json.RootElement.GetProperty(name).GetBoolean();}
        MonitorWindow Open(){var window=new MonitorWindow(new MonitorSource(true),start:false,store:new PreviewSettingsStore(path));window.Show();Dispatcher.UIThread.RunJobs();return window;}
        try {
            File.WriteAllText(path,"{\"schema\":1,\"desktopEnabled\":true,\"desktopLocked\":true}");
            owner=Open();var desktop=owner.FloatingMonitor;
            Check(desktop!=null&&desktop.IsVisible,"Saved Desktop mode restores on startup");
            bool native=desktop!.CanLock;
            Check(native?desktop.IsLocked&&!owner.IsVisible:!desktop.IsLocked&&owner.IsVisible,"Restore locks supported Desktop; unsupported input leaves recovery UI");
            owner.Close();owner=null;
            Check(Flag("desktopEnabled"),"App shutdown retains enabled Desktop mode");
            owner=Open();desktop=owner.FloatingMonitor!;
            Check(desktop!=null&&desktop.IsVisible,"Desktop mode survives owner shutdown/reopen");
            owner.Show();Dispatcher.UIThread.RunJobs();
            Check(ReferenceEquals(desktop,owner.FloatingMonitor)&&owner.IsVisible,"Showing App does not re-run startup or hide it again");
            owner.OpenFloatingMonitor();Check(!desktop!.IsLocked,"Edit unlocks restored Desktop");
            Check(Flag("desktopEnabled")&&!Flag("desktopLocked"),"Edit state persists");
            if(native){Click(desktop,"LockFloatingMonitor");Check(desktop.IsLocked&&!owner.IsVisible&&Flag("desktopLocked"),"Done locks, saves and hides owner");owner.OpenFloatingMonitor();}
            Click(desktop,"ReturnToApp");
            Check(owner.IsVisible&&owner.FloatingMonitor==null&&!Flag("desktopEnabled"),"Return disables persistent Desktop and restores owner");
            owner.Close();owner=null;owner=Open();Check(owner.FloatingMonitor==null&&owner.IsVisible,"Return survives restart");
            Click(owner,"OpenSettings");owner.GetVisualDescendants().OfType<TabControl>().Single(x=>x.Name=="SettingsTabs").SelectedIndex=2;Dispatcher.UIThread.RunJobs();
            var enabled=owner.GetVisualDescendants().OfType<CheckBox>().SingleOrDefault(x=>x.Name=="DesktopEnabled");
            Check(enabled!=null,"Settings exposes original Desktop Mode control");
            var edit=owner.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="DesktopMove");
            Check(enabled!.IsChecked==false&&!edit.IsEnabled,"Disabled mode has no active edit action");
            enabled.IsChecked=true;Check(owner.FloatingMonitor is {IsLocked:false}&&Flag("desktopEnabled")&&edit.IsEnabled,"Settings enables same Desktop editor");
            enabled.IsChecked=false;Check(owner.FloatingMonitor==null&&!Flag("desktopEnabled")&&!edit.IsEnabled,"Settings disables Desktop without losing App");
            owner.OpenFloatingMonitor();Check(enabled.IsChecked==true&&edit.IsEnabled,"External entry synchronizes Settings mode");
            Click(owner.FloatingMonitor!,"ReturnToApp");Check(enabled.IsChecked==false&&!edit.IsEnabled,"Return synchronizes Settings mode");
            owner.OpenFloatingMonitor();desktop=owner.FloatingMonitor!;desktop.Close();
            Check(owner.IsVisible&&!Flag("desktopEnabled"),"Closing editor recovers owner and disables mode");
            owner.OpenFloatingMonitor();owner.Close();owner=null;
            Check(Flag("desktopEnabled")&&!Flag("desktopLocked"),"Shutdown preserves editing mode");
            owner=Open();Check(owner.FloatingMonitor is {IsLocked:false,IsVisible:true},"Unlocked editing mode restores");
            Console.WriteLine("PASS Desktop mode persistence: startup, lock/edit, Return, close recovery and shutdown");
        } finally {owner?.Close();Directory.Delete(directory,true);}
    }
}