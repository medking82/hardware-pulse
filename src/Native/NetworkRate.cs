using System;
using System.Windows.Controls;
namespace HardwarePulse {
    public sealed partial class Shell {
        void WireNetwork(){
            var units=Control<ComboBox>("NetworkUnit");foreach(ComboBoxItem item in units.Items)if((string)item.Tag==settings.Text("networkUnit","auto"))units.SelectedItem=item;
            units.SelectionChanged+=delegate{var selected=units.SelectedItem as ComboBoxItem;if(selected!=null){settings.Data["networkUnit"]=(string)selected.Tag;UpdatePanel();QueueSave();}};
        }
    }
    public static class NetworkRate {
        public static string Format(double bytesPerSecond,string unit){
            if(double.IsNaN(bytesPerSecond)||double.IsInfinity(bytesPerSecond)||bytesPerSecond<0)return "—";
            if(unit!="KB/s"&&unit!="MB/s"&&unit!="Mbit/s")unit=bytesPerSecond>=1000000?"MB/s":"KB/s";
            double value=unit=="Mbit/s"?bytesPerSecond*8/1000000:unit=="MB/s"?bytesPerSecond/1000000:bytesPerSecond/1000;
            return value.ToString("0.##")+" "+unit;
        }
    }
}
