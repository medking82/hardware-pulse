using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class LocalizationTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Until(Func<bool> done){var end=DateTime.UtcNow.AddSeconds(5);while(!done()&&DateTime.UtcNow<end){using var slice=new CancellationTokenSource(TimeSpan.FromMilliseconds(20));Dispatcher.UIThread.MainLoop(slice.Token);}Check(done(),"Localization operation timed out");}
    public static void Run(string? output) {
        Check(new UiLanguage("auto","zh-CN").T("Memory")=="内存","System Chinese selection");
        Check(new UiLanguage("auto","de-DE").T("Memory")=="Memory","Unsupported system language falls back to English");
        Check(new UiLanguage("en","zh-CN").T("Memory")=="Memory","Explicit choice overrides system");
        Check(new UiLanguage("zh-CN").T("Device / eth0")=="Device / eth0","Device names unchanged");
        foreach(string locale in new[]{"zh-TW","zh-HK","zh-MO","zh-Hant","zh-Hant-CN","zh_CHT","ZH_tw"})
            Check(new UiLanguage("auto",locale).T("Memory")=="記憶體","Traditional system locale: "+locale);
        foreach(string locale in new[]{"zh-CN","zh-SG","zh","zh-Hans","zh-Hans-HK","zh-CHS"})
            Check(new UiLanguage("auto",locale).T("Memory")=="内存","Simplified system locale: "+locale);
        Check(new UiLanguage("auto","zhx").T("Memory")=="Memory","Do not match unrelated language prefixes");
        Check(new UiLanguage("zh-TW","en-US").T("Memory")=="記憶體","Explicit Traditional overrides system");
        string[] Catalog(string name){using var stream=typeof(UiLanguage).Assembly.GetManifestResourceStream("Pulse.Desktop."+name)!;using var reader=new StreamReader(stream);return reader.ReadToEnd().Split('\n',StringSplitOptions.RemoveEmptyEntries).Select(x=>x.TrimEnd('\r')).ToArray();}
        var simplified=Catalog("Chinese.txt");var traditional=Catalog("ChineseTraditional.txt");
        Check(simplified.Select(x=>x.Split('|')[0]).Order().SequenceEqual(traditional.Select(x=>x.Split('|')[0]).Order()),"Both Chinese catalogs cover identical keys");
        foreach(string line in simplified.Concat(traditional)) {
            var parts=line.Split('|');Check(parts.Length==2&&!string.IsNullOrWhiteSpace(parts[1]),"Complete translation");
            string[] Tokens(string text)=>System.Text.RegularExpressions.Regex.Matches(text,@"\{\d+\}").Select(x=>x.Value).Order().ToArray();
            Check(Tokens(parts[0]).SequenceEqual(Tokens(parts[1])),"Translation preserves format placeholders");
        }
        string directory=Directory.CreateTempSubdirectory("pulse-language-").FullName;
        try {
            var store=new PreviewSettingsStore(Path.Combine(directory,"settings.json"));store.Save(new PreviewSettings{Language="en",Network="Device / eth0",Theme="Dark"});
            var source=new MonitorSource(true);var window=new MonitorWindow(source,store:store);window.Show();
            window.Present(source.Poll(null));var sampling=window.Sampling;
            var main=window.GetVisualDescendants().OfType<TabControl>().Single(x=>x.Name=="MainTabs");main.SelectedIndex=1;Dispatcher.UIThread.RunJobs();
            var tabs=window.GetVisualDescendants().OfType<TabControl>().Single(x=>x.Name=="SettingsTabs");tabs.SelectedIndex=1;Dispatcher.UIThread.RunJobs();
            var language=window.GetVisualDescendants().OfType<ComboBox>().Single(x=>x.Name=="PreviewLanguage");language.SelectedIndex=2;Dispatcher.UIThread.RunJobs();
            Check(window.Title=="Pulse · 桌面预览版"&&((TabItem)main.Items[1]!).Header?.ToString()=="设置","Title and tabs change immediately");
            Check(window.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="主题"),"Appearance labels translated");
            Check(ReferenceEquals(sampling,window.Sampling)&&window.Language.Choice=="zh-CN","Switch does not restart sampling");
            using(var tray=new DesktopTray(window)){Check(((NativeMenuItem)tray.Menu.Items[0]).Header=="打开 Pulse","Tray uses window language");window.Language.Select("en");Check(((NativeMenuItem)tray.Menu.Items[0]).Header=="Open Pulse","Tray updates live");window.Language.Select("zh-CN");}
            language.SelectedIndex=3;Dispatcher.UIThread.RunJobs();
            Check(window.Title=="Pulse · 桌面預覽版"&&window.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="佈景主題"),"Traditional choice applies immediately");
            foreach(int tab in new[]{1,0}) {
                main.SelectedIndex=tab;window.Width=360;window.Height=800;Dispatcher.UIThread.RunJobs();
                foreach(var text in window.GetVisualDescendants().OfType<TextBlock>())Check(text.Bounds.Width<=360,"Chinese text exceeds window");
                if(output!=null){using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(output,$"traditional-{tab}.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
            }
            Check(window.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="24.0%"),"Numeric snapshot survives language switch");
            window.Close();Until(()=>window.Sampling.IsCompleted);var saved=store.Load();Check(saved.Language=="zh-TW"&&saved.Network=="Device / eth0"&&saved.Theme=="Dark","Language persists without modifying other choices");
            var reopened=new MonitorWindow(source,start:false,store:store);Check(reopened.Title=="Pulse · 桌面預覽版","Saved language restored");reopened.Show();reopened.Close();
            var quotaLanguage=new UiLanguage("en");int reads=0;
            using var quota=new CodexQuotaPanel(true,_=>{Interlocked.Increment(ref reads);return new HardwarePulse.QuotaReading{Provider="Codex",Status="Live",Windows=new(){new(){Label="Weekly",Remaining=45.5}}};},language:quotaLanguage);
            var host=new Window{Content=quota,Width=360,Height=500};host.Show();quota.QuotaEnabled=true;
            bool Has(string text)=>host.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text==text);
            Until(()=>Has("45.5% left"));quotaLanguage.Select("zh-CN");
            Check(Has("每周")&&Has("剩余 45.5%")&&reads==1,"Quota switches from cached data without reading credentials again");
            if(output!=null){Dispatcher.UIThread.RunJobs();using var frame=host.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"chinese-quota.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
            quotaLanguage.Select("zh-TW");Check(Has("每週")&&Has("剩餘 45.5%")&&reads==1,"Traditional quota retains cached data");
            quotaLanguage.Select("en");Check(Has("Weekly")&&Has("45.5% left")&&reads==1,"Quota language round trip retains values");host.Close();
        } finally {Directory.Delete(directory,true);}
        Console.WriteLine("PASS shared localization: system fallback, instant UI/tray switch, stable sampling, narrow layouts and persistence");
    }
}
