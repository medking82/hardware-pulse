using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using HardwarePulse.Desktop;

static class DesktopGeometryTests {
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
    static void Pump(){using var slice=new CancellationTokenSource(TimeSpan.FromMilliseconds(150));Dispatcher.UIThread.MainLoop(slice.Token);}
    public static void Run() {
        string directory=Directory.CreateTempSubdirectory("pulse-desktop-geometry-").FullName;
        MonitorWindow? owner=null;
        try {
            string path=Path.Combine(directory,"settings.json");
            File.WriteAllText(path,"{\"desktopWidth\":500,\"desktopHeight\":300,\"desktopX\":99999,\"desktopY\":99999}");
            owner=new MonitorWindow(new MonitorSource(true),start:false,store:new PreviewSettingsStore(path));owner.Show();owner.OpenFloatingMonitor();Pump();
            var desktop=owner.FloatingMonitor!;var screen=desktop.Screens.ScreenFromWindow(desktop)??desktop.Screens.Primary;
            Check(desktop.MinWidth==280&&desktop.MinHeight==140,"Original Desktop minimum size");
            if(screen!=null) {
                var area=screen.WorkingArea;
                Check(desktop.Position.X>=area.X&&desktop.Position.Y>=area.Y&&desktop.Position.X<area.Right&&desktop.Position.Y<area.Bottom,"Offscreen saved Desktop returns to working area");
            }
            desktop.Width=420;desktop.Height=310;desktop.KeepOnScreen(reset:true);Pump();
            var position=desktop.Position;double width=desktop.Width,height=desktop.Height;
            desktop.Close();owner.Close();owner=null;
            var saved=new PreviewSettingsStore(path).Load();
            Check(Math.Abs(saved.DesktopWidth-width)<1&&Math.Abs(saved.DesktopHeight-height)<1&&saved.DesktopX==position.X&&saved.DesktopY==position.Y,"Desktop geometry saved on resize/move/close");
            owner=new MonitorWindow(new MonitorSource(true),start:false,store:new PreviewSettingsStore(path));owner.Show();owner.OpenFloatingMonitor();Pump();desktop=owner.FloatingMonitor!;
            Check(Math.Abs(desktop.Width-width)<1&&Math.Abs(desktop.Height-height)<1&&desktop.Position==position,"Desktop geometry survives owner restart");
            desktop.Width=300;desktop.Height=180;Pump();
            Check(desktop.Width==300&&desktop.Height==180,"Original compact Desktop size remains usable");
            Console.WriteLine("PASS Desktop geometry: original minimum size, offscreen recovery, reset and restart persistence");
        } finally {owner?.Close();Directory.Delete(directory,true);}
    }
}
