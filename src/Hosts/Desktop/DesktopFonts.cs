using Avalonia.Media;

namespace HardwarePulse.Desktop;

public static class DesktopFonts {
    public static readonly FontFamily Simplified = new("avares://Pulse.Desktop/Assets/Fonts#Noto Sans CJK SC");
    public static readonly FontFamily Traditional = new("avares://Pulse.Desktop/Assets/Fonts#Noto Sans CJK TC");
    public static FontManagerOptions Options() => new() {
        FontFallbacks = new[] {
            new FontFallback { FontFamily = Simplified },
            new FontFallback { FontFamily = Traditional }
        }
    };
    public static FontFamily? ForLanguage(string language) => language switch {
        "zh-CN" => Simplified, "zh-TW" => Traditional, _ => null
    };
}
