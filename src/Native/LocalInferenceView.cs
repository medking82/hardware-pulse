using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Xml;

namespace HardwarePulse {
    public sealed partial class Shell {
        readonly LocalInferenceSession localInference=new LocalInferenceSession(LocalInferenceSource.Read);
        Thumb localInferenceGrip;
        string InferenceProvider {get{return settings.Text("localInferenceProvider","Strata");}}
        LocalInferenceSpec InferenceSpec {get{return LocalInferenceCatalog.Find(InferenceProvider);}}
        string InferenceTitle {get{return InferenceSpec==null?language.Label("Local Model"):InferenceSpec.Name;}}
        void ConfigureLocalInference(){
            localInference.Configure(settings.Flag("localInferenceEnabled",false),InferenceProvider,settings.Text("localInferenceEndpoint","http://127.0.0.1:8080"));
            RenderLocalInference();ApplyDensity(false);UpdateDesktop();QueueSave();
        }
        void WireLocalInference(){
            var card=Control<Border>("LocalInferenceCard");((Panel)card.Parent).Children.Remove(card);card.Tag="localModelSpeed";
            localInferenceGrip=AiCardGrip(InferenceTitle);Control<ContentControl>("LocalInferenceDrag").Content=localInferenceGrip;
            ((ResponsivePanel)Control<StackPanel>("QuotaCards")).Attach(card,localInferenceGrip,Control<ScrollViewer>("CardScroll"),SaveQuotaOrder);
            var enabled=Control<CheckBox>("LocalInferenceEnabled");enabled.IsChecked=settings.Flag("localInferenceEnabled",false);
            var provider=Control<ComboBox>("LocalInferenceProvider");
            foreach(var source in LocalInferenceCatalog.All)provider.Items.Add(new ComboBoxItem{Tag=source.Name,Content=source.Name});
            provider.SelectedItem=provider.Items.Cast<ComboBoxItem>().FirstOrDefault(item=>(string)item.Tag==InferenceProvider);
            var endpoint=Control<TextBox>("LocalInferenceEndpoint");endpoint.Text=settings.Text("localInferenceEndpoint","http://127.0.0.1:8080");
            enabled.Click+=delegate{settings.Data["localInferenceEnabled"]=enabled.IsChecked==true;ConfigureLocalInference();};
            provider.SelectionChanged+=delegate{var selected=provider.SelectedItem as ComboBoxItem;if(selected==null)return;settings.Data["localInferenceProvider"]=(string)selected.Tag;ConfigureLocalInference();};
            Click("LocalInferenceApply",delegate{
                if(!LocalInferenceSource.ValidEndpoint(endpoint.Text)){Text("LocalInferenceSetupStatus",language.T("Use a loopback HTTP endpoint without credentials."));return;}
                settings.Data["localInferenceEndpoint"]=endpoint.Text;Text("LocalInferenceSetupStatus",language.Label("Endpoint Saved"));ConfigureLocalInference();
            });
            ConfigureLocalInference();
        }
        string InferenceValue(LocalInferenceReading reading){return reading.Status=="Generating"&&reading.TokensPerSecond.HasValue?reading.TokensPerSecond.Value.ToString("0.#",CultureInfo.InvariantCulture)+" tok/s":language.Label(reading.Status);}
        string InferenceDetail(LocalInferenceReading reading){
            string detail=language.T("Backend-reported generation speed; excludes prompt processing.");
            if(reading.Status=="Idle"&&reading.LastTokensPerSecond.HasValue)detail+="\n"+language.Label("Last Request")+": "+reading.LastTokensPerSecond.Value.ToString("0.#",CultureInfo.InvariantCulture)+" tok/s";
            return detail;
        }
        void RenderLocalInference(){
            var card=Control<Border>("LocalInferenceCard");if(card==null||localInferenceGrip==null)return;
            var panel=(ResponsivePanel)Control<StackPanel>("QuotaCards");if(panel.Dragging)return;
            card.Visibility=settings.Flag("localInferenceEnabled",false)?Visibility.Visible:Visibility.Collapsed;
            if(card.Visibility!=Visibility.Visible){panel.Children.Remove(card);return;}
            if(card.Parent!=panel){var order=QuotaOrder();int position=Array.IndexOf(order,"localModelSpeed"),index=0;foreach(Border existing in panel.Children){if(Array.IndexOf(order,(string)existing.Tag)>position)break;index++;}panel.Children.Insert(index,card);}
            var reading=localInference.Current(DateTimeOffset.UtcNow);Text("LocalInferenceTitle",InferenceTitle);Text("LocalInferenceValue",InferenceValue(reading));card.ToolTip=InferenceDetail(reading);
            string last=reading.Status=="Idle"&&reading.LastTokensPerSecond.HasValue?language.Label("Last Request")+": "+reading.LastTokensPerSecond.Value.ToString("0.#",CultureInfo.InvariantCulture)+" tok/s":"";
            Text("LocalInferenceDetail",last);Control<TextBlock>("LocalInferenceDetail").Visibility=last.Length==0?Visibility.Collapsed:Visibility.Visible;
            StyleAiCard(card);var foreground=SystemParameters.HighContrast?SystemColors.WindowTextBrush:Brush(light?"#17202B":"#F0F5FA");
            double scale=Window.FontSize/12;Control<TextBlock>("LocalInferenceTitle").FontSize=13*scale;Control<TextBlock>("LocalInferenceValue").FontSize=(reading.Status=="Generating"&&reading.TokensPerSecond.HasValue?21:13)*scale;Control<TextBlock>("LocalInferenceDetail").FontSize=11*scale;
            foreach(string name in new[]{"LocalInferenceTitle","LocalInferenceValue","LocalInferenceDetail"})Control<TextBlock>(name).Foreground=foreground;
            localInferenceGrip.IsEnabled=!locked;localInferenceGrip.Foreground=foreground;localInferenceGrip.ToolTip=language.T("Drag To Reorder (Esc To Cancel)");System.Windows.Automation.AutomationProperties.SetName(localInferenceGrip,InferenceTitle+" · "+language.T("Reading Order"));
            var source=InferenceSpec;Control<ContentControl>("LocalInferenceIcon").Content=Icon(source==null?"live":source.Icon,19,DesktopPaletteColor(source==null?"live":source.Icon,foreground.ToString()));
        }
        void AddDesktopInference(List<DesktopMetric> metrics){
            if(!settings.Flag("localInferenceEnabled",false))return;
            var reading=localInference.Current(DateTimeOffset.UtcNow);var source=InferenceSpec;
            metrics.Add(new DesktopMetric("localModelSpeed",InferenceTitle,InferenceValue(reading),source==null?"live":source.Icon){ToolTip=InferenceDetail(reading)});
        }
        // Preserve the supplied multi-path 32px Strata mark rather than silently
        // dropping its lower layers through the existing single-path 24px loader.
        Viewbox StrataIcon(XmlDocument document,double size,string color){
            var canvas=new Canvas{Width=32,Height=32};
            foreach(XmlNode node in document.SelectNodes("/*[local-name()='svg']/*[local-name()='path']")){
                bool fill=node.Attributes["fill"]!=null&&node.Attributes["fill"].Value!="none";
                var shape=new System.Windows.Shapes.Path{Data=Geometry.Parse(node.Attributes["d"].Value),Fill=fill?Brush(color):null,Stroke=fill?null:Brush(color),StrokeThickness=3.67,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round};
                double opacity;if(node.Attributes["opacity"]!=null&&double.TryParse(node.Attributes["opacity"].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out opacity))shape.Opacity=opacity;
                canvas.Children.Add(shape);
            }
            return new Viewbox{Width=size,Height=size,Child=canvas,IsHitTestVisible=false};
        }
    }
}
