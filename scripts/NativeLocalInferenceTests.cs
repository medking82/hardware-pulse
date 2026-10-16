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
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using HardwarePulse;

internal static class NativeLocalInferenceTests {
    const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    static void Check(bool ok,string message){if(!ok)throw new Exception("Local inference: "+message);}
    static object Call(Shell shell,string method,params object[] args){return typeof(Shell).GetMethod(method,Hidden).Invoke(shell,args);}
    static System.Collections.Generic.IEnumerable<DependencyObject> Tree(DependencyObject node){yield return node;foreach(object child in LogicalTreeHelper.GetChildren(node)){var item=child as DependencyObject;if(item!=null)foreach(var next in Tree(item))yield return next;}}
    static void Settle(){var frame=new System.Windows.Threading.DispatcherFrame();var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(300)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();System.Windows.Threading.Dispatcher.PushFrame(frame);}
    static string Fixture(string state,double speed){return "{\"time\":\""+DateTimeOffset.UtcNow.ToString("o")+"\",\"live\":{\"state\":\""+state+"\",\"tok_s\":"+speed.ToString(System.Globalization.CultureInfo.InvariantCulture)+"}}";}
    static void CardLayout(Shell shell,Settings settings,LocalInferenceReading reading,string directory){
        var panel=(ResponsivePanel)shell.Control<StackPanel>("QuotaCards");var card=shell.Control<Border>("LocalInferenceCard");
        Check(card.Parent==panel,"Local card participates in the existing responsive/reorder panel");
        var grip=Tree(card).OfType<Thumb>().SingleOrDefault();Check(grip!=null&&grip.IsEnabled&&grip.Focusable,"Local card exposes the standard accessible drag handle");
        var field=typeof(Shell).GetField("quotas",Hidden);var original=(QuotaSession)field.GetValue(shell);
        using(var fake=new QuotaSession((provider,cancel)=>new QuotaReading{Provider=provider,Status="Live",Observed=DateTimeOffset.UtcNow,Windows=new System.Collections.Generic.List<QuotaWindow>{new QuotaWindow{Label="Weekly",Remaining=65}}})){
            field.SetValue(shell,fake);try{
                var quota=shell.Control<CheckBox>("QuotaCodex");quota.IsChecked=true;quota.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));fake.Tick(DateTimeOffset.UtcNow);
                for(int i=0;i<100&&fake.Readings.Any(r=>r.Status!="Live");i++){Thread.Sleep(10);fake.Tick(DateTimeOffset.UtcNow);}Call(shell,"RenderQuota");
                shell.Window.Width=740;shell.Window.Height=640;Settle();shell.Window.UpdateLayout();
                var peer=panel.Children.Cast<Border>().Single(c=>(string)c.Tag=="Codex");
                Check(panel.Columns==2&&Math.Abs(card.ActualWidth-peer.ActualWidth)<1,"Local card uses one matching column rather than a full-width banner");
                Check(card.Padding==peer.Padding&&card.CornerRadius==peer.CornerRadius&&card.Margin==peer.Margin,"Local card reuses adjacent card geometry");
                var before=panel.Children.Cast<Border>().ToArray();grip.RaiseEvent(new DragStartedEventArgs(0,0){RoutedEvent=Thumb.DragStartedEvent});
                Check(panel.Dragging,"Drag handle is wired to the live layout controller");Call(shell,"RenderQuota");Call(shell,"RenderLocalInference");Check(panel.Children.Cast<Border>().SequenceEqual(before),"Polling does not rebuild cards during a drag");
                grip.RaiseEvent(new DragDeltaEventArgs(0,-100){RoutedEvent=Thumb.DragDeltaEvent});grip.RaiseEvent(new DragCompletedEventArgs(0,-100,true){RoutedEvent=Thumb.DragCompletedEvent});
                Check(!panel.Dragging&&panel.Children.Cast<Border>().SequenceEqual(before),"Canceled drag restores the original order");
                grip.RaiseEvent(new DragStartedEventArgs(0,0){RoutedEvent=Thumb.DragStartedEvent});grip.RaiseEvent(new DragDeltaEventArgs(0,-100){RoutedEvent=Thumb.DragDeltaEvent});grip.RaiseEvent(new DragCompletedEventArgs(0,-100,false){RoutedEvent=Thumb.DragCompletedEvent});
                Check(panel.Children[0]==card&&((string[])settings.Data["quotaCardOrder"])[0]=="localModelSpeed","Completed drag commits the reordered local card");
                grip.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(grip),0,Key.Right){RoutedEvent=Keyboard.PreviewKeyDownEvent});Check(panel.Children[1]==card,"Keyboard reorder can move the local card back");
                var key=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(grip),0,Key.Left){RoutedEvent=Keyboard.PreviewKeyDownEvent};grip.RaiseEvent(key);
                Check(panel.Children[0]==card,"Keyboard reorder moves the local card alongside quota cards");shell.Save();
                Check(new Settings(Path.Combine(directory,"widget-settings.json")).Order("quotaCardOrder",new[]{"Codex","localModelSpeed"})[0]=="localModelSpeed","Local card order persists with a stable backend-neutral key");
                using(var restored=new Shell(new PulsePaths(AppDomain.CurrentDomain.BaseDirectory,directory,Path.Combine(directory,"restored")),true)){
                    Call(restored,"RenderQuota");Check(restored.Control<StackPanel>("QuotaCards").Children[0]==restored.Control<Border>("LocalInferenceCard"),"A new window restores the local card position");restored.Exit();
                }
                fake.Readings[0].Observed=DateTimeOffset.UtcNow;Call(shell,"RenderQuota");Check(panel.Children[0]==card,"Quota refresh preserves the local card position");
                var enabled=shell.Control<CheckBox>("LocalInferenceEnabled");enabled.IsChecked=false;enabled.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Check(!panel.Children.Contains(card),"Disabled local card leaves no grid cell");
                enabled.IsChecked=true;enabled.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Check(panel.Children[0]==card,"Re-enabling preserves saved order");
                var session=(LocalInferenceSession)typeof(Shell).GetField("localInference",Hidden).GetValue(shell);typeof(LocalInferenceSession).GetField("reading",Hidden).SetValue(session,reading);
                var locked=shell.Control<CheckBox>("LockPosition");locked.IsChecked=true;locked.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Call(shell,"RenderLocalInference");Check(!grip.IsEnabled,"Position lock disables local card dragging");locked.IsChecked=false;locked.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Call(shell,"RenderLocalInference");Check(grip.IsEnabled,"Unlock restores local card dragging");
                foreach(string locale in new[]{"en","zh-CN"})foreach(bool light in new[]{false,true})foreach(double width in new[]{280d,740d}){
                    foreach(ComboBoxItem option in shell.Control<ComboBox>("LanguagePicker").Items)if((string)option.Tag==locale)shell.Control<ComboBox>("LanguagePicker").SelectedItem=option;
                    settings.Data["background"]=light?"#F4F6F8":"#202831";Call(shell,"ApplyMaterial");shell.Window.Width=width;
                    shell.Control<Slider>("FontSizeSlider").Value=width==280?18:12;reading.Status="Model unloaded";reading.TokensPerSecond=null;reading.SourceTime=DateTimeOffset.UtcNow;Call(shell,"RenderPanel");Settle();shell.Window.UpdateLayout();
                    Check(panel.Columns==(width==280?1:2),"Local card follows responsive columns and user text scale");
                    Check(Math.Abs(shell.Control<TextBlock>("LocalInferenceTitle").FontSize-13*shell.Window.FontSize/12)<.1,"Local title follows the existing title scale");
                    Check(shell.Control<TextBlock>("LocalInferenceValue").FontSize<=shell.Control<TextBlock>("LocalInferenceTitle").FontSize,"Unloaded status is not an oversized numeric hero");
                    Check(shell.Control<TextBlock>("LocalInferenceDetail").Visibility==Visibility.Collapsed&&Convert.ToString(card.ToolTip).Length>0,"Long telemetry explanation stays discoverable in a tooltip, not permanent card height");
                    foreach(var text in Tree(card).OfType<TextBlock>().Where(t=>t.IsVisible)){var point=text.TransformToAncestor(card).Transform(new Point());Check(point.X>=0&&point.X+text.ActualWidth<=card.ActualWidth+1,"Card text stays inside the actual narrow/wide surface");}
                    var image=new System.Windows.Media.Imaging.RenderTargetBitmap((int)shell.Window.ActualWidth,(int)shell.Window.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);image.Render(shell.Window);var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));using(var file=File.Create(Path.Combine(directory,"card-layout-"+locale+(light?"-light-":"-dark-")+width+".png")))png.Save(file);
                }
                quota.IsChecked=false;quota.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Check(panel.Children.Count==1&&panel.Children[0]==card,"Disabling all quotas does not remove local telemetry");
            }finally{field.SetValue(shell,original);}
        }
        shell.Control<Slider>("FontSizeSlider").Value=12;
    }
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
            shell.Control<StackPanel>("Cards").Visibility=Visibility.Collapsed;
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
                var cardBackground=(System.Windows.Media.SolidColorBrush)shell.Control<Border>("LocalInferenceCard").Background;
                Check(cardBackground.Color.A==32,"Local card uses the same translucent surface as quota cards, not an opaque slab");
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
            CardLayout(shell,settings,value,directory);
            value.Status="Idle";value.TokensPerSecond=null;value.LastTokensPerSecond=41;Call(shell,"RenderLocalInference");Check(shell.Control<TextBlock>("LocalInferenceValue").Text!="41 tok/s","Historical speed never occupies the live cell");
            value.Status="Model unloaded";Call(shell,"RenderLocalInference");Check(shell.Control<TextBlock>("LocalInferenceValue").Text!="0 tok/s","Unloaded is not a measured zero");
            toggle.IsChecked=false;toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Check(shell.Control<Border>("LocalInferenceCard").Visibility==Visibility.Collapsed,"Disabling removes the card");shell.Save();Check(!new Settings(Path.Combine(directory,"widget-settings.json")).Flag("localInferenceEnabled"),"Preference persists");shell.Exit();
        }
        Console.WriteLine("PASS local inference: loopback GET, no auth, redirect rejection, source-version lifecycle, live/idle/unloaded, Strata SVG, desktop visibility and bilingual themes");
        Console.WriteLine("PASS local card: shared translucent surface, responsive columns, drag commit/cancel, keyboard reorder, saved position, lock, scaled text and narrow/wide bilingual themes");Console.WriteLine("Local inference evidence: "+directory);
    }
}
