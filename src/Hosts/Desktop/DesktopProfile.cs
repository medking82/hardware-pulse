using System.Reflection;
using System.Text.RegularExpressions;

namespace HardwarePulse.Desktop;

public static class DesktopProfile {
    public static bool IsInstalledStable {get;}=OperatingSystem.IsWindows()&&WindowsStartupManagement.IsInstalled&&
        IsStableVersion(typeof(DesktopProfile).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion??"");
    public static string InstanceScope=>IsInstalledStable?"Shared.Stable":"Shared.Preview";
    public static string DisplayName=>IsInstalledStable?"Hardware Pulse":"Pulse · Desktop preview";
    public static bool IsStableVersion(string value)=>Regex.IsMatch(value,@"^[0-9]+\.[0-9]+\.[0-9]+(?:\+[0-9A-Za-z.-]+)?$",RegexOptions.CultureInvariant)&&Version.TryParse(value.Split('+')[0],out _);
    public static PreviewSettingsStore? CreateStore(string roaming,string local,bool installedStable) {
        string root=installedStable?local:roaming;
        if(!Path.IsPathFullyQualified(root))return null;
        return installedStable?new(Path.Combine(root,"HardwarePulse","shared-ui-settings.json"),Path.Combine(root,"HardwarePulse","widget-settings.json")):
            new(Path.Combine(root,"HardwarePulse.Preview","settings.json"));
    }
}
