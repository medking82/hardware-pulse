using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Forms=System.Windows.Forms;

namespace HardwarePulse {
    public sealed partial class Shell {
        DesktopView desktop;Forms.ToolStripMenuItem trayDesktop,trayDesktopEdit,trayDesktopLock;
        bool DesktopEnabled {get{return settings.Flag("desktopEnabled");}}
        string DesktopColor(){string hex=settings.Text("desktopColor","#E4F3EF");return System.Text.RegularExpressions.Regex.IsMatch(hex,"^#[0-9a-fA-F]{6}$")?hex:"#E4F3EF";}
        void WireDesktop(){
            Control<CheckBox>("DesktopEnabled").IsChecked=DesktopEnabled;
            Control<CheckBox>("DesktopLocked").IsChecked=settings.Flag("desktopLocked",true);
            Control<Slider>("DesktopFontSize").Value=settings.Number("desktopFontSize",16,10,32);
            Control<Slider>("DesktopSpacing").Value=settings.Number("desktopSpacing",14,4,40);
            Control<CheckBox>("DesktopAutoContrast").IsChecked=settings.Flag("desktopAutoContrast",!settings.Data.ContainsKey("desktopColor"));
            Control<Slider>("DesktopTextOpacity").Value=settings.Number("desktopTextOpacity",100,30,100);
            Control<CheckBox>("DesktopAutoContrast").Click+=delegate{settings.Data["desktopAutoContrast"]=Checked("DesktopAutoContrast");UpdateDesktop();QueueSave();};
            Control<Slider>("DesktopTextOpacity").ValueChanged+=delegate{settings.Data["desktopTextOpacity"]=Control<Slider>("DesktopTextOpacity").Value;UpdateDesktop();QueueSave();};
            Control<CheckBox>("DesktopEnabled").Click+=delegate{SetDesktopEnabled(Checked("DesktopEnabled"));};
            Control<CheckBox>("DesktopLocked").Click+=delegate{SetDesktopLocked(Checked("DesktopLocked"));};
            foreach(string key in new[]{"DesktopFontSize","DesktopSpacing"}){
                string name=key;Control<Slider>(name).ValueChanged+=delegate{settings.Data[name=="DesktopFontSize"?"desktopFontSize":"desktopSpacing"]=Control<Slider>(name).Value;UpdateDesktop();QueueSave();};
            }
            Click("DesktopColor",delegate{using(var dialog=new Forms.ColorDialog{FullOpen=true,Color=System.Drawing.ColorTranslator.FromHtml(DesktopColor())}){
                if(dialog.ShowDialog()!=Forms.DialogResult.OK)return;
                settings.Data["desktopColor"]="#"+dialog.Color.R.ToString("X2")+dialog.Color.G.ToString("X2")+dialog.Color.B.ToString("X2");UpdateDesktop();QueueSave();
                settings.Data["desktopAutoContrast"]=false;Control<CheckBox>("DesktopAutoContrast").IsChecked=false;UpdateDesktop();QueueSave();
            }});
            Click("DesktopDone",delegate{SetDesktopLocked(true);Save();if(DesktopEnabled)Window.Hide();});
            Click("DesktopMove",delegate{if(!DesktopEnabled)SetDesktopEnabled(true);SetDesktopLocked(false);Window.Hide();});
            Click("DesktopResetPosition",delegate{if(desktop!=null){desktop.Left=SystemParameters.WorkArea.Left+40;desktop.Top=SystemParameters.WorkArea.Top+100;desktop.KeepOnScreen();SaveDesktopPosition();}});
            BuildDesktopOrder();
            UpdateDesktopLabels();
        }
        void SetDesktopEnabled(bool enabled){
            settings.Data["desktopEnabled"]=enabled;Control<CheckBox>("DesktopEnabled").IsChecked=enabled;
            if(enabled){settings.Data["desktopLocked"]=false;Control<CheckBox>("DesktopLocked").IsChecked=false;}
            if(!enabled&&desktop!=null){desktop.Close();desktop=null;}
            UpdateDesktop();Save();
            if(enabled)EditDesktop();else{Show();ShowSettings(false);}
        }
        void SetDesktopLocked(bool value){settings.Data["desktopLocked"]=value;Control<CheckBox>("DesktopLocked").IsChecked=value;UpdateDesktop();Save();}
        void SaveDesktopPosition(){if(desktop==null)return;settings.Data["desktopLeft"]=desktop.Left;settings.Data["desktopTop"]=desktop.Top;QueueSave();}
        void EditDesktop(){if(DesktopEnabled)SetDesktopLocked(false);Show();ShowSettings(true);Control<Expander>("DesktopSection").IsExpanded=true;Control<Expander>("DesktopSection").BringIntoView();}
        void BuildDesktopTray(Forms.ContextMenuStrip menu){
            trayDesktop=(Forms.ToolStripMenuItem)menu.Items.Add("Desktop Mode",null,delegate{SetDesktopEnabled(!DesktopEnabled);});
            trayDesktopEdit=(Forms.ToolStripMenuItem)menu.Items.Add("Edit Desktop",null,delegate{EditDesktop();});
            trayDesktopLock=(Forms.ToolStripMenuItem)menu.Items.Add("Lock Desktop",null,delegate{SetDesktopLocked(!settings.Flag("desktopLocked",true));});
            menu.Opening+=delegate{trayDesktop.Checked=DesktopEnabled;trayDesktopLock.Checked=settings.Flag("desktopLocked",true);trayDesktopEdit.Enabled=trayDesktopLock.Enabled=DesktopEnabled;};
        }
        void UpdateDesktopLabels(){
            Control<Button>("DesktopColor").Content=DesktopColor();
            Text("DesktopFontValue",Control<Slider>("DesktopFontSize").Value+" px");Text("DesktopSpacingValue",Control<Slider>("DesktopSpacing").Value+" px");
            Text("DesktopTextOpacityValue",Control<Slider>("DesktopTextOpacity").Value+"%");
            Control<Button>("DesktopDone").IsEnabled=DesktopEnabled;
            Control<CheckBox>("DesktopLocked").IsEnabled=DesktopEnabled;
            UpdateDesktopOrderLabels();
            if(trayDesktop!=null){trayDesktop.Text=language.T("Desktop Mode");trayDesktopEdit.Text=language.T("Edit Desktop");trayDesktopLock.Text=language.T("Lock Desktop");}
        }
        string DesktopReading(string key,string unit){double value;return readings.Latest.state=="LIVE"&&readings.Latest.values.TryGetValue(key,out value)?value.ToString(unit==" RPM"?"0":"0.#")+unit:"—";}
        string DesktopProcessor(string temperature,string load){return string.Join("   ",new[]{Available(temperature)?DesktopReading(temperature," °C"):null,Available(load)?DesktopReading(load,"%"):null}.Where(value=>value!=null));}
        string DesktopUsage(string key){Usage value;return readings.Latest.state=="LIVE"&&readings.Latest.usage.TryGetValue(key,out value)?string.Format("{0:0.#} / {1:0.#} GB · {2:0}%",value.used,value.total,value.percent):"—";}
        string DesktopFanTitle(string key){
            string fallback=key=="cpuFan"?"CPU Fan":key=="gpuFan"?(readings.Latest.gpuFanCount>1?"GPU Fan 1":"GPU Fan"):key=="gpuFan2"?"GPU Fan 2":key=="bottom"?"System Fan 1":"System Fan 2";
            return Device(key,fallback);
        }
        List<DesktopMetric> DesktopMetrics(){
            var result=new List<DesktopMetric>();
            foreach(var card in cards.Children.Cast<Border>()){
                string key=(string)card.Tag;if(!CardEnabled(key))continue;
                if(key=="CPU"&&(Available("cpu")||Available("cpuLoad")))result.Add(new DesktopMetric(key,language.T(key),DesktopProcessor("cpu","cpuLoad"),"cpu"));
                if(key=="GPU"&&(Available("gpu")||Available("gpuLoad")))result.Add(new DesktopMetric(key,language.T(key),DesktopProcessor("gpu","gpuLoad"),"gpu"));
                if(key=="GPU"&&readings.HasUsage("vram")){
                    Usage usage;string label=readings.Latest.usage.TryGetValue("vram",out usage)?usage.label:null;
                    result.Add(new DesktopMetric("vram",language.T(label??"VRAM"),DesktopUsage("vram"),"gpu"));
                }
                if(key=="Memory"&&(readings.HasUsage("ram")||readings.Latest.available==null))result.Add(new DesktopMetric(key,language.T("Memory"),DesktopUsage("ram"),"memory"));
                if(key=="NVMe")foreach(string disk in new[]{"diskC","diskD"})if(Available(disk))result.Add(new DesktopMetric(disk,Device(disk,disk=="diskC"?"Drive 1":"Drive 2"),DesktopReading(disk," °C"),"nvme"));
                if(key=="Airflow")foreach(string fan in new[]{"cpuFan","gpuFan","gpuFan2","bottom","top"})if(Available(fan))result.Add(new DesktopMetric(fan,DesktopFanTitle(fan),DesktopReading(fan," RPM"),fan.StartsWith("gpu")?"gpu":"airflow"));
            }
            if(readings.Latest.state!="LIVE")result.Add(new DesktopMetric("status",language.T(readings.Latest.state),language.T("Waiting for collector"),"live"));
            if(result.Count==0)result.Add(new DesktopMetric("empty","Pulse",language.T("No cards shown. Choose cards in Settings."),"live"));
            var order=DesktopOrderKeys();return result.OrderBy(metric=>{int index=Array.IndexOf(order,metric.Key);return index<0?int.MaxValue:index;}).ToList();
        }
        void UpdateDesktop(){
            UpdateDesktopLabels();if(!loaded||!DesktopEnabled)return;
            if(desktop==null){
                desktop=new DesktopView((name,size,color)=>Icon(name,size,color),isolated);
                desktop.Left=settings.Number("desktopLeft",SystemParameters.WorkArea.Left+40,-100000,100000);desktop.Top=settings.Number("desktopTop",SystemParameters.WorkArea.Top+100,-100000,100000);
                desktop.PositionSaved+=SaveDesktopPosition;
                desktop.Show();
            }
            string effectiveColor=desktop.ResolveColor(Checked("DesktopAutoContrast"),DesktopColor());
            desktop.Render(DesktopMetrics(),Control<Slider>("DesktopFontSize").Value,Control<Slider>("DesktopSpacing").Value,effectiveColor,settings.Flag("desktopLocked",true));
            desktop.SetTextOpacity(Control<Slider>("DesktopTextOpacity").Value);
            desktop.RefreshLayer();
            Text("DesktopStatus",language.T(desktop.LayerAvailable?"Use the system tray to edit or exit Desktop Mode.":"Waiting for Windows desktop"));
        }
    }
}
