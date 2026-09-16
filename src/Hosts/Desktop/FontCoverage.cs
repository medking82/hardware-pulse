using System.Globalization;
using System.Text;
using Avalonia.Media;

namespace HardwarePulse.Desktop;

public sealed record FontCoverageResult(string Language,int CodePoints,string[] Missing);

// Developer-only observation against the initialized native font manager.
// Missing glyphs are evidence, not permission to install fonts or alter OS settings.
public static class FontCoverage {
    public static FontCoverageResult[] Capture() {
        return new[]{"en","zh-CN","zh-TW"}.Select(language=>{
            var culture=CultureInfo.GetCultureInfo(language);
            var points=UiLanguage.Catalog(language).SelectMany(text=>text.EnumerateRunes())
                .Where(rune=>!Rune.IsWhiteSpace(rune)).Select(rune=>rune.Value).Distinct().Order().ToArray();
            bool Supports(int point)=>new[]{FontWeight.Normal,FontWeight.SemiBold}.All(weight=>
                FontManager.Current.TryMatchCharacter(point,FontStyle.Normal,weight,FontStretch.Normal,DesktopFonts.ForLanguage(language),culture,out _));
            return new FontCoverageResult(language,points.Length,points.Where(point=>!Supports(point)).Select(point=>"U+"+point.ToString("X4")).ToArray());
        }).ToArray();
    }
}
