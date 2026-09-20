using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class LocalizationTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Until(Func<bool> done){var end=DateTime.UtcNow.AddSeconds(5);while(!done()&&DateTime.UtcNow<end){using var slice=new CancellationTokenSource(TimeSpan.FromMilliseconds(20));Dispatcher.UIThread.MainLoop(slice.Token);}Check(done(),"Localization operation timed out");}
    public static void Run(string? output,bool native=false) {
        void Phase(string step){if(native)Console.WriteLine("NATIVE_SESSION_PHASE "+step);}
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
        var coverage=FontCoverage.Capture();
        Check(coverage.Select(x=>x.Language).SequenceEqual(new[]{"en","zh-CN","zh-TW"})&&coverage.All(x=>x.CodePoints>0),"Font coverage observes all catalogs");
        Check(coverage.All(x=>x.Missing.Length<=x.CodePoints&&x.Missing.Distinct().Count()==x.Missing.Length),"Font coverage reports unique missing code points");
        Console.WriteLine((native?"NATIVE_SESSION_FONT_COVERAGE ":"HEADLESS_FONT_COVERAGE ")+System.Text.Json.JsonSerializer.Serialize(coverage));
        Check(coverage.All(x=>x.Missing.Length==0),"UI catalogs must have complete glyph coverage");
        foreach(var family in new[]{DesktopFonts.Simplified,DesktopFonts.Traditional}) {
            Check(Avalonia.Media.FontManager.Current.TryGetGlyphTypeface(new Avalonia.Media.Typeface(family),out var glyphs),"Embedded CJK family resolves");
            Check(glyphs!=null&&glyphs.FamilyName.StartsWith("Noto Sans CJK",StringComparison.Ordinal),"Embedded font must resolve without a system substitute");
        }
        string directory=Directory.CreateTempSubdirectory("pulse-language-").FullName;
        try {
            var store=new PreviewSettingsStore(Path.Combine(directory,"settings.json"));store.Save(new PreviewSettings{Language="en",Network="Device / eth0",Theme="Dark"});
            var source=new MonitorSource(true);var window=new MonitorWindow(source,store:store);window.Show();
            window.Present(source.Poll(null));var sampling=window.Sampling;
            var main=window.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="OpenSettings");main.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));Dispatcher.UIThread.RunJobs();
            var tabs=window.GetVisualDescendants().OfType<TabControl>().Single(x=>x.Name=="SettingsTabs");tabs.SelectedIndex=0;Dispatcher.UIThread.RunJobs();
            var language=window.GetVisualDescendants().OfType<ComboBox>().Single(x=>x.Name=="PreviewLanguage");language.SelectedIndex=2;Dispatcher.UIThread.RunJobs();
            Check(window.Title=="Pulse · 桌面预览版"&&window.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Name=="SettingsTitle"&&x.Text=="设置"),"Title and Settings navigation change immediately");
            Check(window.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="语言"),"General labels translated");
            Check(ReferenceEquals(sampling,window.Sampling)&&window.Language.Choice=="zh-CN","Switch does not restart sampling");
            using(var tray=new DesktopTray(window)){Check(((NativeMenuItem)tray.Menu.Items[0]).Header=="打开 Pulse","Tray uses window language");window.Language.Select("en");Check(((NativeMenuItem)tray.Menu.Items[0]).Header=="Open Pulse","Tray updates live");window.Language.Select("zh-CN");}
            language.SelectedIndex=3;Dispatcher.UIThread.RunJobs();
            Check(window.Title=="Pulse · 桌面預覽版"&&window.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="語言"),"Traditional choice applies immediately");
            foreach(int tab in new[]{1,0}) {
                if(tab==0)window.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="Back").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));window.Width=360;window.Height=800;Dispatcher.UIThread.RunJobs();
                if(native) {
                    double Widest()=>window.GetVisualDescendants().OfType<TextBlock>().Where(x=>x.IsEffectivelyVisible).Select(x=>x.Bounds.Width).DefaultIfEmpty().Max();
                    Console.WriteLine($"NATIVE_RESIZE before: requested={window.Width} client={window.ClientSize.Width} widest={Widest()}");
                    // Native window managers acknowledge resize asynchronously. Test the
                    // resulting layout, not the previous frame still using the old width.
                    Until(()=>Math.Abs(window.ClientSize.Width-360)<.5);
                    window.UpdateLayout();
                    Console.WriteLine($"NATIVE_RESIZE settled: requested={window.Width} client={window.ClientSize.Width} widest={Widest()}");
                }
                foreach(var text in window.GetVisualDescendants().OfType<TextBlock>().Where(x=>x.IsEffectivelyVisible))Check(text.Bounds.Width<=360,$"Chinese text exceeds window: text={text.Text}, width={text.Bounds.Width}, client={window.ClientSize.Width}, requested={window.Width}");
                if(output!=null){using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(output,$"traditional-{tab}.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
            }
            Check(window.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="24.0%"),"Numeric snapshot survives language switch");
            Phase("close localized monitor");window.Close();Phase("await sampling stop");Until(()=>window.Sampling.IsCompleted);
            Phase("load saved settings");var saved=store.Load();Check(saved.Language=="zh-TW"&&saved.Network=="Device / eth0"&&saved.Theme=="Dark","Language persists without modifying other choices");
            Phase("construct reopened monitor");var reopened=new MonitorWindow(source,start:false,store:store);Check(reopened.Title=="Pulse · 桌面預覽版","Saved language restored");
            Phase("show reopened monitor");reopened.Show();Phase("close reopened monitor");reopened.Close();
            var quotaLanguage=new UiLanguage("en");int reads=0;
            using var quota=new QuotaPanel(true,_=>{Interlocked.Increment(ref reads);return new HardwarePulse.QuotaReading{Provider="Codex",Status="Live",Observed=DateTimeOffset.UtcNow,Windows=new(){new(){Label="Weekly",Remaining=45.5}}};},language:quotaLanguage);
            Phase("show quota host");var host=new Window{Content=quota,Width=360,Height=500};host.Show();Phase("enable synthetic quota");quota.QuotaEnabled=true;
            bool Has(string text)=>host.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text==text);
            Phase("await synthetic quota");Until(()=>Has("45.5% left"));Phase("switch quota language");quotaLanguage.Select("zh-CN");
            Check(Has("每周")&&Has("剩余 45.5%")&&reads==1,"Quota switches from cached data without reading credentials again");
            if(output!=null){Dispatcher.UIThread.RunJobs();using var frame=host.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"chinese-quota.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
            quotaLanguage.Select("zh-TW");Check(Has("每週")&&Has("剩餘 45.5%")&&reads==1,"Traditional quota retains cached data");
            quotaLanguage.Select("en");Check(Has("Weekly")&&Has("45.5% left")&&reads==1,"Quota language round trip retains values");Phase("close quota host");host.Close();Phase("session complete");
        } finally {Directory.Delete(directory,true);}
        Console.WriteLine("PASS shared localization: system fallback, instant UI/tray switch, stable sampling, narrow layouts and persistence");
    }
}
