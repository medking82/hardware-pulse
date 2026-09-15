using System;
using System.Windows.Controls;
namespace HardwarePulse {
    public sealed partial class Shell {
        void WireNetwork(){
            var units=Control<ComboBox>("NetworkUnit");foreach(ComboBoxItem item in units.Items)if((string)item.Tag==settings.Text("networkUnit","auto"))units.SelectedItem=item;
            units.SelectionChanged+=delegate{var selected=units.SelectedItem as ComboBoxItem;if(selected!=null){settings.Data["networkUnit"]=(string)selected.Tag;UpdatePanel();QueueSave();}};
        }
    }
}
