using Avalonia.Threading;
using HardwarePulse.Desktop;

// Manual native visual acceptance with deterministic data and a disposable profile.
// No collectors, credentials, startup changes, update requests, or personal settings.
static class NativeVisualFixture {
    public static void Run() {
        string directory=Directory.CreateTempSubdirectory("pulse-visual-").FullName;
        try {
            var store=new PreviewSettingsStore(Path.Combine(directory,"settings.json"));
            store.Save(new PreviewSettings{Theme="Dark",Width=400,Height=850,Language="en",FloatingBackgroundOpacity=0,FloatingTextColor="#E4F3EF",FloatingFontSize=16,FloatingRowSpacing=14});
            var window=new MonitorWindow(new MonitorSource(true),start:false,store:store);
            window.Title="Pulse · isolated visual acceptance";
            HardwareSensorSnapshot[] hardware=[
                new("cpu","CPU temperature","58.6 °C",Device:"AMD Ryzen 7 9700X"),new("vcore","Vcore · Motherboard","1.206 V"),new("cpuFan","CPU Fan","797 RPM"),
                new("gpu","GPU temperature","45.8 °C",Device:"NVIDIA GeForce RTX 5080"),new("gpuLoad","GPU utilization","9.0%"),new("vram","VRAM Junction","56.0 °C"),
                new("gpuVolt","Core Voltage","0.840 V"),new("gpuFan","GPU Fan 1","0 RPM"),new("gpuFan2","GPU Fan 2","0 RPM"),new("vramUsage","GPU memory","2.0 / 16.0 GiB · 12.5%"),
                new("ramA","Module 1","43.0 °C"),new("ramB","Module 2","42.0 °C"),new("diskC","Drive 1","44.0 °C"),new("diskD","Drive 2","41.0 °C"),
                new("system","Motherboard temperature","39.0 °C",Device:"Motherboard"),new("bottom","System Fan 1","850 RPM"),new("top","System Fan 2","850 RPM"),new("lanLink","LAN Link Speed","2.5 Gbit/s")];
            window.Show();
            window.Present(new MonitorSnapshot("18.9%","37.2 / 61.4 GiB · 60.5%","124.5 KiB/s","8.2 KiB/s",true,true){CpuModel="AMD Ryzen 7 9700X",WindowsHardwareSupported=true,WindowsHardware=hardware,PeakWindowsHardware=hardware,PeakCpu="42.0%",WindowsHardwareStatus="Demo · Sample values"});
            using var stop=new CancellationTokenSource();window.Closed+=(_,_)=>stop.Cancel();
            Dispatcher.UIThread.MainLoop(stop.Token);
        }finally{Directory.Delete(directory,true);}
    }
}
