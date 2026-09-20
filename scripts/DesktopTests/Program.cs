using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class Tests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    [STAThread]
    static void Main(string[] args) {
        var expected=Environment.GetEnvironmentVariable("PULSE_TEST_ARCH");
        Check(expected==null||expected==System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),"Architecture mismatch");
        if(args.SequenceEqual(new[]{"--native-contrast"})) {HardwarePulse.Desktop.Program.BuildApp().SetupWithoutStarting();WindowsCaptureTests.Native();LocalContrastTests.Native();return;}
        if(args.SequenceEqual(new[]{"--native-desktop-tray"})) {HardwarePulse.Desktop.Program.BuildApp().SetupWithoutStarting();FloatingMonitorTests.Run();TrayTests.Run(native:true);return;}
        if(args.Length>=1&&args[0]=="--native-game-overlay") {HardwarePulse.Desktop.Program.BuildApp().SetupWithoutStarting();GameOverlayTests.Native(args.Length==2?args[1]:null);return;}
        if(args.SequenceEqual(new[]{"--native-session"})) {
            HardwarePulse.Desktop.Program.BuildApp().SetupWithoutStarting();
            WindowsCaptureTests.Native();LocalContrastTests.Native();
            GameOverlayTests.Native();
            DesktopMetricPreferenceTests.Run();DesktopGeometryTests.Run();SettingsTests.Run(null);
            FpsPanelTests.Owner();
            AppMaterialTests.Run();DesktopModeTests.Run();
            LocalizationTests.Run(null,native:true);
            TrayTests.Run(native:true);
            TitlebarTests.Run();
            FloatingMonitorTests.Run();
            WindowsInputTests.Run();
            MacInputTests.Run();
            X11InputTests.Run();
            Console.WriteLine("PASS native Desktop session: isolated settings, language, theme, restore, tray commands and shutdown");
            return;
        }
        AppBuilder.Configure<PulseApplication>().With(DesktopFonts.Options()).UseSkia().UseHeadless(new(){UseHeadlessDrawing=false}).SetupWithoutStarting();
        if(args.Length>=1&&args[0]=="--fps") {FpsPanelTests.Run(args.Length==2?args[1]:null);return;}
        if(args.Length>=1&&args[0]=="--game-overlay") {GameOverlayTests.Run(args.Length==2?args[1]:null);return;}
        if(args.SequenceEqual(new[]{"--palette"})) {ReadingPaletteTests.Run();return;}
        if(args.Length>=1&&args[0]=="--material") {AppMaterialTests.Run(args.Length==2?args[1]:null);return;}
        if(args.SequenceEqual(new[]{"--desktop-tray"})) {FloatingMonitorTests.Run();TrayTests.Run();return;}
        if(args.Length==2&&args[0]=="--settings-sections") {SettingsSectionTests.Run(args[1]);return;}
        var source=new MonitorSource(true);
        var window=new MonitorWindow(source,start:false);
        window.Show();window.Present(source.Poll(null));
        foreach(int width in new[]{800,360,1200}) {
            window.Width=width;window.Height=700;Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var cards=window.GetVisualDescendants().OfType<Grid>().Single(x=>x.Name=="ReadingCards");
            Check(cards.ColumnDefinitions.Count==(width==1200?3:width==800?2:1),"WPF device cards use one to three responsive columns");
            // Collapsed platform-only sections retain their previous arrange size.
            foreach(var value in window.GetVisualDescendants().OfType<TextBlock>().Where(x=>x.IsEffectivelyVisible))
                Check(value.Bounds.Width<=width,$"Text exceeds window width: {value.Text}; {value.Bounds.Width} > {width}");
            Check(window.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="24.0%"),"CPU value preserved");
            if(args.Length==1) {
                using var frame=window.CaptureRenderedFrame();
                frame!.Save(System.IO.Path.Combine(args[0],$"desktop-{width}.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            }
        }
        var mode=window.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="SessionMax");
        mode.Focus();window.KeyPress(Key.Space,RawInputModifiers.None,PhysicalKey.Space," ");window.KeyRelease(Key.Space,RawInputModifiers.None,PhysicalKey.Space," ");
        var maxButton=(Avalonia.Controls.Primitives.ToggleButton)mode;
        var liveButton=window.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>().Single(x=>x.Name=="Live");
        Check(maxButton.IsChecked==true&&liveButton.IsChecked==false,"Keyboard selects Session Max exclusively");
        mode.Focus();window.KeyPress(Key.Space,RawInputModifiers.None,PhysicalKey.Space," ");window.KeyRelease(Key.Space,RawInputModifiers.None,PhysicalKey.Space," ");
        Check(maxButton.IsChecked==true&&liveButton.IsChecked==false,"Activating the selected mode keeps one mode selected");
        Check(window.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="42.0%"),"Session Max switches immediately without polling");
        window.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="Live").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
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
        FpsPanelTests.Run(args.Length==1?args[0]:null);
        GameOverlayTests.Run(args.Length==1?args[0]:null);
        ReorderHandleTests.Run();DesktopMetricPreferenceTests.Run(args.Length==1?args[0]:null);DesktopGeometryTests.Run();SettingsTests.Run(args.Length==1?args[0]:null);
        AppMaterialTests.Run();DesktopModeTests.Run();
        ReadingPaletteTests.Run(args.Length==1?args[0]:null);
        LocalContrastTests.Settings();
        WindowsSnapshotTests.Run();WindowsQuotaTests.Run();
        DeviceCardsTests.Run(args.Length==1?args[0]:null);
        CardPreferenceTests.Run(args.Length==1?args[0]:null);
        NavigationTests.Run(args.Length==1?args[0]:null);SettingsSectionTests.Run(args.Length==1?args[0]:null);
        TitlebarTests.Run();
        MeasurementTests.Run();
        TrayTests.Run();
        FloatingMonitorTests.Run(args.Length==1?args[0]:null);
        WindowsSystemTests.Run();
        NetworkAdapterTests.Run();
        HardwareSensorTests.Run(args.Length==1?args[0]:null);
        SessionMaxTests.Run();
        SamplingRecoveryTests.Run();
        LocalizationTests.Run(args.Length==1?args[0]:null);
        Console.WriteLine("PASS Desktop rendering, responsive cards, unavailable state, keyboard and worker shutdown");
    }
}
