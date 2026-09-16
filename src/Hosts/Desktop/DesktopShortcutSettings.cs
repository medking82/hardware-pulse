using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace HardwarePulse.Desktop;

public sealed class DesktopShortcutSettings : StackPanel,IDisposable {
    readonly CheckBox enabled=new(){Name="DesktopShortcutEnabled"};
    readonly Button choose=new(){Name="DesktopShortcut"};
    readonly TextBlock status=new(){Name="DesktopShortcutStatus",TextWrapping=Avalonia.Media.TextWrapping.Wrap};
    readonly UiLanguage language;
    readonly PreviewSettings settings;
    readonly Action save;
    IDesktopShortcut? registration;
    bool choosing,disposed;
    public DesktopShortcutSettings(UiLanguage language,PreviewSettings settings,Action save,bool supported) {
        this.language=language;this.settings=settings;this.save=save;Spacing=12;
        Children.Add(language.Set(enabled,"Enable Desktop shortcut"));Children.Add(choose);Children.Add(status);
        enabled.IsChecked=settings.DesktopShortcutEnabled;enabled.IsEnabled=choose.IsEnabled=supported;
        choose.Content=settings.DesktopShortcut;
        if(!supported)language.Set(status,"Global shortcut is unavailable in this session.");
        enabled.IsCheckedChanged+=(_,_)=>{settings.DesktopShortcutEnabled=enabled.IsChecked==true;Apply();save();};
        choose.Click+=(_,_)=>{choosing=true;language.Set(choose,"Press a shortcut; Esc cancels");choose.Focus();};
        choose.LostFocus+=(_,_)=>Finish();
        choose.AddHandler(KeyDownEvent,OnKey,RoutingStrategies.Tunnel);
    }
    public void Attach(IDesktopShortcut value){if(disposed){value.Dispose();return;}registration?.Dispose();registration=value;Apply();}
    void Apply() {
        if(registration==null)return;
        if(!settings.DesktopShortcutEnabled){registration.Clear();language.Set(status,"");Finish();return;}
        language.Set(status,registration.Set(settings.DesktopShortcut)?"Shortcut ready; game bindings may still overlap.":"Shortcut unavailable. Choose another combination.");
    }
    void Finish(){choosing=false;language.Set(choose,settings.DesktopShortcut);}
    void OnKey(object? sender,KeyEventArgs e) {
        if(!choosing)return;
        if(e.Key==Key.Escape){e.Handled=true;Finish();return;}
        if(e.Key==Key.Tab){Finish();return;}
        if(e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift)return;
        e.Handled=true;
        string value=new KeyGesture(e.Key,e.KeyModifiers).ToString();
        if(!WindowsDesktopShortcut.TryParse(value,out _,out _,out _)){language.Set(status,"Use two modifiers (Ctrl, Alt, Shift) with a letter, number or F1–F11.");return;}
        if(registration==null||!registration.Set(value)){language.Set(status,"Shortcut unavailable. Choose another combination.");return;}
        settings.DesktopShortcut=value;settings.DesktopShortcutEnabled=true;enabled.IsChecked=true;Finish();Apply();save();
    }
    public bool CancelCapture(){if(!choosing)return false;Finish();return true;}
    public void Dispose(){if(disposed)return;disposed=true;registration?.Dispose();registration=null;}
}
