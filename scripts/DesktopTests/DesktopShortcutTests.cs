using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class DesktopShortcutTests {
    sealed class Registration : IDesktopShortcut {
        public string? Current;public int Disposals;public bool Accept=true;
        public bool Set(string text){if(!Accept)return false;Current=text;return true;}
        public void Clear()=>Current=null;
        public void Dispose(){Clear();Disposals++;}
    }
    [DllImport("user32.dll")] static extern nint SendMessage(nint hwnd,uint message,nint w,nint l);
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
    public static void Run(string? output=null) {
        foreach(var invalid in new[]{"Alt+F10","Ctrl+F10","Ctrl+Alt+F12","Ctrl+Alt+Space","Win+Ctrl+A","nonsense"})
            Check(!WindowsDesktopShortcut.TryParse(invalid,out _,out _,out _),"Invalid shortcut accepted: "+invalid);
        Check(WindowsDesktopShortcut.TryParse("Ctrl+Alt+F10",out _,out uint mods,out uint key)&&mods==3&&key==0x79,"WPF shortcut policy changed");
        var saved=new PreviewSettings();var fake=new Registration();int saves=0;
        var view=new DesktopShortcutSettings(new UiLanguage("en"),saved,()=>saves++,true);
        var window=new Window{Content=view,Width=360,Height=250};window.Show();view.Attach(fake);
        var choose=view.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="DesktopShortcut");
        var enabled=view.GetVisualDescendants().OfType<CheckBox>().Single();
        var status=view.GetVisualDescendants().OfType<TextBlock>().Single(x=>x.Name=="DesktopShortcutStatus");
        void Capture(Key k){choose.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));choose.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=k,KeyModifiers=KeyModifiers.Control|KeyModifiers.Alt});}
        fake.Accept=false;Capture(Key.F9);
        Check(saved.DesktopShortcut=="Ctrl+Alt+F10"&&fake.Current=="Ctrl+Alt+F10"&&saves==0&&status.Text!.Contains("unavailable"),"Conflict lost previous preference/registration");
        fake.Accept=true;Capture(Key.F9);
        Check(saved.DesktopShortcut.Contains("F9")&&fake.Current==saved.DesktopShortcut&&saves>0,"Shortcut capture did not save");
        Capture(Key.Escape);Check(saved.DesktopShortcut.Contains("F9"),"Escape changed shortcut");
        enabled.IsChecked=false;Check(fake.Current==null&&!saved.DesktopShortcutEnabled,"Disable did not release registration");
        enabled.IsChecked=true;Check(fake.Current==saved.DesktopShortcut,"Enable failed");
        if(output!=null){Dispatcher.UIThread.RunJobs();using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"desktop-shortcut-360.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
        view.Dispose();view.Dispose();window.Close();Check(fake.Disposals==1,"Shortcut disposed more than once");
        string directory=Directory.CreateTempSubdirectory("pulse-shortcut-").FullName;
        try{string path=Path.Combine(directory,"settings.json");var store=new PreviewSettingsStore(path);Check(store.Save(saved),"Shortcut save failed");var loaded=store.Load();Check(loaded.DesktopShortcut==saved.DesktopShortcut&&loaded.DesktopShortcutEnabled,"Shortcut roundtrip failed");File.WriteAllText(path,"{\"desktopShortcut\":\"Ctrl+F12\",\"desktopShortcutEnabled\":false}");loaded=store.Load();Check(loaded.DesktopShortcut=="Ctrl+Alt+F10"&&!loaded.DesktopShortcutEnabled,"Invalid saved shortcut not normalized");}
        finally{Directory.Delete(directory,true);}
        var monitor=new MonitorWindow(new MonitorSource(true),start:false);monitor.Show();monitor.ToggleFloatingMonitor();
        var floating=monitor.FloatingMonitor;Check(floating?.IsVisible==true,"Shortcut failed to open Desktop");
        monitor.ToggleFloatingMonitor();Check(!floating!.IsVisible,"Shortcut failed to hide Desktop");
        monitor.ToggleFloatingMonitor();Check(ReferenceEquals(floating,monitor.FloatingMonitor)&&floating.IsVisible,"Shortcut created duplicate Desktop");monitor.Close();
        Console.WriteLine("PASS Desktop shortcut: capture, conflict preservation, disable, persistence, same-window toggle and disposal");
    }
    public static void Native() {
        if(!OperatingSystem.IsWindows())return;
        var a=new Window();var b=new Window();a.Show();b.Show();int calls=0;
        using(var first=new WindowsDesktopShortcut(a,()=>calls++))using(var second=new WindowsDesktopShortcut(b,()=>{})) {
            string? available=Enumerable.Range(1,11).Select(x=>$"Ctrl+Alt+Shift+F{x}").FirstOrDefault(first.Set);
            Check(available!=null,"No free test shortcut; external registrations preserved");
            Check(!second.Set(available!),"Windows allowed a conflicting shortcut");
            string? occupied=Enumerable.Range(1,11).Select(x=>$"Ctrl+Alt+Shift+F{x}").Where(x=>x!=available).FirstOrDefault(second.Set);
            Check(occupied!=null,"No second free test shortcut; external registrations preserved");
            Check(!first.Set(occupied!),"Conflicting replacement accepted");
            var handle=a.TryGetPlatformHandle()!.Handle;
            SendMessage(handle,0x312,0x504,0);Check(calls==1,"WM_HOTKEY did not reach owner");
            Check(!first.Set("Ctrl+F12"),"Invalid replacement accepted");
            SendMessage(handle,0x312,0x504,0);Check(calls==2,"Invalid replacement removed old shortcut");
            first.Clear();Check(second.Set(available!),"Clear did not release OS registration");
            SendMessage(handle,0x312,0x504,0);Check(calls==2,"Cleared shortcut still active");
            second.Dispose();Check(first.Set(available!),"Dispose did not release OS registration");
        }
        a.Close();b.Close();Console.WriteLine("PASS Windows native shortcut: OS conflict, hook dispatch, replacement safety, clear and disposal");
    }
}
