param([string]$AppPath='build/native/app')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml
[void][Reflection.Assembly]::LoadFrom((Join-Path ([IO.Path]::GetFullPath($AppPath)) 'HardwarePulse.exe'))
Add-Type @'
using System;using System.Runtime.InteropServices;
public static class ShortcutProbe {
 [DllImport("user32.dll")]public static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr w,IntPtr l);
 public static int Calls;public static void Called(){Calls++;}
}
'@
function Assert($condition,$message){if(!$condition){throw $message}}
$a=[Windows.Window]::new();$b=[Windows.Window]::new();$first=$null;$second=$null
try {
 $ha=[Windows.Interop.WindowInteropHelper]::new($a).EnsureHandle();[void][Windows.Interop.WindowInteropHelper]::new($b).EnsureHandle()
 $action=[Action]{[ShortcutProbe]::Called()};$first=[HardwarePulse.DesktopHotkey]::new($a,$action);$second=[HardwarePulse.DesktopHotkey]::new($b,$action)
 $available=@();foreach($key in @('Ctrl+Alt+F10','Ctrl+Alt+F9','Ctrl+Shift+F10')){if($first.Set($key)){$available+=$key;$first.Clear()}}
 Assert ($available.Count -gt 0) 'No candidate available for registration test'
 $key=$available[0];Assert ($first.Set($key)) 'Initial registration failed';Assert (-not $second.Set($key)) 'Conflict not detected'
 [void][ShortcutProbe]::SendMessage($ha,0x312,[IntPtr]0x504,[IntPtr]::Zero);Assert ([ShortcutProbe]::Calls -eq 1) 'Registered shortcut not dispatched'
 Assert (-not $first.Set('Ctrl+Alt+F12')) 'Reserved F12 accepted'
 [void][ShortcutProbe]::SendMessage($ha,0x312,[IntPtr]0x504,[IntPtr]::Zero);Assert ([ShortcutProbe]::Calls -eq 2) 'Rejected shortcut destroyed old registration'
 $first.Clear();Assert ($second.Set($key)) 'Disabling shortcut did not release registration'
 'PASS shortcut: available now '+($available -join ', ')+'; conflict detection, dispatch, rejected edit preserves binding, unregister'
}finally{if($first){$first.Dispose()};if($second){$second.Dispose()};$a.Close();$b.Close()}
