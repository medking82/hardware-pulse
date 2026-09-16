using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse;
using HardwarePulse.Desktop;

static class GameOverlayTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Until(Func<bool> done,string message){var end=DateTime.UtcNow.AddSeconds(8);while(!done()&&DateTime.UtcNow<end){using var slice=new CancellationTokenSource(TimeSpan.FromMilliseconds(20));Dispatcher.UIThread.MainLoop(slice.Token);}Check(done(),message);}
    sealed class Source : IDesktopGameSource {
        public int Calls,Captures,Disposed;
        public bool Hold;
        public readonly ManualResetEventSlim Entered=new(),Release=new();
        public FrameMetrics Poll(string target)=>throw new Exception("Game source must publish target with metrics");
        public DesktopGameFrame PollGame(string target,bool capture){Calls++;if(capture)Captures++;if(Hold){Entered.Set();Release.Wait(TimeSpan.FromSeconds(5));}return new(new(){Ready=capture,Current=73,Average=70,Minimum=40,Low=30,Status=capture?"Live":"FPS capture stopped"},(nint)123,target);}
        public void Reset(){}
        public void Dispose()=>Disposed++;
    }
    public static void Run() {
        var source=new Source();var panel=new FpsPanel(new("en"),factory:()=>source,discover:()=>[]);
        Check(source.Calls==0,"Disabled overlay accessed source");
        panel.Target="FixtureGame";panel.TrackGame=true;
        Until(()=>panel.Current.TargetWindow==(nint)123,"Hardware-only target not published");
        Check(!panel.Current.Enabled&&panel.Current.Current=="—"&&source.Captures==0&&panel.Current.TargetName=="FixtureGame","Hardware-only overlay captured FPS or lost target");
        panel.Enabled=true;Until(()=>panel.Current.Current=="73","FPS enabling did not publish metrics");
        Check(panel.Current.TargetWindow==(nint)123&&panel.Current.TargetName=="FixtureGame"&&source.Captures>0,"FPS/target snapshot diverged");
        panel.TrackGame=false;Check(panel.Enabled&&panel.Current.Current=="73","Overlay off reset active FPS");
        panel.Enabled=false;Until(()=>panel.Sampling.IsCompleted,"Combined session did not stop");
        Check(panel.Current.TargetWindow==0&&source.Disposed==2,"Stale target or undisposed session");panel.Dispose();
        var late=new Source{Hold=true};var pending=new FpsPanel(new("en"),factory:()=>late,discover:()=>[]);
        pending.TrackGame=true;Until(()=>late.Entered.IsSet,"Pending target poll never entered");pending.TrackGame=false;late.Release.Set();
        Until(()=>pending.Sampling.IsCompleted,"Pending overlay session did not complete");
        Check(pending.Current.TargetWindow==0&&!pending.Current.Enabled&&late.Disposed==1&&late.Captures==0,"Late target escaped cancellation");pending.Dispose();

        var options=new GameOverlayOptions{Enabled=true,Detailed=true,Fans=true,Storage=true};
        var hardware=new MonitorSnapshot("24%","8 / 32 GiB","0","0",true,true){WindowsHardware=[new("cpu","CPU temperature","55 °C"),new("gpuLoad","GPU utilization","60%"),new("gpu","GPU temperature","65 °C"),new("cpuFan","CPU Fan","900 RPM"),new("diskC","Drive 1","40 °C"),new("vcore","Vcore","1.2 V"),new("vramUsage","GPU memory","3 / 8 GiB")]};
        var fps=new DesktopFpsSnapshot(true,"Live","73","70","40","30"){TargetName="FixtureGame"};
        string message=options.Message(hardware,fps,new("en"));
        foreach(string expected in new[]{"FixtureGame","FPS 73","24%","55 °C","60%","65 °C","8 / 32 GiB","900 RPM","40 °C","1.2 V","3 / 8 GiB"})Check(message.Contains(expected),"Overlay omitted "+expected);
        options.Detailed=false;Check(!options.Message(hardware,fps,new("en")).Contains('\n'),"Compact overlay is multiline");
        Check(options.Message(null,new(false,"FPS capture stopped"),new("en")).Contains('—'),"Unavailable readings invented");
        options.Cpu=options.Gpu=options.Memory=options.Fans=options.Storage=options.Fps=false;Check(options.Message(hardware,fps,new("en"))=="Pulse","All disabled groups retained stale text");
        var bounds=new PixelRect(-1000,100,800,600);
        var expectedPositions=new[]{new PixelPoint(-988,112),new PixelPoint(-700,112),new PixelPoint(-412,112),new PixelPoint(-988,588),new PixelPoint(-700,588),new PixelPoint(-412,588)};
        for(int i=0;i<6;i++)Check(GameOverlayWindow.Anchor(bounds,200,100,GameOverlayOptions.Positions[i])==expectedPositions[i],"Anchor mismatch");
        string directory=Directory.CreateTempSubdirectory("pulse-game-settings-").FullName;
        try {
            string path=Path.Combine(directory,"settings.json");var store=new PreviewSettingsStore(path);var settings=store.Load();Check(!settings.GameOverlay.Enabled,"Overlay must be opt-in");
            settings.GameOverlay=new(){Enabled=true,Position="bottom-right",Opacity=33,Background="#123456",Fans=true};Check(store.Save(settings),"Overlay save");
            var restored=new PreviewSettingsStore(path).Load().GameOverlay;Check(restored.Enabled&&restored.Position=="bottom-right"&&restored.Opacity==33&&restored.Background=="#123456"&&restored.Fans,"Overlay persistence");
            using var bad=JsonDocument.Parse("{\"Enabled\":true,\"Position\":\"wrong\",\"Opacity\":999,\"Background\":null}");var normalized=GameOverlayOptions.Read(bad.RootElement);
            Check(normalized.Position=="top-left"&&normalized.Opacity==100&&normalized.Background=="#111923","Overlay option normalization");
            if(OperatingSystem.IsWindows()) {
                var uiSource=new Source();var uiFps=new FpsPanel(new("en"),factory:()=>uiSource,discover:()=>[]);
                var uiOptions=new GameOverlayOptions();settings.GameOverlay=uiOptions;
                var controls=new GameOverlayPanel(new("en"),uiOptions,uiFps,()=>Check(store.Save(settings),"UI preference save"));
                var view=new Window{Content=new ScrollViewer{Content=controls},Width=400,Height=750};
                try {
                    view.Show();Dispatcher.UIThread.RunJobs();
                    controls.GetVisualDescendants().OfType<CheckBox>().Single(x=>x.Name=="GameOverlayEnabled").IsChecked=true;
                    Until(()=>uiSource.Calls>0,"Overlay toggle did not start target tracking");Check(uiSource.Captures==0&&!uiFps.Enabled,"Overlay toggle enabled capture without FPS opt-in");
                    controls.GetVisualDescendants().OfType<CheckBox>().Single(x=>x.Name=="GameOverlayCpu").IsChecked=false;
                    controls.GetVisualDescendants().OfType<ComboBox>().Single(x=>x.Name=="GameOverlayPosition").SelectedItem="bottom-center";
                    controls.GetVisualDescendants().OfType<TextBox>().Single(x=>x.Name=="GameOverlayBackground").Text="#123456";
                    controls.GetVisualDescendants().OfType<Slider>().Single(x=>x.Name=="GameOverlayOpacity").Value=25;
                    Dispatcher.UIThread.RunJobs();
                    var savedUi=new PreviewSettingsStore(path).Load().GameOverlay;
                    Check(savedUi.Enabled&&!savedUi.Cpu&&savedUi.Position=="bottom-center"&&savedUi.Background=="#123456"&&savedUi.Opacity==25,"Overlay controls did not persist independently");
                    controls.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="ResetGameOverlayAppearance").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(uiOptions.Background=="#111923"&&uiOptions.Opacity==80&&!uiOptions.Cpu,"Appearance reset changed metric selection");
                }finally{controls.Dispose();uiFps.Dispose();view.Close();}
                Until(()=>uiFps.Sampling.IsCompleted,"Overlay settings close leaked session");
            }
        }finally{Directory.Delete(directory,true);}
        Console.WriteLine("PASS game overlay: shared target/metrics, hardware-only capture off, independent lifetime, content, anchors and settings");
    }
    public static void Native(string? output=null) {
        if(!OperatingSystem.IsWindows())return;
        var target=new Window{Title="Pulse overlay target fixture",Width=700,Height=480,Background=Brushes.DarkBlue,Position=new(150,150)};
        var other=new Window{Title="Pulse overlay foreground fixture",Width=300,Height=200,Position=new(900,150)};
        var overlay=new GameOverlayWindow();var options=new GameOverlayOptions{Enabled=true};
        try {
            target.Show();target.Activate();nint handle=target.TryGetPlatformHandle()!.Handle;
            Until(()=>GameOverlayWindow.TargetBounds(handle,out _),"Fixture target could not become foreground");
            overlay.Display(handle,"FPS 73 · CPU 24%",options);
            Until(()=>overlay.IsVisible&&overlay.InputReady&&overlay.Bounds.Width>10,"Overlay not visible/pass-through ready");
            Check(GetForegroundWindow()==handle,"Overlay stole foreground");
            if(output!=null) {
                using var bitmap=new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)Math.Ceiling(overlay.Bounds.Width*overlay.RenderScaling),(int)Math.Ceiling(overlay.Bounds.Height*overlay.RenderScaling)),new Vector(96*overlay.RenderScaling,96*overlay.RenderScaling));
                bitmap.Render(overlay);bitmap.Save(output,Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            }
            long flags=GetWindowLongPtrW(overlay.TryGetPlatformHandle()!.Handle,-20).ToInt64();
            Check((flags&0x08080020)==0x08080020,"Owned overlay lost native pass-through/no-activate flags");
            Until(()=>GetAncestor(WindowFromPoint(new NativePoint{X=overlay.Position.X+20,Y=overlay.Position.Y+20}),2)==handle,"Overlay intercepted native hit testing");
            foreach(string position in GameOverlayOptions.Positions) {
                options.Position=position;overlay.Display(handle,"FPS 73 · CPU 24%",options);
                Check(GameOverlayWindow.TargetBounds(handle,out var rect),"Target disappeared");
                Until(()=>overlay.Position==GameOverlayWindow.Anchor(rect,(int)Math.Ceiling(overlay.Bounds.Width*overlay.RenderScaling),(int)Math.Ceiling(overlay.Bounds.Height*overlay.RenderScaling),position),"Native anchor mismatch");
            }
            target.Position=new(230,220);Until(()=>GameOverlayWindow.TargetBounds(handle,out var moved)&&moved.X>=230,"Target move not acknowledged");overlay.Display(handle,"moved",options);
            other.Show();other.Activate();Until(()=>GetForegroundWindow()==other.TryGetPlatformHandle()!.Handle,"Other fixture foreground");
            overlay.Display(handle,"hidden",options);Check(!overlay.IsVisible,"Overlay remained on another foreground app");
            target.Activate();Until(()=>GetForegroundWindow()==handle,"Target restore foreground");overlay.Display(handle,"restored",options);
            Check(overlay.IsVisible&&overlay.InputReady&&GetForegroundWindow()==handle,"Overlay restore lost pass-through or activation policy");
            target.WindowState=WindowState.Minimized;Until(()=>!GameOverlayWindow.TargetBounds(handle,out _),"Target minimize not observed");overlay.Display(handle,"minimized",options);Check(!overlay.IsVisible,"Overlay visible over minimized target");
        }finally{overlay.Close();other.Close();target.Close();}
        Console.WriteLine("PASS native game overlay: six anchors, move, foreground/minimize hide, restore, no-activation and input flags");
    }
    [DllImport("user32.dll")] static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] static extern nint GetWindowLongPtrW(nint window,int index);
    [StructLayout(LayoutKind.Sequential)] struct NativePoint {public int X,Y;}
    [DllImport("user32.dll")] static extern nint WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")] static extern nint GetAncestor(nint window,uint flags);
}
