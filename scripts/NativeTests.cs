using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HardwarePulse;

internal static class NativeTests {
    static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
    static RawSnapshot Snapshot(){return new RawSnapshot {schema=2,pid=1,sequence=1,time=DateTimeOffset.Now.ToString("o"),boardName="Demo board",memoryName="32 GB DDR4-3200 configured",ramUsage=new RamUsage {usedGb=12,totalGb=32},sensors=new[]{
        new Sensor {id="/cpu/temperature/0",hardwareId="/cpu",hardwareType="Cpu",hardware="Demo CPU",name="CPU Package",type="Temperature",value=59},
        new Sensor {id="/cpu/load/0",hardwareId="/cpu",hardwareType="Cpu",hardware="Demo CPU",name="CPU Total",type="Load",value=15},
        new Sensor {id="/gpu/temperature/0",hardwareId="/gpu",hardwareType="GpuNvidia",hardware="Demo GPU",name="GPU Core",type="Temperature",value=49},
        new Sensor {id="/gpu/load/0",hardwareId="/gpu",hardwareType="GpuNvidia",hardware="Demo GPU",name="GPU Core",type="Load",value=18},
        new Sensor {id="/gpu/memory/0",hardwareId="/gpu",hardwareType="GpuNvidia",hardware="Demo GPU",name="GPU Memory Used",type="SmallData",value=2048},
        new Sensor {id="/gpu/memory/1",hardwareId="/gpu",hardwareType="GpuNvidia",hardware="Demo GPU",name="GPU Memory Total",type="SmallData",value=8192},
        new Sensor {id="/board/fan/0",hardwareId="/board",hardwareType="SuperIO",hardware="Demo board",name="Pump Fan",type="Fan",value=2061},
        new Sensor {id="/board/fan/1",hardwareId="/board",hardwareType="SuperIO",hardware="Demo board",name="System Fan #1",type="Fan",value=985},
        new Sensor {id="/nvme/0/temperature/0",hardwareId="/nvme/0",hardwareType="Storage",hardware="Demo SSD",name="Temperature",type="Temperature",value=41}
    }};}
    static IEnumerable<DependencyObject> Tree(DependencyObject node){yield return node;foreach(object child in LogicalTreeHelper.GetChildren(node)){var d=child as DependencyObject;if(d!=null)foreach(var item in Tree(d))yield return item;}}
    static void Click(Shell shell,string name){shell.Control<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();}
    static void Toggle(Shell shell,string name,bool value){var control=shell.Control<CheckBox>(name);control.IsChecked=value;control.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();}
    static T Field<T>(Shell shell,string name){return (T)typeof(Shell).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(shell);}
    static void SharedFeatures(){
        NativeFpsTests.Run();
        using(var metrics=new FrameCapture()){
            metrics.Reset(42);for(int i=0;i<99;i++)metrics.Add("main",10,10);metrics.Add("main",100,10);var result=metrics.ReadAt(10);
            Assert(result.Ready&&result.Minimum==10&&result.Low==10&&Math.Abs(result.Average-100000d/1090)<.001,"FPS aggregate definitions");metrics.Add("other",1,10);Assert(metrics.ReadAt(10).Count==100&&!metrics.ReadAt(13).Ready,"FPS swapchain/staleness");
            metrics.Reset(42);metrics.Feed("Application,ProcessID,SwapChainAddress,MsBetweenPresents");metrics.Feed("\"Game, Demo.exe\",42,0x1,16.0");metrics.Feed("Other.exe,43,0x1,1.0");metrics.Feed("Game.exe,42,0x1,NaN");Assert(metrics.Read().Count==1,"FPS CSV filtering");
            metrics.Reset(42);metrics.Feed("Application,ProcessID,SwapChainAddress,Runtime,SyncInterval,PresentFlags,Dropped,TimeInSeconds,msInPresentAPI,msBetweenPresents,AllowsTearing,PresentMode,msUntilRenderComplete,msUntilDisplayed,msBetweenDisplayChange,msFlipDelay,msUntilRenderStart,msGPUActive,msSinceInput");
            metrics.Feed("Game.exe,42,0x1,D3D9,-1,0,0,0.2659932,0.6495,10,0,Composed: Copy with GPU GDI,0.6092,15.6029,0,0,-0.4862,0.2584,0");Assert(metrics.Read().Ready&&metrics.Read().Current==100,"Actual PresentMon v1 header casing");
        }
        var rect=new GameOverlay.Rect {Left=-1920,Top=0,Right=0,Bottom=1080};foreach(string position in new[]{"top-left","top","top-right","bottom-left","bottom","bottom-right"}){var p=GameOverlay.Anchor(rect,300,80,position);Assert(p.X>=-1920&&p.X+300<=0&&p.Y>=0&&p.Y+80<=1080,"Overlay anchor bounds");}
        Assert(Languages.Resolve("auto","zh-HK")=="zh-TW"&&Languages.Resolve("auto","zh-CN")=="zh-CN"&&Languages.Resolve("auto","de-DE")=="en","Auto system language");
        var translations=new Languages(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages.txt"));
        translations.Preference="zh-CN";Assert(translations.T("Auto (foreground app)")=="自动（前台应用）","Simplified Chinese foreground target label");
        translations.Preference="zh-TW";Assert(translations.T("Auto (foreground app)")=="自動（前景應用程式）","Traditional Chinese foreground target label");
        Assert(translations.T("System glass background is unavailable on this Windows version.")=="此 Windows 版本不支援系統玻璃背景。","Glass message must not include another translation entry");
    }
    static void Capture(Shell shell,string path){shell.Window.UpdateLayout();var bitmap=new RenderTargetBitmap((int)shell.Window.ActualWidth,(int)shell.Window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(shell.Window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))encoder.Save(file);}
    [STAThread] static int Main(string[] args){
        try{
            if(args.Length<2||(args[1]!="bench"&&args[1]!="perf"))NativeStartupTests.Run();
            string root=AppDomain.CurrentDomain.BaseDirectory,state=Path.GetFullPath(args[0]);Directory.CreateDirectory(state);
            var paths=new PulsePaths(root,state,Path.Combine(state,"runtime"));Json.WriteAtomic(paths.Snapshot,Snapshot());
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
            if(args.Length>1&&args[1]=="perf"){
                AppDomain.MonitoringIsEnabled=true;
                using(var shell=new Shell(paths,true)){
                    shell.Window.ShowInTaskbar=false;shell.Window.ShowActivated=false;shell.Window.Width=310;shell.Window.Height=690;shell.Show();Pump();
                    Field<DispatcherTimer>(shell,"poll").Stop();
                    foreach(bool hidden in new[]{false,true}){
                        if(hidden)shell.Window.Hide();
                        for(int i=0;i<20;i++){shell.UpdatePanel();Pump();}
                        long allocated=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;var elapsed=Stopwatch.StartNew();
                        for(int i=0;i<300;i++){var snapshot=Snapshot();snapshot.sequence=i+2;snapshot.sensors[0].value=50+i%40;Json.WriteAtomic(paths.Snapshot,snapshot);shell.UpdatePanel();Pump();}
                        elapsed.Stop();process.Refresh();
                        Console.WriteLine(Json.Serializer().Serialize(new {hidden=hidden,updates=300,allocatedBytes=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-allocated,cpuMs=(process.TotalProcessorTime-cpu).TotalMilliseconds,elapsedMs=elapsed.Elapsed.TotalMilliseconds,workingSet=process.WorkingSet64,privateBytes=process.PrivateMemorySize64}));
                    }
                    shell.Show();Pump();Assert(shell.Control<TextBlock>("Status").Text.Contains("Live"),"Restore must display fresh readings");shell.Exit();
                }app.Shutdown();return 0;
            }
            if(args.Length>1&&args[1]=="bench"){
                using(var bench=new Shell(paths,true)){
                    bench.Window.ShowInTaskbar=false;bench.Window.ShowActivated=false;bench.Window.Width=310;bench.Window.Height=690;
                    bench.Window.ContentRendered+=delegate{Json.WriteAtomic(Path.Combine(state,"ready.json"),new {ready=DateTimeOffset.Now.ToString("o")});};bench.Show();bench.UpdatePanel();
                    var stop=new DispatcherTimer {Interval=TimeSpan.FromSeconds(2)};stop.Tick+=delegate{if(File.Exists(Path.Combine(state,"BENCH-STOP"))){stop.Stop();bench.Exit();app.Shutdown();}};stop.Start();app.Run();
                }return 0;
            }
            SharedFeatures();
            Json.WriteAtomic(Path.Combine(state,"widget-settings.json"),new {width=310,height=690,left=90,top=70,fontSize=12,language="en",unknownMigrationField="keep",overlay=new {enabled=true,processName="PulseTestGameNotRunning",background="#223344",opacity=37},cardOrder=new[]{"GPU","CPU","Memory","NVMe","Airflow"}});
            using(var shell=new Shell(paths,true)){
                shell.Window.ShowInTaskbar=false;shell.Window.ShowActivated=false;shell.Show();Pump();shell.UpdatePanel();Pump();
                Assert(shell.Control<TextBlock>("Status").Text.Contains("7 "),"Native mapped sensor count");
                shell.Window.Hide();var hiddenSnapshot=Snapshot();hiddenSnapshot.sequence=900;hiddenSnapshot.sensors[0].value=87;Json.WriteAtomic(paths.Snapshot,hiddenSnapshot);shell.UpdatePanel();
                Assert(Field<ReadingSession>(shell,"readings").Peaks["cpu"]==87,"Hidden window lost session peak");
                var hiddenStale=Snapshot();hiddenStale.time=DateTimeOffset.Now.AddSeconds(-30).ToString("o");Json.WriteAtomic(paths.Snapshot,hiddenStale);shell.UpdatePanel();
                Assert(Field<ReadingSession>(shell,"readings").Latest.state=="STALE","Hidden window lost stale-state detection");
                var restoredSnapshot=Snapshot();restoredSnapshot.sequence=901;restoredSnapshot.sensors[0].value=63;Json.WriteAtomic(paths.Snapshot,restoredSnapshot);shell.Show();Pump();
                Assert(Field<ReadingSession>(shell,"readings").Latest.values["cpu"]==63&&shell.Control<TextBlock>("Status").Text.Contains("Live"),"Restore did not immediately refresh snapshot");
                var cards=shell.Control<StackPanel>("Cards");Assert(cards.Children.Count==5,"Five card owners preserved");
                Assert((string)((Border)cards.Children[0]).Tag=="GPU"&&shell.Window.Left==90&&shell.Window.Top==70,"Legacy card order/desktop position migration");
                Assert(!Field<DispatcherTimer>(shell,"overlayTimer").IsEnabled,"Idle overlay timer running");
                var fpsOverlay=Field<GameOverlay>(shell,"overlay");
                var fpsBackground=(SolidColorBrush)((Border)fpsOverlay.Content).Background;
                Assert(fpsBackground.Color.R==0x22&&fpsBackground.Color.G==0x33&&fpsBackground.Color.A==94,"Overlay appearance restore");
                shell.Control<Slider>("OverlayOpacity").Value=0;
                Assert(((SolidColorBrush)((Border)fpsOverlay.Content).Background).Color.A==0&&fpsOverlay.Opacity==1&&((Border)fpsOverlay.Content).Child.Opacity==1,"Transparent background faded overlay text");
                shell.Control<Slider>("OverlayOpacity").Value=100;Assert(((SolidColorBrush)((Border)fpsOverlay.Content).Background).Color.A==255,"Opaque overlay background");
                shell.Control<Slider>("OverlayOpacity").Value=37;
                Assert(shell.Control<CheckBox>("OverlayEnabled").IsChecked==true,"Overlay enabled preference was erased at startup");
                Click(shell,"RefreshGames");Assert((string)((ComboBoxItem)shell.Control<ComboBox>("GamePicker").SelectedItem).Tag=="PulseTestGameNotRunning","Refresh lost stopped game selection");
                Assert((string)((ComboBoxItem)shell.Control<ComboBox>("GamePicker").Items[0]).Tag=="","Auto target default missing");
                Toggle(shell,"LockPosition",true);Assert(shell.Window.ResizeMode==ResizeMode.NoResize&&(bool)shell.Window.GetValue(WindowSnap.PositionLockedProperty),"Window lock");Assert(!Tree(cards).OfType<System.Windows.Controls.Primitives.Thumb>().Any(t=>t.IsEnabled),"Locked card handles enabled");
                var first=(Border)cards.Children[0];((MenuItem)first.ContextMenu.Items[1]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Assert(cards.Children[0]==first,"Locked card reordered");Toggle(shell,"LockPosition",false);
                ((MenuItem)first.ContextMenu.Items[1]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Assert(cards.Children[1]==first,"Unlocked card reorder failed");
                var cardChoice=shell.Control<StackPanel>("CardOptions").Children.OfType<CheckBox>().Single(c=>c.Content.ToString()=="GPU");cardChoice.IsChecked=false;cardChoice.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert(first.Visibility==Visibility.Collapsed,"Card hide ignored");cardChoice.IsChecked=true;cardChoice.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert(first.Visibility==Visibility.Visible,"Card show ignored");
                shell.Control<Slider>("OpacitySlider").Value=0;Pump();if(shell.Control<Slider>("OpacitySlider").IsEnabled){Assert(shell.Window.Opacity==1&&((SolidColorBrush)shell.Window.Background).Color.A==0,"Zero background opacity faded text or stayed opaque");}shell.Control<Slider>("OpacitySlider").Value=70;
                shell.Control<Slider>("OpacitySlider").Value=20;Toggle(shell,"LockPosition",true);
                if(shell.Control<Slider>("OpacitySlider").IsEnabled){
                    Assert(((SolidColorBrush)shell.Window.Background).Color.A==13,"Locked monitor effective opacity");
                    Click(shell,"Settings");Assert(((SolidColorBrush)shell.Window.Background).Color.A==51,"Settings must restore saved opacity");Click(shell,"Back");
                    shell.Save();Assert(new Settings(Path.Combine(state,"widget-settings.json")).Number("opacity",0,0,100)==20,"Lock persisted temporary effective opacity");
                }
                Toggle(shell,"LockPosition",false);shell.Control<Slider>("OpacitySlider").Value=70;
                var gpuCard=cards.Children.Cast<Border>().Single(c=>(string)c.Tag=="GPU");var gpuHeader=(Grid)((StackPanel)gpuCard.Child).Children[0];var gpuHero=(TextBlock)gpuHeader.Children[1];Assert(gpuHero.Text=="49.0 °C"&&gpuHero.Visibility==Visibility.Visible,"GPU temperature visibility: "+gpuHero.Text+" "+gpuHero.Visibility);
                foreach(double size in new[]{10d,12d,16d})foreach(double width in new[]{240d,310d}){
                    shell.Control<Slider>("FontSizeSlider").Value=size;shell.Window.Width=width;shell.Window.Height=690;Pump();shell.UpdatePanel();Pump();
                    foreach(var card in cards.Children.Cast<Border>()){
                        var header=((StackPanel)card.Child).Children[0] as Grid;if(header.Children.Count<2)continue;var title=(FrameworkElement)header.Children[0];var value=(FrameworkElement)header.Children[1];if(value.Visibility!=Visibility.Visible)continue;Assert(value.ActualWidth>5 && value.ActualHeight>5,"Hero has no layout area: "+card.Tag);var a=title.TranslatePoint(new Point(),header);var b=value.TranslatePoint(new Point(),header);
                        Assert(b.Y>=a.Y+title.ActualHeight-1||b.X>=a.X+title.ActualWidth-1,"Header overlap");Assert(b.X+value.ActualWidth<=header.ActualWidth+1,"Header clipping");
                    }
                }
                shell.Control<Slider>("FontSizeSlider").Value=12;shell.Window.Width=310;Pump();Click(shell,"Details");Capture(shell,Path.Combine(state,"native-details.png"));Click(shell,"Details");Capture(shell,Path.Combine(state,"native-compact.png"));
                // Desktop mode owns separate layout preferences and reuses this ReadingSession.
                double originalFont=shell.Window.FontSize,originalLeft=shell.Window.Left;
                Toggle(shell,"DesktopEnabled",true);Pump();shell.UpdatePanel();
                var desktop=Field<DesktopView>(shell,"desktop");
                Assert(desktop!=null&&desktop.Locked&&desktop.IsVisible,"Desktop did not open locked");
                Assert(Tree(desktop).OfType<TextBlock>().Any(t=>t.Text.Contains("49 °C")),"Desktop did not use existing GPU readings");
                Assert(!Tree(desktop).OfType<Button>().Any(),"Desktop contains window buttons");
                Assert(Tree(desktop).OfType<TextBlock>().Any(t=>t.Text=="VRAM")&&Tree(desktop).OfType<TextBlock>().Any(t=>t.Text=="2 / 8 GB · 25%"),"Desktop VRAM usage missing");
                var fanSnapshot=Snapshot();fanSnapshot.sequence=20;fanSnapshot.sensors=fanSnapshot.sensors.Concat(new[]{
                    new Sensor{id="/gpu/fan/0",hardwareId="/gpu",hardwareType="GpuNvidia",hardware="Demo GPU",name="GPU Fan 1",type="Fan",value=700},
                    new Sensor{id="/gpu/fan/1",hardwareId="/gpu",hardwareType="GpuNvidia",hardware="Demo GPU",name="GPU Fan 2",type="Fan",value=0}
                }).ToArray();Json.WriteAtomic(paths.Snapshot,fanSnapshot);shell.UpdatePanel();Pump();
                var fanLabels=Tree(desktop).OfType<TextBlock>().Select(t=>t.Text).ToArray();
                Assert(fanLabels.Contains("GPU Fan 1")&&fanLabels.Contains("GPU Fan 2")&&fanLabels.Contains("700 RPM")&&fanLabels.Contains("0 RPM"),"Desktop must identify both GPU fan channels, including zero readings");
                fanSnapshot.sequence++;fanSnapshot.sensors=fanSnapshot.sensors.Where(s=>s.name!="GPU Fan 2").ToArray();Json.WriteAtomic(paths.Snapshot,fanSnapshot);shell.UpdatePanel();Pump();
                fanLabels=Tree(desktop).OfType<TextBlock>().Select(t=>t.Text).ToArray();Assert(fanLabels.Contains("GPU Fan")&&!fanLabels.Contains("GPU Fan 2"),"Single GPU fan label/capability did not update");
                Json.WriteAtomic(paths.Snapshot,Snapshot());shell.UpdatePanel();Pump();
                shell.Control<Slider>("DesktopFontSize").Value=24;shell.Control<Slider>("DesktopSpacing").Value=26;Pump();
                Assert(shell.Window.FontSize==originalFont&&shell.Window.Left==originalLeft,"Desktop settings changed monitor layout");
                Assert(Tree(desktop).OfType<TextBlock>().Where(t=>t.Visibility==Visibility.Visible).All(t=>t.FontSize==24),"Desktop font was not applied");
                var desktopBitmap=new RenderTargetBitmap((int)desktop.ActualWidth,(int)desktop.ActualHeight,96,96,PixelFormats.Pbgra32);desktopBitmap.Render(desktop);var desktopEncoder=new PngBitmapEncoder();desktopEncoder.Frames.Add(BitmapFrame.Create(desktopBitmap));using(var file=File.Create(Path.Combine(state,"desktop-mode.png")))desktopEncoder.Save(file);
                Assert(DesktopContrast.Choose(0,"#152127")=="#F5F7FA"&&DesktopContrast.Choose(1,"#F5F7FA")=="#152127","Wallpaper contrast selection");
                Assert(DesktopContrast.Choose(.19,"#152127")=="#152127"&&DesktopContrast.Choose(.19,"#F5F7FA")=="#F5F7FA","Animated wallpaper hysteresis");
                Assert(DesktopContrast.Outline(Colors.White)==Colors.Black&&DesktopContrast.Outline(Colors.Black)==Colors.White,"Custom text contrast outline");
                Toggle(shell,"DesktopAutoContrast",false);shell.Control<Slider>("DesktopTextOpacity").Value=65;Pump();
                Assert(((Border)desktop.Content).Child.Opacity==.65,"Desktop text opacity not applied");
                Assert(desktop.ResolveColor(false,"#4488CC")=="#4488CC","Auto Contrast overrode custom color");
                Toggle(shell,"DesktopLocked",false);Assert(!desktop.Locked,"Desktop unlock failed");
                Click(shell,"DesktopDone");Assert(desktop.Locked&&!shell.Window.IsVisible,"Done did not lock desktop and hide editor");
                shell.Show();shell.ShowSettings(true);Toggle(shell,"DesktopEnabled",false);Assert(Field<DesktopView>(shell,"desktop")==null&&shell.Window.IsVisible,"Desktop disable did not restore monitor");
                var clamped=DesktopView.Clamp(new Point(-1800,80),new Size(300,200),new[]{new Rect(-1920,0,1920,1080),new Rect(0,0,1920,1080)});
                Assert(clamped.X==-1800&&clamped.Y==80,"Negative monitor position was lost");
                clamped=DesktopView.Clamp(new Point(8000,8000),new Size(300,200),new[]{new Rect(0,0,1920,1080)});
                Assert(clamped.X==1620&&clamped.Y==880,"Disconnected monitor recovery failed");
                shell.Save();var desktopSettings=new Settings(Path.Combine(state,"widget-settings.json"));
                Assert(desktopSettings.Number("desktopFontSize",0,10,32)==24&&desktopSettings.Number("desktopSpacing",0,4,40)==26,"Desktop preferences did not persist");
                var stale=Snapshot();stale.time=DateTimeOffset.Now.AddSeconds(-30).ToString("o");Json.WriteAtomic(paths.Snapshot,stale);shell.UpdatePanel();Assert(!shell.Control<TextBlock>("Status").Text.Contains("Live")&&gpuHero.Text=="—"&&gpuCard.Visibility==Visibility.Visible,"Stale readings/capability retention");Assert((string)gpuCard.ToolTip=="Demo GPU","Stale card lost device label");Json.WriteAtomic(paths.Snapshot,Snapshot());shell.UpdatePanel();Click(shell,"Max");Assert(gpuHero.Text=="49.0 °C","Session peaks lost");Click(shell,"Live");
                Click(shell,"Settings");var language=shell.Control<ComboBox>("LanguagePicker");foreach(ComboBoxItem item in language.Items)if((string)item.Tag=="zh-TW")language.SelectedItem=item;Pump();Assert(shell.Control<Button>("Live").Content.ToString()=="即時","Native language selection");
                Click(shell,"CheckUpdates");var updateWait=Stopwatch.StartNew();while(!shell.Control<Button>("CheckUpdates").IsEnabled && updateWait.Elapsed.TotalSeconds<15){Pump();System.Threading.Thread.Sleep(30);}Assert(shell.Control<Button>("CheckUpdates").IsEnabled,"Update check did not complete");Assert(shell.Control<TextBlock>("UpdateStatus").Text=="You are up to date" || shell.Control<TextBlock>("UpdateStatus").Text=="已是最新版本" || shell.Control<TextBlock>("UpdateStatus").Text=="已是最新版本", "Native update check: "+shell.Control<TextBlock>("UpdateStatus").Text);foreach(ComboBoxItem choice in language.Items)if((string)choice.Tag=="en")language.SelectedItem=choice;Pump();Assert(shell.Control<TextBlock>("UpdateStatus").Text=="You are up to date","Update status did not follow language change");foreach(ComboBoxItem choice in language.Items)if((string)choice.Tag=="zh-TW")language.SelectedItem=choice;Pump();Capture(shell,Path.Combine(state,"localized-settings.png"));shell.Control<Slider>("FontSizeSlider").Value=14;shell.Save();Click(shell,"Back");shell.Window.Close();Assert(!shell.Window.IsVisible,"Close to tray");shell.Show();Pump();Assert(shell.Window.IsVisible,"Restore window");shell.Exit();
            }
            using(var restored=new Shell(paths,true)){restored.Window.ShowInTaskbar=false;restored.Show();Pump();Assert(restored.Control<Slider>("OverlayOpacity").Value==37&&restored.Control<Button>("OverlayBackground").Content.ToString()=="#223344","Overlay appearance persistence");Click(restored,"ResetOverlayAppearance");Assert(restored.Control<Slider>("OverlayOpacity").Value==80&&restored.Control<Button>("OverlayBackground").Content.ToString()=="#111923","Overlay appearance reset");Assert(restored.Window.FontSize==14,"Font restore");Assert(restored.Control<Button>("Live").Content.ToString()=="即時","Language restore");var picker=restored.Control<ComboBox>("LanguagePicker");foreach(ComboBoxItem item in picker.Items)if((string)item.Tag=="en")picker.SelectedItem=item;Assert(restored.Control<StackPanel>("CardOptions").Children.OfType<CheckBox>().Any(c=>c.Content.ToString()=="Memory"),"Restored card options cannot switch back to English");restored.Exit();}
            Assert(new Settings(Path.Combine(state,"widget-settings.json")).Text("unknownMigrationField")=="keep","Upgrade lost unknown settings");
            var references=Assembly.LoadFrom(Path.Combine(root,"HardwarePulse.exe")).GetReferencedAssemblies();Assert(!references.Any(r=>r.Name=="System.Management.Automation"),"Automation reference remains");
            Console.WriteLine("PASS native WPF: semantic snapshot, 10/12/16 DIP headers, Details, language, settings restore, tray and no automation reference");app.Shutdown();return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
