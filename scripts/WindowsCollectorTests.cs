using System;
using System.IO;
using System.Reflection;
using HardwarePulse;

static class WindowsCollectorTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static int Main(string[] args) {
        if(args.Length!=1)return 2;
        string folder=Path.GetFullPath(args[0]);
        Directory.CreateDirectory(folder);
        string worker=AppDomain.CurrentDomain.BaseDirectory;
        var assembly=typeof(Collector).Assembly;
        foreach(var reference in assembly.GetReferencedAssemblies())
            Check(reference.Name!="PresentationFramework"&&reference.Name!="PresentationCore"&&reference.Name!="System.Windows.Forms"&&reference.Name!="Avalonia"&&reference.Name!="System.Management.Automation","Worker loads UI or PowerShell");
        Check(assembly.GetName().Name=="HardwarePulse.Collector","Test must execute shipped collector assembly");
        foreach(var input in new[]{new string[0],new[]{"--unknown"},new[]{"--collector","arbitrary-path"}})
            Check((int)assembly.EntryPoint.Invoke(null,new object[]{input})==2,"Worker accepts unsupported command");
        // No UI exists in the test payload: reject before touching installed state.
        Check((int)assembly.EntryPoint.Invoke(null,new object[]{new[]{"--collector"}})==4,"Incomplete payload launches collector");
        foreach(string command in new[]{"--install-startup","--remove-startup","--enable-startup","--disable-startup","--startup-enabled","--start-collector"})
            Check((int)assembly.EntryPoint.Invoke(null,new object[]{new[]{command}})==4,"Incomplete payload manages installed tasks");
        string ui=Path.Combine(Path.GetDirectoryName(worker.TrimEnd(Path.DirectorySeparatorChar)),"HardwarePulse.exe");
        Check(!File.Exists(ui),"Test must not replace an existing UI");
        File.WriteAllText(ui,"Isolated test placeholder; not executable");
        try {
            foreach(string command in new[]{"--install-startup","--remove-startup","--enable-startup","--disable-startup","--startup-enabled","--start-collector"})
                Check((int)assembly.EntryPoint.Invoke(null,new object[]{new[]{command}})==4,"Unprotected workspace payload manages installed tasks");
        }finally{File.Delete(ui);}
        SharedStartupTests.Run();
        var paths=new PulsePaths(Path.GetDirectoryName(worker.TrimEnd(Path.DirectorySeparatorChar)),folder,Path.Combine(folder,"runtime"));
        Check(paths.Exe==Path.Combine(paths.Root,"HardwarePulse.exe"),"FPS peer remains the fixed UI executable");
        int result=Collector.Run(paths,2,"Local\\PulseWorkerTest-"+Guid.NewGuid().ToString("N"),true);
        Check(result==0&&File.Exists(paths.Snapshot),"Driver-free collector failed");
        var raw=Json.Serializer().Deserialize<RawSnapshot>(Json.Read(paths.Snapshot));
        Check(raw.schema==2&&raw.sequence==2&&raw.pid==System.Diagnostics.Process.GetCurrentProcess().Id,"Snapshot protocol changed");
        var reading=SensorProfile.Parse(raw,DateTimeOffset.UtcNow);
        Check(reading.state=="LIVE"&&reading.usage.ContainsKey("ram")&&reading.values.ContainsKey("cpuLoad"),"Missing live counters");
        Check(!reading.values.ContainsKey("gpu")&&!reading.values.ContainsKey("cpuFan"),"Driver-free source invented hardware");
        Console.WriteLine("PASS dedicated collector: shipped assembly, no UI references, strict arguments, fixed FPS peer and isolated driver-free snapshot");
        return 0;
    }
}
