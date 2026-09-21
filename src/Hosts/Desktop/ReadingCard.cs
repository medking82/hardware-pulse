using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace HardwarePulse.Desktop;

// The Monitor cards share the compact surface from the Windows 0.6.27 UI.
// Theme changes repaint the surface, never rebuild readings or restart sampling.
static class ReadingCard {
    public static Border Apply(Border border) {
        border.Padding=new Thickness(10,7);
        border.CornerRadius=new CornerRadius(14);
        border.BorderThickness=new Thickness(1);
        void Paint() {
            bool dark=border.ActualThemeVariant==ThemeVariant.Dark;
            border.Background=new SolidColorBrush(Color.Parse(dark?"#3031485B":"#60FFFFFF"));
            border.BorderBrush=new SolidColorBrush(Color.Parse(dark?"#426D8B9F":"#42607080"));
        }
        border.PropertyChanged+=(_,e)=>{if(e.Property.Name=="ActualThemeVariant")Paint();};
        Paint();return border;
    }
}
