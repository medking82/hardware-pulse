using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class StartupModeTests {
    public static string Mode="Monitor";
    public static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
    public static int Child(string mode) {
        Mode=mode;
        return AppBuilder.Configure<StartupFixture>().UsePlatformDetect().With(DesktopFonts.Options()).StartWithClassicDesktopLifetime([]);
    }
    public static void Native() {
        if(!OperatingSystem.IsWindows())return;
        foreach(string mode in new[]{"Monitor","Desktop","Tray","Unavailable","Activated","TrayExit","MonitorExit"}) {
            var info=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            info.ArgumentList.Add(typeof(StartupModeTests).Assembly.Location);info.ArgumentList.Add("--startup-fixture");info.ArgumentList.Add(mode);
            using var process=Process.Start(info)!;
            var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
            try {Check(process.WaitForExit(12000),"Startup fixture did not quit: "+mode);Check(process.ExitCode==0&&stdout.Result.Contains("ownerClosed=1"),"Startup fixture failed: "+mode+" "+stdout.Result+stderr.Result);}
            finally{if(!process.HasExited){process.Kill();process.WaitForExit();}}
        }
        Console.WriteLine("PASS native startup modes: real lifetime, no Monitor flash, hidden sampler, Desktop lock, tray fallback, activation precedence and exit cleanup");
    }
    public static void Settings() {
        string directory=Directory.CreateTempSubdirectory("pulse-startup-mode-").FullName;
        try {
            string path=Path.Combine(directory,"settings.json");File.WriteAllText(path,"{\"startupMode\":\"unknown\"}");
            var store=new PreviewSettingsStore(path);Check(store.Load().StartupMode=="Monitor","Invalid startup mode did not fall back");
            var window=new MonitorWindow(new MonitorSource(true),start:false,store:store);window.Show();
            window.GetVisualDescendants().OfType<TabControl>().Single(x=>x.Name=="MainTabs").SelectedIndex=1;Dispatcher.UIThread.RunJobs();
            window.GetVisualDescendants().OfType<TabControl>().Single(x=>x.Name=="SettingsTabs").SelectedIndex=1;Dispatcher.UIThread.RunJobs();
            var choice=window.GetVisualDescendants().OfType<ComboBox>().Single(x=>x.Name=="StartupMode");choice.SelectedItem="Tray";window.Close();
            Check(new PreviewSettingsStore(path).Load().StartupMode=="Tray","Startup preference did not persist");
        }finally{Directory.Delete(directory,true);}
        Console.WriteLine("PASS startup preference: safe default and UI persistence");
    }
}

sealed class StartupFixture : Application {
    public StartupFixture(){}
    sealed class Source : IMonitorSource {
        public bool IsDemo=>false;public int Calls;
        public string[] Interfaces()=>[];
        public MonitorSnapshot Poll(string? name){Interlocked.Increment(ref Calls);return new("20%","4 GiB","—","—",true,true);}
    }
    [DllImport("user32.dll")] static extern bool IsWindowVisible(nint hwnd);
    public override void Initialize()=>Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted() {
        var lifetime=(IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        string directory=Directory.CreateTempSubdirectory("pulse-startup-child-").FullName;
        string mode=StartupModeTests.Mode;
        var store=new PreviewSettingsStore(Path.Combine(directory,"settings.json"));
        store.Save(new PreviewSettings{StartupMode=mode is "Activated"?"Desktop":mode is "Unavailable" or "TrayExit"?"Tray":mode,DesktopShortcutEnabled=false});
        var source=new Source();var monitor=new MonitorWindow(source,store:store);int opened=0,closed=0;
        monitor.Opened+=(_,_)=>opened++;monitor.Closed+=(_,_)=>closed++;
        var tray=new DesktopTray(monitor,true,()=>mode!="Unavailable");
        if(mode=="Activated")Dispatcher.UIThread.Post(monitor.RestoreMain);
        PulseApplication.ConfigureLifetime(lifetime,monitor,tray);
        lifetime.Exit+=(_,_)=>{Console.WriteLine("EXIT ownerClosed="+closed);Directory.Delete(directory,true);};
        lifetime.Startup+=(_,_)=>Dispatcher.UIThread.Post(async ()=>{
            try {
                var limit=DateTime.UtcNow.AddSeconds(5);while(source.Calls==0&&DateTime.UtcNow<limit)await Task.Delay(20);
                StartupModeTests.Check(source.Calls>0,"Hidden startup never started sampler");
                bool hidden=mode is "Desktop" or "Tray" or "TrayExit";
                StartupModeTests.Check(opened==(hidden?0:1)&&monitor.IsVisible!=hidden,"Monitor visibility/Opened count reveals a startup flash");
                StartupModeTests.Check(IsWindowVisible(monitor.TryGetPlatformHandle()!.Handle)!=hidden,"Native HWND visibility disagrees");
                if(mode=="Desktop")StartupModeTests.Check(monitor.FloatingMonitor?.IsVisible==true&&monitor.FloatingMonitor.IsLocked,"Desktop mode did not restore locked floating window");
                else StartupModeTests.Check(monitor.FloatingMonitor==null,"Unexpected floating window at startup");
                var sampling=monitor.Sampling;
                if(mode is not "TrayExit" and not "MonitorExit") {
                    monitor.RestoreMain();monitor.RestoreMain();await Task.Delay(30);
                    StartupModeTests.Check(ReferenceEquals(sampling,monitor.Sampling)&&monitor.IsVisible,"Restore replaced hidden sampler");
                    ((NativeMenuItem)tray.Menu.Items[^1]).Command!.Execute(null);
                }else lifetime.Shutdown();
            }catch(Exception e){Console.Error.WriteLine(e);lifetime.Shutdown(1);}
        });
        base.OnFrameworkInitializationCompleted();
    }
}
