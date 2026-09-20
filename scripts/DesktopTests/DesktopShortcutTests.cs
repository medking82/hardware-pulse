using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse;
using HardwarePulse.Desktop;

static class DesktopShortcutTests {
    [DllImport("user32.dll")] static extern bool PostMessage(nint window,uint message,nint w,nint l);
    [DllImport("user32.dll")] static extern nint GetDesktopWindow();
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Pump(){using var slice=new CancellationTokenSource(150);Dispatcher.UIThread.MainLoop(slice.Token);}
    public static void Settings() {
        foreach(string value in new[]{"Ctrl+Alt+F10","Ctrl+Shift+A","Alt+Shift+9","Ctrl+Alt+Shift+F11"})Check(DesktopShortcutPanel.TryGesture(value,out _,out _,out _),"Valid original gesture rejected: "+value);
        foreach(string value in new[]{"Ctrl+A","F10","Ctrl+Alt+F12","Ctrl+Alt+Delete","Ctrl+Alt+Meta+A","invalid"})Check(!DesktopShortcutPanel.TryGesture(value,out _,out _,out _),"Unsafe/reserved gesture accepted: "+value);
        string directory=Directory.CreateTempSubdirectory("pulse-shortcut-").FullName;
        try {
            var store=new PreviewSettingsStore(Path.Combine(directory,"settings.json"));var settings=new PreviewSettings{DesktopShortcut="Alt+Shift+9",DesktopShortcutEnabled=false};Check(store.Save(settings),"Save shortcut");var loaded=store.Load();Check(loaded.DesktopShortcut==settings.DesktopShortcut&&!loaded.DesktopShortcutEnabled,"Shortcut roundtrip");
            File.WriteAllText(Path.Combine(directory,"settings.json"),"{\"desktopShortcut\":\"Ctrl+F12\",\"desktopShortcutEnabled\":false}");Check(store.Load().DesktopShortcut=="Ctrl+Alt+F10","Invalid gesture must normalize to original default");
            var owner=new Window();using var panel=new DesktopShortcutPanel(owner,settings,new UiLanguage("en"),()=>{},()=>throw new Exception("Isolated shortcut executed"),false);owner.Content=panel;owner.Show();
            Check(!panel.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="DesktopShortcut").IsEnabled,"Unsupported session offered global registration");owner.Close();
        }finally{Directory.Delete(directory,true);}
    }
    public static void Native() {
        if(!OperatingSystem.IsWindows())return;
        bool rejected=false;try{using var foreign=new WindowsDesktopHotkey(GetDesktopWindow());}catch(InvalidOperationException){rejected=true;}Check(rejected,"Foreign HWND accepted");
        var owner=new Window{Width=380,Height=220};var competitor=new Window{Width=200,Height=100};
        var settings=new PreviewSettings{DesktopShortcut="Ctrl+Alt+Shift+F11"};int toggles=0;
        using var panel=new DesktopShortcutPanel(owner,settings,new UiLanguage("en"),()=>{},()=>toggles++,true);owner.Content=panel;
        try {
            owner.Show();competitor.Show();Pump();
            nint hwnd=owner.TryGetPlatformHandle()!.Handle;
            using var other=new WindowsDesktopHotkey(competitor.TryGetPlatformHandle()!.Handle);
            Check(!other.Set(7,122),"Registered shortcut was not exclusive");
            var status=panel.GetVisualDescendants().OfType<TextBlock>().Single(x=>x.Name=="DesktopShortcutStatus");Check(status.Text!.StartsWith("Shortcut ready"),"Native shortcut did not register");
            Check(PostMessage(hwnd,0x312,0x504,(nint)((122<<16)|7)),"Could not post owned shortcut fixture");Pump();Check(toggles==1,"Registered WM_HOTKEY did not reach shared action");
            Check(other.Set(7,120),"Conflict fixture could not reserve Ctrl+Alt+Shift+F9");
            var choose=panel.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="DesktopShortcut");choose.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            choose.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Key.F9,KeyModifiers=KeyModifiers.Control|KeyModifiers.Alt|KeyModifiers.Shift});
            Check(settings.DesktopShortcut=="Ctrl+Alt+Shift+F11"&&status.Text!.StartsWith("Shortcut unavailable"),"Conflict replaced the saved working shortcut");
            choose.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Key.Escape});
            PostMessage(hwnd,0x312,0x504,(nint)((122<<16)|7));Pump();Check(toggles==2,"Conflict cancelled the previous registration");
            var enabled=panel.GetVisualDescendants().OfType<CheckBox>().Single();enabled.IsChecked=false;
            PostMessage(hwnd,0x312,0x504,(nint)((122<<16)|7));Pump();Check(toggles==2&&other.Set(7,122),"Disable did not release registration/reject stale message");
            other.Clear();enabled.IsChecked=true;Pump();owner.Close();Check(other.Set(7,122),"Close leaked the global shortcut");
        }finally{owner.Close();competitor.Close();}
        Console.WriteLine("PASS Windows Desktop shortcut: owned HWND, registration, message routing, conflict rollback, Esc, disable, stale message and close cleanup");
    }
}
