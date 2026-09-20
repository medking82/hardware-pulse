using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class DesktopLifetimeTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Until(Func<bool> ready){var end=DateTime.UtcNow.AddSeconds(5);while(!ready()&&DateTime.UtcNow<end){using var slice=new CancellationTokenSource(30);Dispatcher.UIThread.MainLoop(slice.Token);}Check(ready(),"Lifetime transition timed out");}
    public static void Run() {
        string root=Directory.CreateTempSubdirectory("pulse-lifetime-").FullName;
        MonitorWindow? window=null;
        try {
            string stop=Path.Combine(root,"STOP");
            window=new MonitorWindow(new MonitorSource(true));window.Show();Dispatcher.UIThread.RunJobs();
            bool available=true,closed=false;window.Closed+=(_,_)=>closed=true;
            using var tray=new DesktopTray(window,closeToTray:true,trayAvailable:()=>available);
            var sampling=window.Sampling;
            window.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="Close").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(!window.IsVisible&&!closed&&!sampling.IsCompleted,"Titlebar close must retain existing sampler behind a reachable tray");
            ((NativeMenuItem)tray.Menu.Items[0]).Command!.Execute(null);
            Check(window.IsVisible&&ReferenceEquals(sampling,window.Sampling),"Tray restores the existing App");
            window.RequestUserClose();int shutdowns=0;
            using(var watcher=new DesktopStopMonitor(stop,()=>{shutdowns++;window.Close();})) {
                File.WriteAllText(stop,"synthetic upgrade");Until(()=>closed&&sampling.IsCompleted);
                Check(shutdowns==1&&File.ReadAllText(stop)=="synthetic upgrade","STOP closes hidden App without modifying installer-owned signal");
            }
            window=null;File.Delete(stop);int late=0;
            using(var disposed=new DesktopStopMonitor(stop,()=>late++)){}
            File.WriteAllText(stop,"after dispose");using(var slice=new CancellationTokenSource(650))Dispatcher.UIThread.MainLoop(slice.Token);
            Check(late==0,"Disposed STOP monitor must not callback");
            window=new MonitorWindow(new MonitorSource(true),start:false);window.Show();closed=false;window.Closed+=(_,_)=>closed=true;
            using(var fallback=new DesktopTray(window,closeToTray:true,trayAvailable:()=>false)){window.RequestUserClose();Check(closed,"Unavailable tray must not hide an unreachable App");}
            window=new MonitorWindow(new MonitorSource(true),start:false);window.Show();closed=false;window.Closed+=(_,_)=>closed=true;
            using(var explicitQuit=new DesktopTray(window,closeToTray:true,trayAvailable:()=>true)){
                ((NativeMenuItem)explicitQuit.Menu.Items[^1]).Command!.Execute(null);Check(closed,"Explicit Quit must bypass close-to-tray");
            }
            window=null;
            Console.WriteLine("PASS lifetime: titlebar close/tray restore, same sampler, hidden STOP, read-only signal, disposal, missing tray and explicit Quit");
        }finally{window?.Close();Directory.Delete(root,true);}
    }
}
