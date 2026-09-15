using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms=System.Windows.Forms;

namespace HardwarePulse {
    public sealed partial class Shell {
        Forms.ToolStripMenuItem trayFps;
        void WireFpsSwitches(){
            Control<ContentControl>("FpsQuickIcon").Content=Icon("fps",16,"#A5E7D5");
            Control<CheckBox>("FpsQuick").Click+=delegate{SetFpsEnabled(Checked("FpsQuick"));};
            trayFps=new Forms.ToolStripMenuItem("FPS",null,delegate{SetFpsEnabled(!Checked("OverlayEnabled")||!Checked("OverlayFps"));});
            var trayIcon=Icon("fps",20,"#A5E7D5");trayIcon.Measure(new Size(20,20));trayIcon.Arrange(new Rect(0,0,20,20));
            var bitmap=new RenderTargetBitmap(20,20,96,96,PixelFormats.Pbgra32);bitmap.Render(trayIcon);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using(var memory=new MemoryStream()){encoder.Save(memory);memory.Position=0;using(var source=System.Drawing.Image.FromStream(memory))trayFps.Image=new System.Drawing.Bitmap(source);}
            tray.ContextMenuStrip.Items.Insert(2,trayFps);tray.ContextMenuStrip.Disposed+=delegate{if(trayFps.Image!=null)trayFps.Image.Dispose();};
            SyncFpsSwitches();
        }
        void SetFpsEnabled(bool enabled){
            if(enabled){Control<CheckBox>("OverlayFps").IsChecked=true;settings.Map("overlay")["fps"]=true;}
            var main=Control<CheckBox>("OverlayEnabled");main.IsChecked=enabled;main.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
        }
        void SyncFpsSwitches(){bool enabled=Checked("OverlayEnabled")&&Checked("OverlayFps");Control<CheckBox>("FpsQuick").IsChecked=enabled;if(trayFps!=null)trayFps.Checked=enabled;}
    }
}
