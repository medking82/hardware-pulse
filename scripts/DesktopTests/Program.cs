using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class Tests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void NativeStep(string name,Action test) {
        var elapsed=System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"NATIVE_TEST_BEGIN {name}");Console.Out.Flush();
        try {test();}
        catch {Console.WriteLine($"NATIVE_TEST_FAIL {name} elapsedMs={elapsed.ElapsedMilliseconds}");Console.Out.Flush();throw;}
        Console.WriteLine($"NATIVE_TEST_PASS {name} elapsedMs={elapsed.ElapsedMilliseconds}");Console.Out.Flush();
    }
    [STAThread]
    static void Main(string[] args) {
        if(args.Length>0&&args[0]=="--ui-benchmark"){Environment.ExitCode=UiBenchmark.Run(args);return;}
        var expected=Environment.GetEnvironmentVariable("PULSE_TEST_ARCH");
        Check(expected==null||expected==System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),"Architecture mismatch");
        if(args.Length==2&&args[0]=="--startup-fixture"){Environment.ExitCode=StartupModeTests.Child(args[1]);return;}
        if(args.SequenceEqual(new[]{"--startup-native"})){StartupModeTests.Native();return;}
        if(args.SequenceEqual(new[]{"--update-live-metadata"})){DesktopUpdateTests.LiveMetadata();return;}
        if(args.SequenceEqual(new[]{"--windows-hardware-live-native"})){HardwarePulse.Desktop.Program.BuildApp().SetupWithoutStarting();WindowsHardwareTests.Live();return;}
        if(args.SequenceEqual(new[]{"--layer-native"})){HardwarePulse.Desktop.Program.BuildApp().SetupWithoutStarting();WindowsLayerTests.Native();return;}
        if(args.SequenceEqual(new[]{"--capture-native"})){HardwarePulse.Desktop.Program.BuildApp().SetupWithoutStarting();WindowsCaptureTests.Native();return;}
        if(args.SequenceEqual(new[]{"--input-native"})){HardwarePulse.Desktop.Program.BuildApp().SetupWithoutStarting();WindowsInputTests.Run();return;}
        if(args.Length>=1&&args[0]=="--game-overlay-native"){HardwarePulse.Desktop.Program.BuildApp().SetupWithoutStarting();GameOverlayTests.Native(args.Length==2?args[1]:null);return;}
        if(args.SequenceEqual(new[]{"--contrast-native"})){HardwarePulse.Desktop.Program.BuildApp().SetupWithoutStarting();LocalContrastTests.Native();return;}
        if(args.Length==2&&args[0]=="--instance-secondary"){Environment.ExitCode=WindowsInstanceTests.Secondary(args[1]);return;}
        if(args.Length==2&&args[0]=="--instance-owner"){Environment.ExitCode=WindowsInstanceTests.Hold(args[1]);return;}
        if(args.SequenceEqual(new[]{"--instance-native"})){HardwarePulse.Desktop.Program.BuildApp().SetupWithoutStarting();WindowsInstanceTests.Native();return;}
        if(args.SequenceEqual(new[]{"--locked-layout-native"})) {HardwarePulse.Desktop.Program.BuildApp().SetupWithoutStarting();LockedLayoutTests.Native();return;}
        if(args.SequenceEqual(new[]{"--shortcuts-native"})) {HardwarePulse.Desktop.Program.BuildApp().SetupWithoutStarting();DesktopShortcutTests.Native();return;}
        if(args.SequenceEqual(new[]{"--layouts-native"})) {HardwarePulse.Desktop.Program.BuildApp().SetupWithoutStarting();ReadingLayoutTests.Run(native:true);return;}
        if(args.SequenceEqual(new[]{"--sampling-reopen-native"})) {HardwarePulse.Desktop.Program.BuildApp().SetupWithoutStarting();NativeStep("sampling-reopen",SamplingRecoveryTests.Reopen);return;}
        if(args.SequenceEqual(new[]{"--tray-reopen-native"})) {HardwarePulse.Desktop.Program.BuildApp().SetupWithoutStarting();NativeStep("tray",()=>TrayTests.Run(native:true));NativeStep("sampling-reopen",SamplingRecoveryTests.Reopen);return;}
        if(args.SequenceEqual(new[]{"--native-session"})) {
            HardwarePulse.Desktop.Program.BuildApp().SetupWithoutStarting();
            NativeStep("visibility",()=>SamplingRecoveryTests.Visibility());
            NativeStep("profile-migration",ProfileMigrationTests.Run);
            NativeStep("settings",()=>SettingsTests.Run(null));
            NativeStep("localization",()=>LocalizationTests.Run(null,native:true));
            NativeStep("tray",()=>TrayTests.Run(native:true));
            NativeStep("sampling-reopen",SamplingRecoveryTests.Reopen);
            NativeStep("floating-monitor",()=>FloatingMonitorTests.Run());
            NativeStep("windows-hardware",()=>WindowsHardwareTests.Run(null));
            NativeStep("fps",()=>FpsPanelTests.Run());
            NativeStep("game-overlay",()=>{GameOverlayTests.Run();GameOverlayTests.Native();});
            NativeStep("shortcuts",DesktopShortcutTests.Native);
            NativeStep("reading-layout",()=>ReadingLayoutTests.Run(native:true));
            NativeStep("locked-layout",LockedLayoutTests.Native);
            NativeStep("instance",WindowsInstanceTests.Native);
            NativeStep("startup-mode",StartupModeTests.Native);
            NativeStep("startup-controls",DesktopStartupTests.Run);
            NativeStep("updates",DesktopUpdateTests.Run);
            NativeStep("windows-layer",WindowsLayerTests.Native);
            NativeStep("windows-capture",WindowsCaptureTests.Native);
            NativeStep("contrast",LocalContrastTests.Native);
            NativeStep("windows-input",WindowsInputTests.Run);
            NativeStep("mac-input",MacInputTests.Run);
            NativeStep("mac-material",MacMaterialTests.Run);
            NativeStep("x11-input",X11InputTests.Run);
            Console.WriteLine("PASS native Desktop session: isolated settings, language, theme, restore, tray commands and shutdown");
            return;
        }
        AppBuilder.Configure<PulseApplication>().With(DesktopFonts.Options()).UseSkia().UseHeadless(new(){UseHeadlessDrawing=false}).SetupWithoutStarting();
        if(args.SequenceEqual(new[]{"--startup-settings"})){StartupModeTests.Settings();return;}
        if(args.SequenceEqual(new[]{"--profile-migration"})){ProfileMigrationTests.Run();return;}
        if(args.SequenceEqual(new[]{"--windows-hardware-live"})){WindowsHardwareTests.Live();return;}
        if(args.SequenceEqual(new[]{"--game-overlay"})){GameOverlayTests.Run();return;}
        if(args.SequenceEqual(new[]{"--startup-controls"})){DesktopStartupTests.Run();return;}
        if(args.SequenceEqual(new[]{"--update-controls"})){DesktopUpdateTests.Run();return;}
        if(args.Length>=1&&args[0]=="--compact-fps"){LockedLayoutTests.Fps(args.Length==2?args[1]:null);return;}
        if(args.Length>=1&&args[0]=="--layouts"){ReadingLayoutTests.Run(args.Length==2?args[1]:null);return;}
        if(args.Length>=1&&args[0]=="--columns"){AdaptiveReadingsTests.Run(args.Length==2?args[1]:null);return;}
        if(args.Length>=1&&args[0]=="--shortcuts"){DesktopShortcutTests.Run(args.Length==2?args[1]:null);return;}
        if(args.Length>=1&&args[0]=="--fps"){FpsPanelTests.Run(args.Length==2?args[1]:null);return;}
        if(args.Length>=1&&args[0]=="--windows-hardware") {WindowsHardwareTests.Run(args.Length==2?args[1]:null);return;}
        var source=new MonitorSource(true);
        var window=new MonitorWindow(source,start:false);
        window.Show();window.Present(source.Poll(null));
        foreach(int width in new[]{800,360,1200}) {
            window.Width=width;window.Height=700;Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var cards=window.GetVisualDescendants().OfType<AdaptiveReadingsPanel>().Single(x=>x.Name=="MonitorCards");
            Check(cards.Columns==(width>=1000?3:width>=660?2:1),"Responsive columns");
            foreach(var value in window.GetVisualDescendants().OfType<TextBlock>())
                Check(value.Bounds.Width<=width,"Text exceeds window width");
            Check(window.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="24.0%"),"CPU value preserved");
            if(args.Length==1) {
                using var frame=window.CaptureRenderedFrame();
                frame!.Save(System.IO.Path.Combine(args[0],$"desktop-{width}.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            }
        }
        var mode=window.GetVisualDescendants().OfType<ComboBox>().Single(x=>x.Name=="ReadingMode");
        mode.SelectedIndex=1;
        Check(window.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="42.0%"),"Session Max switches immediately without polling");
        mode.SelectedIndex=0;
        Check(window.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="24.0%"),"Live restores current snapshot");
        var pause=window.GetVisualDescendants().OfType<CheckBox>().Single(x=>x.Name=="PauseHardware");
        pause.Focus();window.KeyPress(Key.Space,RawInputModifiers.None,PhysicalKey.Space," ");window.KeyRelease(Key.Space,RawInputModifiers.None,PhysicalKey.Space," ");
        Check(pause.IsChecked==true,"Pause keyboard interaction");
        window.Present(new("—","—","—","—",false,false));
        Check(!window.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="24.0%"),"Unavailable clears stale values");
        window.Close();
        var live=new MonitorWindow(new MonitorSource(true));live.Show();
        var limit=DateTime.UtcNow.AddSeconds(5);
        while(!live.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="24.0%")&&DateTime.UtcNow<limit){Dispatcher.UIThread.RunJobs();Thread.Sleep(10);}
        Check(live.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="24.0%"),"Worker publishes to UI");
        live.Close();
        while(!live.Sampling.IsCompleted&&DateTime.UtcNow<limit){Dispatcher.UIThread.RunJobs();Thread.Sleep(10);}
        Check(live.Sampling.IsCompletedSuccessfully,"Close cancels and completes sampling");
        QuotaPanelTests.Run(args.Length==1?args[0]:null);
        WindowsQuotaTests.Run();
        SettingsTests.Run(args.Length==1?args[0]:null);
        StartupModeTests.Settings();
        ProfileMigrationTests.Run();
        LocalContrastTests.Settings();
        DesktopStartupTests.Run();
        DesktopUpdateTests.Run();
        MeasurementTests.Run();
        TrayTests.Run();
        FloatingMonitorTests.Run(args.Length==1?args[0]:null);
        WindowsSystemTests.Run();
        NetworkAdapterTests.Run();
        HardwareSensorTests.Run(args.Length==1?args[0]:null);
        WindowsHardwareTests.Run(args.Length==1?args[0]:null);
        FpsPanelTests.Run();
        GameOverlayTests.Run();
        LockedLayoutTests.Fps(args.Length==1?args[0]:null);
        DesktopShortcutTests.Run();
        AdaptiveReadingsTests.Run();
        ReadingLayoutTests.Run(args.Length==1?args[0]:null);
        GpuPresentationTests.Run(args.Length==1?args[0]:null);
        SessionMaxTests.Run();
        SamplingRecoveryTests.Run();
        LocalizationTests.Run(args.Length==1?args[0]:null);
        Console.WriteLine("PASS Desktop rendering, responsive cards, unavailable state, keyboard and worker shutdown");
    }
}
