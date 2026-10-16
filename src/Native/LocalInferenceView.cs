using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;

namespace HardwarePulse {
    public sealed partial class Shell {
        readonly LocalInferenceSession localInference=new LocalInferenceSession(LocalInferenceSource.Read);
        string InferenceProvider {get{return settings.Text("localInferenceProvider","Strata");}}
        LocalInferenceSpec InferenceSpec {get{return LocalInferenceCatalog.Find(InferenceProvider);}}
        string InferenceTitle {get{return InferenceSpec==null?language.Label("Local Model"):InferenceSpec.Name;}}
        void ConfigureLocalInference(){
            localInference.Configure(settings.Flag("localInferenceEnabled",false),InferenceProvider,settings.Text("localInferenceEndpoint","http://127.0.0.1:8080"));
            RenderLocalInference();ApplyDensity(false);UpdateDesktop();QueueSave();
        }
        void WireLocalInference(){
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
            var card=Control<Border>("LocalInferenceCard");if(card==null)return;
            card.Visibility=settings.Flag("localInferenceEnabled",false)?Visibility.Visible:Visibility.Collapsed;
            if(card.Visibility!=Visibility.Visible)return;
            var reading=localInference.Current(DateTimeOffset.UtcNow);Text("LocalInferenceTitle",InferenceTitle);Text("LocalInferenceValue",InferenceValue(reading));Text("LocalInferenceDetail",InferenceDetail(reading));
            card.Background=Brush(light?"#FFFFFF":"#202831");card.BorderBrush=Brush(light?"#D4DCE2":"#40515D");
            foreach(string name in new[]{"LocalInferenceTitle","LocalInferenceValue","LocalInferenceDetail"})Control<TextBlock>(name).Foreground=Brush(light?"#17202B":"#F0F5FA");
            var source=InferenceSpec;Control<ContentControl>("LocalInferenceIcon").Content=Icon(source==null?"live":source.Icon,22,DesktopPaletteColor(source==null?"live":source.Icon,light?"#17202B":"#F0F5FA"));
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
