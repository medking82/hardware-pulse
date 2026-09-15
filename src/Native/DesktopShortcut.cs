using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace HardwarePulse {
    public sealed class DesktopHotkey : IDisposable {
        [DllImport("user32.dll",SetLastError=true)] static extern bool RegisterHotKey(IntPtr window,int id,uint modifiers,uint key);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr window,int id);
        readonly HwndSource source;readonly Action action;int active;string current;
        public DesktopHotkey(Window window,Action action){source=HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);this.action=action;source.AddHook(Hook);}
        public static bool Valid(Key key,ModifierKeys modifiers){
            int count=((modifiers&ModifierKeys.Control)!=0?1:0)+((modifiers&ModifierKeys.Alt)!=0?1:0)+((modifiers&ModifierKeys.Shift)!=0?1:0);
            return (modifiers&ModifierKeys.Windows)==0&&count>=2&&(key>=Key.A&&key<=Key.Z||key>=Key.D0&&key<=Key.D9||key>=Key.F1&&key<=Key.F11);
        }
        public bool Set(string gesture){
            KeyGesture value;try{value=(KeyGesture)new KeyGestureConverter().ConvertFromInvariantString(gesture);}catch{return false;}
            if(!Valid(value.Key,value.Modifiers))return false;
            if(active!=0&&gesture==current)return true;
            int next=active==0x504?0x505:0x504;
            if(!RegisterHotKey(source.Handle,next,(uint)value.Modifiers|0x4000,(uint)KeyInterop.VirtualKeyFromKey(value.Key)))return false;
            if(active!=0)UnregisterHotKey(source.Handle,active);active=next;current=gesture;return true;
        }
        IntPtr Hook(IntPtr hwnd,int message,IntPtr w,IntPtr l,ref bool handled){if(message==0x312&&active!=0&&w.ToInt32()==active){handled=true;action();}return IntPtr.Zero;}
        public void Clear(){if(active!=0)UnregisterHotKey(source.Handle,active);active=0;current=null;}
        public void Dispose(){Clear();source.RemoveHook(Hook);}
    }
    public sealed partial class Shell {
        DesktopHotkey desktopHotkey;bool choosingShortcut;
        string DesktopShortcutText {get{return settings.Text("desktopShortcut","Ctrl+Alt+F10");}}
        void WireDesktopShortcut(){
            var button=Control<Button>("DesktopShortcut");button.Content=DesktopShortcutText;
            Control<CheckBox>("DesktopShortcutEnabled").IsChecked=settings.Flag("desktopShortcutEnabled",true);
            Window.SourceInitialized+=delegate{if(!isolated){desktopHotkey=new DesktopHotkey(Window,delegate{if(choosingShortcut){FinishShortcut();return;}ToggleDesktopFromShortcut();});ApplyDesktopShortcut();}};
            Control<CheckBox>("DesktopShortcutEnabled").Click+=delegate{settings.Data["desktopShortcutEnabled"]=Checked("DesktopShortcutEnabled");ApplyDesktopShortcut();QueueSave();};
            button.Click+=delegate{choosingShortcut=true;button.Content=language.T("Press a shortcut; Esc cancels");button.Focus();};
            button.LostKeyboardFocus+=delegate{if(choosingShortcut)FinishShortcut();};
            button.PreviewKeyDown+=delegate(object sender,KeyEventArgs e){
                if(!choosingShortcut)return;
                Key key=e.Key==Key.System?e.SystemKey:e.Key;
                if(key==Key.Escape){FinishShortcut();e.Handled=true;return;}
                if(key==Key.Tab){FinishShortcut();return;}
                if(key==Key.LeftCtrl||key==Key.RightCtrl||key==Key.LeftAlt||key==Key.RightAlt||key==Key.LeftShift||key==Key.RightShift)return;
                e.Handled=true;var modifiers=Keyboard.Modifiers;
                if(!DesktopHotkey.Valid(key,modifiers)){Text("DesktopShortcutStatus",language.T("Use two modifiers (Ctrl, Alt, Shift) with a letter, number or F1–F11."));return;}
                string value=new KeyGestureConverter().ConvertToInvariantString(new KeyGesture(key,modifiers));
                if(!isolated&&desktopHotkey!=null&&!desktopHotkey.Set(value)){Text("DesktopShortcutStatus",language.T("Shortcut unavailable. Choose another combination."));return;}
                settings.Data["desktopShortcut"]=value;settings.Data["desktopShortcutEnabled"]=true;Control<CheckBox>("DesktopShortcutEnabled").IsChecked=true;FinishShortcut();ApplyDesktopShortcut();QueueSave();
            };
        }
        void FinishShortcut(){choosingShortcut=false;Control<Button>("DesktopShortcut").Content=DesktopShortcutText;}
        void ApplyDesktopShortcut(){
            if(!Checked("DesktopShortcutEnabled")){if(desktopHotkey!=null)desktopHotkey.Clear();Text("DesktopShortcutStatus","");return;}
            if(desktopHotkey!=null)Text("DesktopShortcutStatus",language.T(desktopHotkey.Set(DesktopShortcutText)?"Shortcut ready; game bindings may still overlap.":"Shortcut unavailable. Choose another combination."));
        }
        void ToggleDesktopFromShortcut(){
            bool enabled=!DesktopEnabled;settings.Data["desktopEnabled"]=enabled;Control<CheckBox>("DesktopEnabled").IsChecked=enabled;
            if(enabled){settings.Data["desktopLocked"]=true;Control<CheckBox>("DesktopLocked").IsChecked=true;}
            else if(desktop!=null){SaveDesktopPosition();desktop.Close();desktop=null;}
            StartOverlay();UpdateDesktop();Save();
        }
    }
}
