using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace HardwarePulse.Desktop;

public sealed class DesktopShortcutPanel : StackPanel,IDisposable {
    readonly Window owner;
    readonly PreviewSettings settings;
    readonly UiLanguage language;
    readonly Action save,toggle;
    readonly bool supported;
    readonly CheckBox enabled;
    readonly Button choose;
    readonly TextBlock status=new(){Name="DesktopShortcutStatus",TextWrapping=TextWrapping.Wrap};
    WindowsDesktopHotkey? native;
    bool choosing,disposed;
    public DesktopShortcutPanel(Window owner,PreviewSettings settings,UiLanguage language,Action save,Action toggle,bool supported) {
        this.owner=owner;this.settings=settings;this.language=language;this.save=save;this.toggle=toggle;this.supported=supported&&OperatingSystem.IsWindows();Spacing=8;
        enabled=language.Set(new CheckBox{Name="DesktopShortcutEnabled",IsChecked=settings.DesktopShortcutEnabled,IsEnabled=this.supported},"Desktop toggle shortcut");
        choose=new Button{Name="DesktopShortcut",Content=settings.DesktopShortcut,IsEnabled=this.supported};
        Children.Add(enabled);Children.Add(choose);Children.Add(status);
        enabled.IsCheckedChanged+=(_,_)=>{settings.DesktopShortcutEnabled=enabled.IsChecked==true;Apply();save();};
        choose.Click+=(_,_)=>{choosing=true;language.Set(choose,"Press a shortcut; Esc cancels");choose.Focus();};
        choose.LostFocus+=(_,_)=>Finish();
        choose.AddHandler(KeyDownEvent,OnShortcutKeyDown,RoutingStrategies.Tunnel);
        owner.Opened+=Opened;owner.Closed+=Closed;language.Changed+=Localize;
        if(this.supported)Win32Properties.AddWndProcHookCallback(owner,Hook);
        Apply();
    }
    public static bool TryGesture(string text,out KeyGesture? gesture,out uint modifiers,out uint key) {
        gesture=null;modifiers=key=0;
        if(text.Length>80)return false;
        // WPF persists digit gestures as "9"; Avalonia needs the enum name "D9".
        int last=text.LastIndexOf('+');string tail=text[(last+1)..].Trim();
        if(tail.Length==1&&tail[0] is >= '0' and <= '9')text=text[..(last+1)]+"D"+tail;
        try {gesture=KeyGesture.Parse(text);}catch(FormatException){return false;}catch(ArgumentException){return false;}
        if((gesture.KeyModifiers&KeyModifiers.Meta)!=0)return false;
        modifiers=((gesture.KeyModifiers&KeyModifiers.Alt)!=0?1u:0)|((gesture.KeyModifiers&KeyModifiers.Control)!=0?2u:0)|((gesture.KeyModifiers&KeyModifiers.Shift)!=0?4u:0);
        key=gesture.Key is >=Key.A and <=Key.Z?(uint)(65+gesture.Key-Key.A):gesture.Key is >=Key.D0 and <=Key.D9?(uint)(48+gesture.Key-Key.D0):gesture.Key is >=Key.F1 and <=Key.F11?(uint)(112+gesture.Key-Key.F1):0;
        return WindowsDesktopHotkey.Valid(modifiers,key);
    }
    static string Format(Key key,KeyModifiers modifiers)=>((modifiers&KeyModifiers.Control)!=0?"Ctrl+":"")+((modifiers&KeyModifiers.Alt)!=0?"Alt+":"")+((modifiers&KeyModifiers.Shift)!=0?"Shift+":"")+(key is >=Key.D0 and <=Key.D9?((int)(key-Key.D0)).ToString():key.ToString());
    void OnShortcutKeyDown(object? sender,KeyEventArgs e) {
        if(!choosing)return;
        if(e.Key==Key.Escape){Finish();e.Handled=true;return;}
        if(e.Key==Key.Tab){Finish();return;}
        if(e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift)return;
        e.Handled=true;string value=Format(e.Key,e.KeyModifiers);
        if((e.KeyModifiers&KeyModifiers.Meta)!=0||!TryGesture(value,out _,out uint mods,out uint key)){language.Set(status,"Use two modifiers (Ctrl, Alt, Shift) with a letter, number or F1–F11.");return;}
        if(native==null||!native.Set(mods,key)){language.Set(status,"Shortcut unavailable. Choose another combination.");return;}
        settings.DesktopShortcut=value;settings.DesktopShortcutEnabled=true;enabled.IsChecked=true;Finish();Apply();save();
    }
    nint Hook(nint hwnd,uint message,nint w,nint l,ref bool handled) {
        if(!disposed&&native?.Matches(message,w,l)==true){handled=true;if(choosing)Finish();else toggle();}return 0;
    }
    void Opened(object? sender,EventArgs e){if(disposed||!supported)return;var handle=owner.TryGetPlatformHandle();if(handle?.HandleDescriptor=="HWND")native??=new(handle.Handle);Apply();}
    void Closed(object? sender,EventArgs e)=>Dispose();
    void Finish(){choosing=false;language.Set(choose,settings.DesktopShortcut);}
    void Localize(){if(choosing)language.Set(choose,"Press a shortcut; Esc cancels");Apply();}
    void Apply(){
        if(disposed)return;
        if(!supported){language.Set(status,"Desktop shortcut is unavailable in this session.");return;}
        if(!settings.DesktopShortcutEnabled){native?.Clear();language.Set(status,"");return;}
        bool ready=TryGesture(settings.DesktopShortcut,out _,out uint mods,out uint key)&&native?.Set(mods,key)==true;
        language.Set(status,ready?"Shortcut ready; game bindings may still overlap.":"Shortcut unavailable. Choose another combination.");
    }
    public void Dispose(){if(disposed)return;disposed=true;native?.Dispose();native=null;owner.Opened-=Opened;owner.Closed-=Closed;language.Changed-=Localize;if(supported)Win32Properties.RemoveWndProcHookCallback(owner,Hook);}
}
