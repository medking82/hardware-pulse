using System;
using Microsoft.Win32;

namespace HardwarePulse {
    public static class WindowsCompatibility {
        public static bool RequiresDriverFreeCollector(Version version){return version!=null&&version.Major<10;}
        public static bool SupportsFpsCapture(Version version){return version!=null&&version.Major>=10;}
        public static bool SupportsCaptureExclusion(Version version){return version!=null&&(version.Major>10||(version.Major==10&&version.Build>=19041));}
        public static Version CurrentVersion() {
            // An unmanifested Framework process can report 6.2 on newer Windows.
            // Read the OS-owned version metadata instead of downgrading modern machines.
            using(var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion")) {
                if(key!=null) {
                    int major,minor,build;
                    if(int.TryParse(Convert.ToString(key.GetValue("CurrentMajorVersionNumber")),out major)&&int.TryParse(Convert.ToString(key.GetValue("CurrentMinorVersionNumber")),out minor)&&int.TryParse(Convert.ToString(key.GetValue("CurrentBuildNumber")),out build))return new Version(major,minor,build);
                    Version legacy;
                    if(Version.TryParse(Convert.ToString(key.GetValue("CurrentVersion")),out legacy))return legacy;
                }
            }
            return Environment.OSVersion.Version;
        }
    }
}
