using System;
using System.Windows.Controls;
using System.Windows.Media;
using Forms=System.Windows.Forms;

namespace HardwarePulse {
    public sealed partial class Shell {
        string OverlayBackground(){
            object saved;string hex=settings.Map("overlay").TryGetValue("background",out saved)?Convert.ToString(saved):"#111923";
            return System.Text.RegularExpressions.Regex.IsMatch(hex??"","^#[0-9a-fA-F]{6}$")?hex:"#111923";
        }
        void WireOverlayAppearance(){
            var state=settings.Map("overlay");object saved;double opacity=80;
            if(state.TryGetValue("opacity",out saved)){try{opacity=Convert.ToDouble(saved);if(double.IsNaN(opacity)||double.IsInfinity(opacity))opacity=80;}catch{opacity=80;}}
            var slider=Control<Slider>("OverlayOpacity");slider.Value=Math.Max(0,Math.Min(100,opacity));
            slider.ValueChanged+=delegate{state["opacity"]=slider.Value;ApplyOverlayAppearance();QueueSave();};
            Click("OverlayBackground",delegate{
                using(var dialog=new Forms.ColorDialog{FullOpen=true,Color=System.Drawing.ColorTranslator.FromHtml(OverlayBackground())}){
                    if(dialog.ShowDialog()!=Forms.DialogResult.OK)return;
                    state["background"]="#"+dialog.Color.R.ToString("X2")+dialog.Color.G.ToString("X2")+dialog.Color.B.ToString("X2");
                    ApplyOverlayAppearance();QueueSave();
                }
            });
            Click("ResetOverlayAppearance",delegate{state["background"]="#111923";state["opacity"]=80d;slider.Value=80;ApplyOverlayAppearance();QueueSave();});
            ApplyOverlayAppearance();
        }
        void ApplyOverlayAppearance(){
            string hex=OverlayBackground();double opacity=Control<Slider>("OverlayOpacity").Value;
            overlay.SetAppearance(hex,opacity);
            Control<Button>("OverlayBackground").Content=hex;
            Text("OverlayOpacityValue",Math.Round(opacity)+"%");
            // Preview shares the actual background brush, with opaque sample text.
            Control<Border>("OverlayPreview").Background=((Border)overlay.Content).Background;
        }
    }
}
