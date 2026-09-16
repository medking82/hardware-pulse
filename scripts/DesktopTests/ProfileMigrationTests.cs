using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class ProfileMigrationTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    // macOS temporary paths may traverse /var -> /private/var. The normal-input
    // fixture must be regular; production deliberately rejects linked ancestors.
    static string PhysicalDirectory(string path) {
        path=Path.GetFullPath(path);
        string current=Path.GetPathRoot(path)!;
        foreach(string part in path[current.Length..].Split([Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar],StringSplitOptions.RemoveEmptyEntries)) {
            string next=Path.Combine(current,part);
            current=new DirectoryInfo(next).ResolveLinkTarget(true)?.FullName??next;
        }
        return current;
    }
    public static void Run() {
        foreach(string version in new[]{"0.7.0","0.7.0+abc-123"})Check(DesktopProfile.IsStableVersion(version),"Stable version rejected");
        foreach(string version in new[]{"","0.7.0-preview.3","0.7.0.0","0.7","0.7.0+","999999999999.0.0"})Check(!DesktopProfile.IsStableVersion(version),"Unstable version admitted");
        Check(DesktopProfile.CreateStore("","",true)==null,"Empty root must not use current directory");
        string directory=PhysicalDirectory(Directory.CreateTempSubdirectory("pulse-profile-migration-").FullName);
        try {
            string local=Path.Combine(directory,"local"),roaming=Path.Combine(directory,"roaming");
            string target=Path.Combine(local,"HardwarePulse","shared-settings.json"),legacy=Path.Combine(local,"HardwarePulse","widget-settings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(legacy)!);
            const string fixture="""
                {"width":720,"height":610,"language":"zh-CN","desktopEnabled":true,
                "desktopWidth":690,"desktopHeight":520,"desktopLeft":-800,"desktopTop":50,
                "desktopFontSize":22,"desktopSpacing":40,"desktopTextOpacity":70,"desktopBackgroundOpacity":25,
                "desktopAutoContrast":false,"desktopColor":"#123456","desktopLocalContrast":true,
                "desktopAlwaysOnTop":true,"desktopAppIconColors":false,"desktopColumns":2,
                "quotaClaude":true,"quotaCodex":false,"quotaAntigravity":false,
                "desktopShortcutEnabled":false,"desktopShortcut":"Ctrl+Alt+F9",
                "overlay":{"enabled":true,"fps":true,"processName":"game.exe"},
                "autoUpdates":true,"autoDownload":false,"privateUnknown":"never-copy-this"}
                """;
            File.WriteAllText(legacy,fixture,new UTF8Encoding(true));
            byte[] original=File.ReadAllBytes(legacy);
            var store=DesktopProfile.CreateStore(roaming,local,true)!;var settings=store.Load();
            Check(store.Error==null&&store.LegacyImported&&!File.Exists(target),"Read-only BOM legacy import");
            Check(settings.Width==720&&settings.Height==610&&settings.Language=="zh-CN"&&settings.StartupMode=="Desktop","Common preferences");
            Check(settings.FloatingWidth==690&&settings.FloatingHeight==520&&!settings.FloatingPositionSet,"Dimensions without incompatible position");
            Check(settings.FloatingFontSize==22&&settings.FloatingRowSpacing==24&&settings.FloatingTextOpacity==70&&settings.FloatingBackgroundOpacity==25,"Bounded appearance");
            Check(settings.FloatingTextColor=="#123456"&&settings.FloatingLocalContrast&&settings.FloatingTopmost&&!settings.FloatingIconsFollowApp,"Desktop flags");
            Check(!settings.Codex&&settings.Claude&&!settings.Antigravity&&settings.Fps&&settings.FpsTarget=="game.exe","Independent opt-ins");
            Check(settings.AutoUpdates&&!settings.AutoDownload&&!settings.DesktopShortcutEnabled&&settings.DesktopShortcut=="Ctrl+Alt+F9","Update and shortcut preferences");
            Check(store.Save(settings)&&File.ReadAllBytes(legacy).SequenceEqual(original),"Save preserves WPF bytes");
            Check(!File.ReadAllText(target).Contains("never-copy-this"),"Unknown legacy data leaked");

            const string winner="{\"schema\":1,\"width\":930,\"future\":{\"keep\":7}}";
            File.Delete(target);store.Load();File.WriteAllText(target,winner);
            Check(!store.Save(settings)&&File.ReadAllText(target)==winner,"Concurrent create must preserve winner");
            var current=store.Load();Check(!store.LegacyImported&&current.Width==930&&store.Error==null,"Reload uses winner");
            Check(store.Save(current),"Reload allows normal save");
            using(var doc=JsonDocument.Parse(File.ReadAllText(target)))Check(doc.RootElement.GetProperty("future").GetProperty("keep").GetInt32()==7,"Stable unknown fields preserved");
            foreach(string bad in new[]{"{broken","[]",new string(' ',65537)}) {
                File.WriteAllText(target,bad);var invalid=new PreviewSettingsStore(target,legacy);var value=invalid.Load();
                Check(invalid.Error!=null&&!invalid.LegacyImported&&!invalid.Save(value)&&File.ReadAllText(target)==bad,"Invalid stable must not fall back");
                File.Delete(target);File.WriteAllText(legacy,bad);invalid=new(target,legacy);value=invalid.Load();
                Check(invalid.Error!=null&&!invalid.Save(value)&&!File.Exists(target)&&File.ReadAllText(legacy)==bad,"Invalid source preserved");
            }
            File.WriteAllText(legacy,"{}");store=new(target,legacy);settings=store.Load();
            Check(!settings.Codex&&!settings.Claude&&!settings.Antigravity&&!settings.Fps&&!settings.AutoUpdates&&!settings.AutoDownload,"Missing fields cannot enable optional features");
            Directory.CreateDirectory(target);store.Load();Check(store.Error!=null&&!store.Save(settings),"Directory target cannot fall back");Directory.Delete(target);
            var preview=DesktopProfile.CreateStore(roaming,local,false)!;preview.Load();Check(!preview.LegacyImported&&preview.Save(new()),"Preview remains independent");
            Check(File.Exists(Path.Combine(roaming,"HardwarePulse.Preview","settings.json"))&&!File.Exists(target),"Preview path unchanged");
            bool rejected=false;try{_ =new PreviewSettingsStore(legacy,OperatingSystem.IsWindows()?legacy.ToUpperInvariant():legacy);}catch(ArgumentException){rejected=true;}
            Check(rejected,"Source and target must differ");
            string link=Path.Combine(directory,"linked-profile");
            if(OperatingSystem.IsWindows()) {
                // A directory junction exercises the ancestor reparse check without elevation.
                var info=new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"cmd.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
                info.Arguments=$"/d /c mklink /J \"{link}\" \"{Path.GetDirectoryName(legacy)}\"";
                using var process=System.Diagnostics.Process.Start(info)!;
                string stdout=process.StandardOutput.ReadToEnd(),stderr=process.StandardError.ReadToEnd();process.WaitForExit();
                Check(process.ExitCode==0,"Junction fixture creation failed: "+stdout+stderr);
            }else Directory.CreateSymbolicLink(link,Path.GetDirectoryName(legacy)!);
            try {
                var linked=new PreviewSettingsStore(target,Path.Combine(link,"widget-settings.json"));var linkedValue=linked.Load();
                Check(linked.Error!=null&&!linked.Save(linkedValue)&&!File.Exists(target),"Linked legacy source must be refused");
                var physical=new PreviewSettingsStore(target,Path.Combine(PhysicalDirectory(link),"widget-settings.json"));
                physical.Load();Check(physical.Error==null&&physical.LegacyImported&&!File.Exists(target),"Regular fixture path must resolve linked temporary ancestors");
            }finally{Directory.Delete(link);}
            File.WriteAllText(legacy,"{\"language\":\"en\"}");
            var window=new MonitorWindow(new MonitorSource(true),start:false,store:new(target,legacy));
            try {
                window.Show();
                window.GetVisualDescendants().OfType<TabControl>().Single(x=>x.Name=="MainTabs").SelectedIndex=1;
                Dispatcher.UIThread.RunJobs();
                var notice=window.GetVisualDescendants().OfType<TextBlock>().Single(x=>x.Name=="ImportedSettingsNotice");
                Check(notice.Text!.StartsWith("Compatible preferences"),"Import notice missing");
                foreach(string locale in new[]{"zh-CN","zh-TW"}) {
                    window.Language.Select(locale);Dispatcher.UIThread.RunJobs();
                    Check(notice.Text==window.Language.T("Compatible preferences were imported. Review layout and window placement. Your original settings are preserved.")&&!notice.Text!.StartsWith("Compatible"),"Notice translation");
                }
            }finally{window.Close();}
            Check(File.ReadAllText(legacy)=="{\"language\":\"en\"}","UI changed source profile");
            Check(!Directory.EnumerateFiles(directory,".settings-*.tmp",SearchOption.AllDirectories).Any(),"Temporary files leaked");
        }finally{Directory.Delete(directory,true);}
        Console.WriteLine("PASS stable profile selection, bounded legacy import, source preservation and concurrent-create recovery");
    }
}
