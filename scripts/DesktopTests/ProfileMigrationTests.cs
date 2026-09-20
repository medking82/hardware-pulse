using System.Text;
using System.Text.Json;
using HardwarePulse.Desktop;

static class ProfileMigrationTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static string PhysicalDirectory(string path) {
        path=Path.GetFullPath(path);string current=Path.GetPathRoot(path)!;
        foreach(string part in path[current.Length..].Split([Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar],StringSplitOptions.RemoveEmptyEntries)) {
            string next=Path.Combine(current,part);current=new DirectoryInfo(next).ResolveLinkTarget(true)?.FullName??next;
        }
        return current;
    }
    public static void Run() {
        Check(WindowGeometry.FromLegacy(-800,100,[(new Avalonia.PixelRect(-1920,0,1920,1080),1.5)])==new Avalonia.PixelPoint(-1200,150),"Negative monitor WPF DIP conversion");
        Check(WindowGeometry.FromLegacy(2000,100,[(new Avalonia.PixelRect(0,0,1920,1080),1),(new Avalonia.PixelRect(1920,0,2560,1440),1.5)])==new Avalonia.PixelPoint(3000,150),"Target monitor DPI conversion");
        Check(WindowGeometry.FromLegacy(double.NaN,0,[])==null&&WindowGeometry.FromLegacy(null,10,[])==null,"Malformed legacy location accepted");
        Check(DesktopProfile.IsStableVersion("0.7.1+abc")&&!DesktopProfile.IsStableVersion("0.7.1-rc.1"),"Stable identity excludes prerelease");
        Check(DesktopProfile.CreateStore("","",true)==null,"Empty root cannot select working directory");
        string root=PhysicalDirectory(Directory.CreateTempSubdirectory("pulse-profile-").FullName);
        try {
            string local=Path.Combine(root,"local"),roaming=Path.Combine(root,"roaming"),folder=Path.Combine(local,"HardwarePulse");
            Directory.CreateDirectory(folder);
            string legacy=Path.Combine(folder,"widget-settings.json"),target=Path.Combine(folder,"shared-ui-settings.json"),withdrawn=Path.Combine(folder,"shared-settings.json");
            const string fixture="""
                {"width":620,"height":730,"language":"zh-CN","fontSize":14,"opacity":73,"background":"#153040","solid":false,"pin":true,"positionLocked":true,"details":true,
                 "cardOrder":["GPU","CPU"],"cardsVisible":{"CPU":false,"GPU":true},"readingColor":"#E0D0C0","unifiedReadingColors":true,
                 "desktopEnabled":true,"desktopLocked":false,"desktopWidth":690,"desktopHeight":520,"desktopLeft":-800,"desktopTop":50,
                 "desktopFontSize":22,"desktopSpacing":34,"desktopTextOpacity":70,"desktopBackgroundOpacity":25,"desktopOverlayOpacity":44,
                 "desktopAutoContrast":false,"desktopColor":"#123456","desktopLocalContrast":true,"desktopAlwaysOnTop":true,"desktopAppIconColors":false,"desktopColumns":2,
                 "desktopOrder":["GPU","quotaClaude1","CPU","quotaClaude0"],"desktopVisible":{"CPU":false,"fps":true,"quotaClaude0":false,"quotaClaude1":true},"desktopShortcutEnabled":false,"desktopShortcut":"Ctrl+Alt+F9",
                 "quotaClaude":true,"quotaCodex":false,"quotaAntigravity":false,"quotaFull":true,"names":{"CPU":"Desk CPU"},
                 "overlay":{"enabled":true,"fps":true,"detail":true,"cpu":false,"gpu":true,"memory":false,"fans":true,"storage":true,"position":"bottom-right","background":"#132435","opacity":66,"processName":"game"},
                 "autoUpdates":true,"autoDownload":false,"privateUnknown":"never-copy-this"}
                """;
            File.WriteAllText(legacy,fixture,new UTF8Encoding(true));byte[] original=File.ReadAllBytes(legacy);
            File.WriteAllText(withdrawn,"{\"width\":1900,\"floatingBackgroundOpacity\":100}");
            var store=DesktopProfile.CreateStore(roaming,local,true)!;var settings=store.Load();
            Check(store.Error==null&&store.LegacyImported&&!File.Exists(target),"Import must be read-only and accept BOM");
            Check(settings.Width==620&&settings.Height==730&&settings.Language=="zh-CN"&&settings.AppOpacity==73&&settings.BackgroundColor=="#153040"&&settings.Theme=="Dark","App appearance import");
            Check(settings.Topmost&&settings.LockPosition&&settings.Details&&settings.FontSize==14&&settings.HiddenCards.Contains("CPU")&&settings.CardOrder[0]=="GPU","App controls import");
            Check(settings.DesktopEnabled&&!settings.DesktopLocked&&settings.DesktopWidth==690&&settings.DesktopHeight==520&&settings.DesktopX==null,"Desktop geometry must not confuse WPF DIP with pixels");
            Check(settings.LegacyDesktopLeft==-800&&settings.LegacyDesktopTop==50,"Legacy DIP coordinates lost before first window restore");
            Check(settings.DesktopFontSize==22&&settings.DesktopSpacing==34&&settings.DesktopTextOpacity==70&&settings.DesktopBackgroundOpacity==25&&settings.DesktopOverlayOpacity==44,"Independent Desktop appearance");
            Check(!settings.DesktopAutoContrast&&settings.DesktopLocalContrast&&settings.DesktopTopmost&&!settings.DesktopAppIconColors&&settings.DesktopColor=="#123456"&&settings.DesktopColumns==2,"Desktop flags");
            Check(settings.DesktopOrder[0]=="GPU"&&!settings.DesktopVisible["CPU"]&&!settings.DesktopShortcutEnabled&&settings.DesktopShortcut=="Ctrl+Alt+F9","Desktop order/visibility/shortcut");
            Check(settings.DesktopOrder.Take(4).SequenceEqual(new[]{"GPU","quotaClaude1","CPU","quotaClaude0"})&&!settings.DesktopVisible["quotaClaude0"]&&settings.DesktopVisible["quotaClaude1"],"WPF quota window preferences remain active, not merely retained as raw JSON");
            var overlay=settings.GameOverlay;
            Check(overlay.Enabled&&overlay.Detailed&&!overlay.Cpu&&overlay.Gpu&&!overlay.Memory&&overlay.Fans&&overlay.Storage&&overlay.Position=="bottom-right"&&overlay.Opacity==66&&overlay.Background=="#132435","Overlay appearance and visibility");
            Check(settings.Claude&&!settings.Codex&&!settings.Antigravity&&settings.QuotaFull&&settings.Fps&&settings.FpsTarget=="game"&&settings.AutoUpdates&&!settings.AutoDownload,"Independent opt-ins");
            Check(store.Save(settings)&&File.ReadAllBytes(legacy).SequenceEqual(original),"Import save must preserve original bytes");
            using(var json=JsonDocument.Parse(File.ReadAllText(target))) {
                Check(!json.RootElement.TryGetProperty("privateUnknown",out _)&&json.RootElement.GetProperty("legacyDesktopLeft").GetDouble()==-800,"Bounded projection and retained legacy geometry");
                Check(json.RootElement.GetProperty("names").GetProperty("CPU").GetString()=="Desk CPU"&&settings.Names["CPU"]=="Desk CPU","Hardware names imported");
            }
            Check(File.ReadAllText(withdrawn).Contains("1900"),"Withdrawn shared profile changed");
            const string winner="{\"schema\":1,\"width\":930,\"future\":{\"keep\":7}}";
            File.Delete(target);store.Load();File.WriteAllText(target,winner);
            Check(!store.Save(settings)&&File.ReadAllText(target)==winner,"Concurrent create must preserve winner");
            var current=store.Load();Check(!store.LegacyImported&&current.Width==930&&store.Error==null&&store.Save(current),"Reload uses winner and recovers save");
            Check(File.ReadAllText(target).Contains("\"keep\":7"),"Unknown target fields lost");
            foreach(string bad in new[]{"{broken","[]",new string(' ',65537),"{\"schema\":2}"}) {
                File.WriteAllText(target,bad);var invalid=new PreviewSettingsStore(target,legacy);var value=invalid.Load();
                Check(invalid.Error!=null&&!invalid.LegacyImported&&!invalid.Save(value)&&File.ReadAllText(target)==bad,"Invalid target cannot fall back or be overwritten");
                File.Delete(target);
                if(bad.Contains("schema"))continue;
                File.WriteAllText(legacy,bad);invalid=new(target,legacy);value=invalid.Load();
                Check(invalid.Error!=null&&!invalid.Save(value)&&!File.Exists(target)&&File.ReadAllText(legacy)==bad,"Malformed legacy source must be preserved");
            }
            File.WriteAllText(legacy,"{}");store=new(target,legacy);settings=store.Load();
            Check(!settings.Codex&&!settings.Claude&&!settings.Antigravity&&!settings.Fps&&!settings.AutoUpdates&&!settings.AutoDownload,"Missing fields must not opt in");
            Directory.CreateDirectory(target);store.Load();Check(store.Error!=null&&!store.Save(settings),"Directory target cannot fall back");Directory.Delete(target);
            var preview=DesktopProfile.CreateStore(roaming,local,false)!;preview.Load();Check(!preview.LegacyImported&&preview.Save(new())&&!File.Exists(target),"Preview remains separate");
            string link=Path.Combine(root,"linked-profile");
            if(OperatingSystem.IsWindows()) {
                var info=new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"cmd.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
                info.ArgumentList.Add("/d");info.ArgumentList.Add("/c");info.ArgumentList.Add("mklink");info.ArgumentList.Add("/J");info.ArgumentList.Add(link);info.ArgumentList.Add(folder);
                using var process=System.Diagnostics.Process.Start(info)!;process.WaitForExit();
                Check(process.ExitCode==0,"Could not create isolated junction fixture");
            }else Directory.CreateSymbolicLink(link,folder);
            try {
                var linked=new PreviewSettingsStore(target,Path.Combine(link,"widget-settings.json"));var value=linked.Load();
                Check(linked.Error!=null&&!linked.Save(value)&&!File.Exists(target),"Linked import source accepted");
                linked=new(Path.Combine(link,"shared-ui-settings.json"),legacy);value=linked.Load();
                Check(!linked.Save(value)&&!File.Exists(target),"Linked destination accepted");
            }finally{Directory.Delete(link);}
            bool rejected=false;try{_=new PreviewSettingsStore(legacy,legacy);}catch(ArgumentException){rejected=true;}Check(rejected,"Source and destination must differ");
            Check(!Directory.EnumerateFiles(root,".settings-*.tmp",SearchOption.AllDirectories).Any(),"Temporary settings leaked");
        }finally{Directory.Delete(root,true);}
        Console.WriteLine("PASS rebuilt profile: distinct stable identity, WPF projection, independent preferences, source preservation, malformed input and concurrent-create recovery");
    }
}
