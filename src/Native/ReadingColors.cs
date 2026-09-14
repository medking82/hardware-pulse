using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Forms=System.Windows.Forms;

namespace HardwarePulse {
    public sealed partial class Shell {
        string ReadingColor(){var color=settings.Text("readingColor","#DDE9F0");return System.Text.RegularExpressions.Regex.IsMatch(color,"^#[0-9a-fA-F]{6}$")?color:"#DDE9F0";}
        void WireReadingColors(){
            Control<RadioButton>("HardwareReadingColors").IsChecked=!settings.Flag("unifiedReadingColors");
            Control<RadioButton>("UnifiedReadingColors").IsChecked=settings.Flag("unifiedReadingColors");
            Control<RadioButton>("HardwareReadingColors").Checked+=delegate{settings.Data["unifiedReadingColors"]=false;ApplyReadingColors();QueueSave();};
            Control<RadioButton>("UnifiedReadingColors").Checked+=delegate{settings.Data["unifiedReadingColors"]=true;ApplyReadingColors();QueueSave();};
            Click("ReadingColorPicker",delegate{using(var dialog=new Forms.ColorDialog{FullOpen=true,Color=System.Drawing.ColorTranslator.FromHtml(ReadingColor())}){
                if(dialog.ShowDialog()!=Forms.DialogResult.OK)return;
                settings.Data["readingColor"]="#"+dialog.Color.R.ToString("X2")+dialog.Color.G.ToString("X2")+dialog.Color.B.ToString("X2");ApplyReadingColors();QueueSave();
            }});
            ApplyReadingColors();
        }
        void ApplyReadingColors(){
            bool unified=settings.Flag("unifiedReadingColors");
            Control<Button>("ReadingColorPicker").Visibility=unified?Visibility.Visible:Visibility.Collapsed;
            Control<Button>("ReadingColorPicker").Content=ReadingColor();
            foreach(var view in views.Values){
                string color=SystemParameters.HighContrast?SystemColors.WindowTextColor.ToString():light?"#17202B":unified?ReadingColor():view.Accent;
                var brush=Brush(color);
                if(view.HeroValue!=null)view.HeroValue.Foreground=brush;
                foreach(var pair in view.PairItems)pair.Value.Foreground=brush;
                foreach(var row in view.Rows)if(row.Unit=="°C")row.Value.Foreground=row.ShortValue.Foreground=brush;
                if(view.DisplayAccent==color)continue;
                var icon=Icon(view.Key.ToLowerInvariant(),18,color);icon.Margin=new Thickness(0,0,8,0);
                view.TitleRow.Children.RemoveAt(1);view.TitleRow.Children.Insert(1,icon);view.DisplayAccent=color;
            }
        }
    }
}
