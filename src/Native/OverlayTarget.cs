using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HardwarePulse {
    public static class OverlayTarget {
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
        public static bool Excluded(string name){return string.Equals(name,"HardwarePulse",StringComparison.OrdinalIgnoreCase)||string.Equals(name,"explorer",StringComparison.OrdinalIgnoreCase)||string.Equals(name,"dwm",StringComparison.OrdinalIgnoreCase)||string.Equals(name,"ShellExperienceHost",StringComparison.OrdinalIgnoreCase)||string.Equals(name,"StartMenuExperienceHost",StringComparison.OrdinalIgnoreCase)||string.Equals(name,"SearchHost",StringComparison.OrdinalIgnoreCase)||string.Equals(name,"LockApp",StringComparison.OrdinalIgnoreCase);}
        public static bool Eligible(Process process){try{return !process.HasExited&&process.Id!=Process.GetCurrentProcess().Id&&process.MainWindowHandle!=IntPtr.Zero&&!Excluded(process.ProcessName)&&FpsProtocol.OwnProcess(process);}catch{return false;}}
        public static Process Resolve(string name,Process current){
            if(string.IsNullOrEmpty(name)){
                uint pid;GetWindowThreadProcessId(GetForegroundWindow(),out pid);
                try{var foreground=Process.GetProcessById(checked((int)pid));if(Eligible(foreground))return foreground;foreground.Dispose();}catch{}
                // Keep the previous game's sample history while configuring Pulse or
                // visiting the desktop. GameOverlay itself hides when not foreground.
                return current!=null&&Eligible(current)?current:null;
            }
            if(current!=null&&Eligible(current)&&string.Equals(current.ProcessName,name,StringComparison.OrdinalIgnoreCase))return current;
            Process chosen=null;try{foreach(var candidate in Process.GetProcessesByName(name)){if(chosen==null&&Eligible(candidate))chosen=candidate;else candidate.Dispose();}}catch{}
            return chosen;
        }
    }
}
