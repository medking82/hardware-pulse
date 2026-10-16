using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using HardwarePulse;

internal static class NativeSnapshotRecoveryTests {
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool value,string message){if(!value)throw new Exception("Snapshot UI: "+message);}
    internal static void Run(string root,string directory){
        Directory.CreateDirectory(directory);Json.WriteAtomic(Path.Combine(directory,"widget-settings.json"),new {language="en",solid=true});
        var paths=new PulsePaths(root,directory,Path.Combine(directory,"runtime"));var now=DateTimeOffset.Now;
        var raw=new RawSnapshot{schema=2,pid=101,sequence=1,time=now.ToString("o"),sensors=new[]{new Sensor{id="/cpu/0/temperature/0",name="CPU Package",hardware="Fixture CPU",hardwareId="/cpu/0",hardwareType="Cpu",type="Temperature",value=62}}};
        Json.WriteAtomic(paths.Snapshot,raw);
        using(var shell=new Shell(paths,true)){
            shell.Window.ShowActivated=false;shell.Window.ShowInTaskbar=false;shell.Show();shell.UpdatePanel();
            Check(shell.Control<TextBlock>("Status").Text.Contains("Live"),"initial real snapshot is live");
            using(var locked=new FileStream(paths.Snapshot,FileMode.Open,FileAccess.Read,FileShare.None)){
                shell.UpdatePanel();Check(shell.Control<TextBlock>("Status").Text.Contains("Recent Sample")&&shell.Control<TextBlock>("Status").Text.Contains("Retrying Snapshot"),"recent fallback must be visibly disclosed, not labelled Live");
                var metrics=(System.Collections.Generic.IEnumerable<DesktopMetric>)typeof(Shell).GetMethod("DesktopMetrics",Hidden).Invoke(shell,null);
                Check(metrics.Any(m=>m.Key=="status"&&m.Value.Contains("Retrying Snapshot")),"desktop must disclose recent fallback too");
                var readings=(ReadingSession)typeof(Shell).GetField("readings",Hidden).GetValue(shell);readings.Poll(now.AddSeconds(16));typeof(Shell).GetMethod("RenderPanel",Hidden).Invoke(shell,null);
                Check(readings.Latest.values.Count==0&&shell.Control<TextBlock>("Status").Text.Contains("Snapshot Busy"),"expired fallback must remove values and retain the actual reason");
            }
            raw.time=DateTimeOffset.Now.ToString("o");raw.sequence=2;Json.WriteAtomic(paths.Snapshot,raw);shell.UpdatePanel();Check(shell.Control<TextBlock>("Status").Text.Contains("Live"),"live sampling resumes after file unlock");shell.Exit();
        }
        var type=typeof(Shell).Assembly.GetType("HardwarePulse.HardwareDiagnosticLog");Check(type!=null,"bounded transition diagnostics are available");
        var log=Activator.CreateInstance(type,new object[]{directory});var record=type.GetMethod("Record");
        for(int i=0;i<100;i++)record.Invoke(log,new object[]{new Reading{error="private-fixture-path-and-payload",readErrorCode=32},now});
        string path=Path.Combine(directory,"hardware-diagnostics.jsonl");Check(File.ReadAllLines(path).Length==1,"identical failure must not spam diagnostics");
        Check(!File.ReadAllText(path).Contains("private-fixture"),"diagnostics must not serialize raw exception text");
        record.Invoke(log,new object[]{new Reading{state="LIVE",time=now},now});Check(File.ReadAllLines(path).Length==2,"recovery transition is recorded");
        Console.WriteLine("PASS snapshot UI: recent sample warning, original expiry, recovery and bounded metadata-only diagnostics");
    }
}
