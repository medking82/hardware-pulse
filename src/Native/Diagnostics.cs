using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace HardwarePulse {
    public static class DiagnosticReport {
        // Explicit projection: never serialize settings, network identities or arbitrary logs.
        public static string Create(RawSnapshot raw,string status,string manufacturer,string model) {
            var sensors=(raw==null?new Sensor[0]:raw.sensors??new Sensor[0]).Where(s=>s!=null&&s.hardwareType!="Network"&&s.type!="Throughput").Take(4096).ToArray();
            Reading mapped=null;
            if(raw!=null)try{mapped=SensorProfile.Parse(raw,DateTimeOffset.Now);}catch{status="invalid_snapshot";}
            var fanKeys=new[]{"cpuFan","gpuFan","gpuFan2","bottom","top"};
            return Json.Serializer().Serialize(new {
                formatVersion=1,appVersion=typeof(DiagnosticReport).Assembly.GetName().Version.ToString(),
                exportedUtc=DateTimeOffset.UtcNow.ToString("o"),osVersion=Environment.OSVersion.Version.ToString(),
                os64Bit=Environment.Is64BitOperatingSystem,process64Bit=Environment.Is64BitProcess,
                manufacturer=manufacturer,model=model,snapshotStatus=status,
                snapshotTime=raw==null?null:raw.time,snapshotSchema=raw==null?0:raw.schema,
                board=raw==null?null:raw.boardName,readingState=mapped==null?null:mapped.state,
                sensors=sensors.Select(s=>new {id=s.id,name=s.name,hardware=s.hardware,hardwareType=s.hardwareType,type=s.type,value=s.value.HasValue&&!double.IsNaN(s.value.Value)&&!double.IsInfinity(s.value.Value)?s.value:null}).ToArray(),
                fanMapping=fanKeys.Select(key=>new {key=key,available=mapped!=null&&mapped.available!=null&&mapped.available.ContainsKey(key)&&mapped.available[key],rpm=mapped!=null&&mapped.values.ContainsKey(key)?(double?)mapped.values[key]:null}).ToArray()
            });
        }
        public static string Collect(string snapshotPath) {
            RawSnapshot raw=null;string status="available",manufacturer=null,model=null;
            try{if(!File.Exists(snapshotPath))status="missing_snapshot";else if(new FileInfo(snapshotPath).Length>8*1024*1024)status="snapshot_too_large";else{raw=Json.Serializer().Deserialize<RawSnapshot>(Json.Read(snapshotPath));if(raw==null)status="invalid_snapshot";}}
            catch{status="unreadable_snapshot";}
            try{
                using(var query=new ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem")){
                    query.Options.Timeout=TimeSpan.FromSeconds(3);
                    using(var rows=query.Get())foreach(ManagementObject row in rows)using(row){manufacturer=Convert.ToString(row["Manufacturer"]);model=Convert.ToString(row["Model"]);break;}
                }
            }catch{/* Hardware data remains useful when WMI is unavailable. */}
            return Create(raw,status,manufacturer,model);
        }
    }
    public sealed partial class Shell {
        async void ExportDiagnostics(){
            var dialog=new Microsoft.Win32.SaveFileDialog {Title=language.T("Export Diagnostics"),Filter="JSON (*.json)|*.json",DefaultExt=".json",AddExtension=true,FileName="Pulse-diagnostics-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".json"};
            if(dialog.ShowDialog(Window)!=true)return;
            var button=Control<Button>("ExportDiagnostics");button.IsEnabled=false;Text("DiagnosticStatus",language.T("Exporting diagnostics…"));
            try{
                string snapshot=paths.Snapshot,destination=dialog.FileName;
                await Task.Run(()=>File.WriteAllText(destination,DiagnosticReport.Collect(snapshot),new UTF8Encoding(false)));
                if(!disposed)Text("DiagnosticStatus",language.T("Diagnostics saved. You can share this file for support."));
            }catch{if(!disposed)Text("DiagnosticStatus",language.T("Could not save diagnostics. Choose another location and try again."));}
            finally{if(!disposed)button.IsEnabled=true;}
        }
    }
}
