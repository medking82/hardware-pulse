using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using HardwarePulse;

internal static class NativeLocalInferenceTests {
    const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    static void Check(bool ok,string message){if(!ok)throw new Exception("Local inference: "+message);}
    static object Call(Shell shell,string method,params object[] args){return typeof(Shell).GetMethod(method,Hidden).Invoke(shell,args);}
    static System.Collections.Generic.IEnumerable<DependencyObject> Tree(DependencyObject node){yield return node;foreach(object child in LogicalTreeHelper.GetChildren(node)){var item=child as DependencyObject;if(item!=null)foreach(var next in Tree(item))yield return next;}}
    static void Settle(){var frame=new System.Windows.Threading.DispatcherFrame();var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(300)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();System.Windows.Threading.Dispatcher.PushFrame(frame);}
    static string Fixture(string state,double speed){return "{\"time\":\""+DateTimeOffset.UtcNow.ToString("o")+"\",\"live\":{\"state\":\""+state+"\",\"tok_s\":"+speed.ToString(System.Globalization.CultureInfo.InvariantCulture)+"}}";}
    static LocalInferenceReading Request(Type adapter,string response,out string request){
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;
        string captured=null;Exception error=null;
        var thread=new Thread(()=>{try{using(var client=listener.AcceptTcpClient()){client.ReceiveTimeout=3000;using(var stream=client.GetStream())using(var input=new StreamReader(stream,Encoding.ASCII,false,1024,true)){var lines=new System.Collections.Generic.List<string>();for(string line;(line=input.ReadLine())!=null&&line!="";)lines.Add(line);captured=string.Join("\n",lines);byte[] data=Encoding.UTF8.GetBytes(response);stream.Write(data,0,data.Length);}}}catch(Exception e){error=e;}});thread.IsBackground=true;thread.Start();
        LocalInferenceReading result;
        try{result=(LocalInferenceReading)adapter.GetMethod("Read").Invoke(null,new object[]{"Strata","http://127.0.0.1:"+port,CancellationToken.None});Check(thread.Join(4000),"Fixture finishes without a second request");}finally{listener.Stop();}
        Check(error==null,"Loopback fixture completed");request=captured;return result;
    }
    internal static void Run(string root,string directory){
        var adapter=typeof(QuotaProviders).Assembly.GetType("HardwarePulse.LocalInferenceSource");Check(adapter!=null,"Read-only local telemetry adapter exists");
        foreach(string invalid in new[]{"https://example.com","http://localhost:8080","http://127.0.0.1:8080/private","http://127.0.0.1:8080/?key=fixture","http://user:fixture@127.0.0.1:8080","file:///metrics"})Check(!(bool)adapter.GetMethod("ValidEndpoint").Invoke(null,new object[]{invalid}),"Reject non-literal-loopback, credentials, arbitrary paths and queries before IO");
        string request,body=Fixture("generating",59.5);var value=Request(adapter,"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: "+Encoding.UTF8.GetByteCount(body)+"\r\nConnection: close\r\n\r\n"+body,out request);
        Check(value.Status=="Generating"&&value.TokensPerSecond==59.5,"Real HTTP adapter decodes output speed");Check(request.StartsWith("GET /metrics HTTP/1.1")&&request.IndexOf("Authorization",StringComparison.OrdinalIgnoreCase)<0&&request.IndexOf("Cookie",StringComparison.OrdinalIgnoreCase)<0,"Only GET metadata, no credentials or cookies");
        Check(Request(adapter,"HTTP/1.1 302 Found\r\nLocation: http://127.0.0.1:1/metrics\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",out request).Status=="Unavailable","Redirects do not cross the configured source");
        Directory.CreateDirectory(directory);Json.WriteAtomic(Path.Combine(directory,"widget-settings.json"),new {language="en",solid=true});
        using(var shell=new Shell(new PulsePaths(root,directory,Path.Combine(directory,"runtime")),true)){
            var toggle=shell.Control<CheckBox>("LocalInferenceEnabled");Check(toggle!=null&&toggle.IsChecked!=true,"Local telemetry is opt-in");
            shell.Window.ShowActivated=false;shell.Window.ShowInTaskbar=false;shell.Show();
            shell.Control<StackPanel>("Cards").Visibility=Visibility.Collapsed;shell.Control<StackPanel>("QuotaCards").Visibility=Visibility.Collapsed;
            toggle.IsChecked=true;toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            var session=(LocalInferenceSession)typeof(Shell).GetField("localInference",Hidden).GetValue(shell);
            typeof(LocalInferenceSession).GetField("reading",Hidden).SetValue(session,value);
            Call(shell,"RenderLocalInference");var rows=((System.Collections.Generic.IEnumerable<DesktopMetric>)Call(shell,"DesktopMetrics")).Where(m=>m.Key=="localModelSpeed").ToArray();
            Check(rows.Length==1&&rows[0].Icon=="strata"&&rows[0].Value=="59.5 tok/s","Desktop uses the Strata SVG and concise numeric unit");
            Check(((string[])Call(shell,"DesktopOrderKeys")).Last()=="localModelSpeed","Local model starts after quota rows");
            var settings=(Settings)typeof(Shell).GetField("settings",Hidden).GetValue(shell);settings.Map("desktopVisible")["localModelSpeed"]=false;
            foreach(string card in new[]{"CPU","GPU","Memory","NVMe","Airflow","Network"})settings.Map("cardsVisible")[card]=false;
            Call(shell,"RenderPanel");Check(shell.Control<TextBlock>("CardsEmpty").Visibility==Visibility.Collapsed,"A local-inference-only layout is not an empty monitor");
            Check(!((System.Collections.Generic.IEnumerable<DesktopMetric>)Call(shell,"DesktopMetrics")).Any(m=>m.Key=="localModelSpeed"),"Desktop visibility is respected");settings.Map("desktopVisible")["localModelSpeed"]=true;
            foreach(string locale in new[]{"en","zh-CN"})foreach(bool light in new[]{false,true}){
                foreach(ComboBoxItem option in shell.Control<ComboBox>("LanguagePicker").Items)if((string)option.Tag==locale)shell.Control<ComboBox>("LanguagePicker").SelectedItem=option;
                settings.Data["background"]=light?"#F4F6F8":"#202831";Call(shell,"ApplyMaterial");
                value.SourceTime=DateTimeOffset.UtcNow;Call(shell,"RenderLocalInference");Check(shell.Control<TextBlock>("LocalInferenceValue").Text=="59.5 tok/s","Card renders live speed in each theme/language");
                shell.Window.Width=280;shell.Window.Height=640;Settle();shell.Window.UpdateLayout();
                var mark=(Viewbox)Call(shell,"Icon","strata",24d,(string)Call(shell,"DesktopPaletteColor","strata",light?"#17202B":"#F0F5FA"));Check(((Canvas)mark.Child).Children.Count==3,"All three original SVG layers are rendered");
                Check(((System.Windows.Media.SolidColorBrush)((System.Windows.Shapes.Path)((Canvas)mark.Child).Children[0]).Fill).Color==(System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(light?"#17202B":"#10B981"),"Actual SVG fill uses the production theme palette");
                foreach(double font in new[]{16d,20d,32d}){
                    var desktop=new DesktopView((name,size,color)=>(FrameworkElement)Call(shell,"Icon",name,size,color),true);
                    try{desktop.Width=font==32?466:280;desktop.ShowActivated=false;desktop.Show();desktop.Render(rows,font,8,light?"#17202B":"#F0F5FA",true,1,name=>(string)Call(shell,"DesktopPaletteColor",name,light?"#17202B":"#F0F5FA"));desktop.SetTextOpacity(100,false,true,100);Settle();desktop.UpdateLayout();
                        foreach(var text in Tree(desktop).OfType<TextBlock>().Where(t=>t.IsVisible)){var point=text.TransformToAncestor(desktop).Transform(new Point());Check(point.X>=-1&&point.X+text.ActualWidth<=desktop.ActualWidth+1,"Desktop text stays inside the real row bounds");}
                        if(font==16){var shot=new System.Windows.Media.Imaging.RenderTargetBitmap((int)desktop.ActualWidth,(int)desktop.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);shot.Render(desktop);var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(shot));using(var file=File.Create(Path.Combine(directory,"local-desktop-"+locale+(light?"-light":"-dark")+".png")))png.Save(file);}
                    }finally{desktop.Close();}
                }
                var image=new System.Windows.Media.Imaging.RenderTargetBitmap((int)shell.Window.ActualWidth,(int)shell.Window.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);image.Render(shell.Window);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));using(var file=File.Create(Path.Combine(directory,"local-inference-"+locale+(light?"-light":"-dark")+".png")))encoder.Save(file);
            }
            value.Status="Idle";value.TokensPerSecond=null;value.LastTokensPerSecond=41;Call(shell,"RenderLocalInference");Check(shell.Control<TextBlock>("LocalInferenceValue").Text!="41 tok/s","Historical speed never occupies the live cell");
            value.Status="Model unloaded";Call(shell,"RenderLocalInference");Check(shell.Control<TextBlock>("LocalInferenceValue").Text!="0 tok/s","Unloaded is not a measured zero");
            toggle.IsChecked=false;toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Check(shell.Control<Border>("LocalInferenceCard").Visibility==Visibility.Collapsed,"Disabling removes the card");shell.Save();Check(!new Settings(Path.Combine(directory,"widget-settings.json")).Flag("localInferenceEnabled"),"Preference persists");shell.Exit();
        }
        Console.WriteLine("PASS local inference: loopback GET, no auth, redirect rejection, source-version lifecycle, live/idle/unloaded, Strata SVG, desktop visibility and bilingual themes");Console.WriteLine("Local inference evidence: "+directory);
    }
}
