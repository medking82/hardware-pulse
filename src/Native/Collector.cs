using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using LibreHardwareMonitor.Hardware;

namespace HardwarePulse {
    public sealed class PulsePaths {
        public readonly string Root,State,Runtime,Exe;
        public PulsePaths(string root,string state,string runtime) {Root=Path.GetFullPath(root);State=Path.GetFullPath(state);Runtime=Path.GetFullPath(runtime);Exe=Path.Combine(Root,"HardwarePulse.exe");}
        public static PulsePaths Installed() {
            return new PulsePaths(AppDomain.CurrentDomain.BaseDirectory,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"HardwarePulse"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"HardwarePulse",WindowsIdentity.GetCurrent().User.Value,"runtime"));
        }
        public string Stop {get{return Path.Combine(Runtime,"STOP");}}
        public string Snapshot {get{return Path.Combine(Runtime,"snapshot.json");}}
    }
    public static class Collector {
        // Windows returns the same usable physical-memory capacity as Win32_OperatingSystem,
        // without running a WMI query on every sensor sample.
        [StructLayout(LayoutKind.Sequential)] struct MemoryStatus {
            public uint Length,Load;public ulong TotalPhysical,AvailablePhysical,TotalPage,AvailablePage,TotalVirtual,AvailableVirtual,AvailableExtended;
        }
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool GlobalMemoryStatusEx(ref MemoryStatus state);
        static string Text(ManagementBaseObject value,string key) {return Convert.ToString(value[key])??"";}
        static double Number(ManagementBaseObject value,string key){try{return Convert.ToDouble(value[key]);}catch{return 0;}}
        static void ReadSensors(IHardware hardware,List<Sensor> target) {
            hardware.Update();
            foreach(ISensor sensor in hardware.Sensors)target.Add(new Sensor {id=sensor.Identifier.ToString(),name=sensor.Name,hardware=hardware.Name,hardwareId=hardware.Identifier.ToString(),hardwareType=hardware.HardwareType.ToString(),type=sensor.SensorType.ToString(),value=sensor.Value.HasValue?(double?)sensor.Value.Value:null});
            foreach(IHardware child in hardware.SubHardware)ReadSensors(child,target);
        }
        static MemoryModule[] GetMemory(out string name) {
            var modules=new List<MemoryModule>();var speeds=new HashSet<int>();var types=new List<int>();
            using(var query=new ManagementObjectSearcher("SELECT Manufacturer,PartNumber,DeviceLocator,Capacity,ConfiguredClockSpeed,SMBIOSMemoryType FROM Win32_PhysicalMemory"))
            using(var rows=query.Get())foreach(ManagementObject row in rows)using(row){
                modules.Add(new MemoryModule {brand=Text(row,"Manufacturer").Trim(),part=Text(row,"PartNumber").Trim(),slot=Text(row,"DeviceLocator"),capacityGb=Math.Round(Number(row,"Capacity")/1073741824.0)});
                int speed=(int)Number(row,"ConfiguredClockSpeed");if(speed>0)speeds.Add(speed);types.Add((int)Number(row,"SMBIOSMemoryType"));
            }
            double gb=modules.Sum(m=>m.capacityGb);string type=types.Count>0&&types.All(t=>t==34)?"DDR5":types.Count>0&&types.All(t=>t==26)?"DDR4":"RAM";
            name=gb>0?gb+" GB "+type:"Memory";if(speeds.Count==1)name+="-"+speeds.First()+" configured";return modules.ToArray();
        }
        static DiskInfo[] GetDisks() {
            var disks=new List<DiskInfo>();
            using(var query=new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive"))
            using(var rows=query.Get())foreach(ManagementObject drive in rows)using(drive){
                var volumes=new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                using(var parts=drive.GetRelated("Win32_DiskPartition"))foreach(ManagementObject part in parts)using(part)
                using(var letters=part.GetRelated("Win32_LogicalDisk"))foreach(ManagementObject letter in letters)using(letter)volumes.Add(Text(letter,"DeviceID"));
                disks.Add(new DiskInfo {model=Text(drive,"Model"),volumes=volumes.ToArray()});
            }return disks.ToArray();
        }
        public static int Run(PulsePaths paths,int samples=0,string mutexName="Local\\HardwarePulseCollector") {
            Directory.CreateDirectory(paths.Runtime);
            using(var mutex=new Mutex(false,mutexName)){
                bool owned=false;try{owned=mutex.WaitOne(0);}catch(AbandonedMutexException){owned=true;}if(!owned)return 0;
                var computer=new Computer {IsCpuEnabled=true,IsGpuEnabled=true,IsMemoryEnabled=true,IsMotherboardEnabled=true,IsStorageEnabled=true,IsNetworkEnabled=true};
                // A blocked driver read must not prevent a cooperative upgrade from completing.
                // Exit the collector process only, never another application's process.
                int stopping=0;
                using(var fps=samples==0?new FpsServer(paths):null)
                using(var watchdog=new Timer(delegate {if(File.Exists(paths.Stop)&&Interlocked.Exchange(ref stopping,1)==0){if(fps!=null)fps.Dispose();Environment.Exit(0);}},null,500,500)){
                    try {
                        File.WriteAllText(Path.Combine(paths.Runtime,"collector-stage.txt"),"Opening Hardware");computer.Open();
                        string memoryName="Memory";MemoryModule[] modules=new MemoryModule[0];DiskInfo[] disks=new DiskInfo[0];
                        try{modules=GetMemory(out memoryName);}catch(Exception e){File.WriteAllText(Path.Combine(paths.Runtime,"memory-warning.txt"),e.Message);}
                        try{disks=GetDisks();}catch(Exception e){File.WriteAllText(Path.Combine(paths.Runtime,"disk-warning.txt"),e.Message);}
                        var board=computer.Hardware.FirstOrDefault(h=>h.HardwareType==HardwareType.Motherboard);
                        long sequence=0;
                        while(!File.Exists(paths.Stop)){
                            var sensors=new List<Sensor>();foreach(var hardware in computer.Hardware)ReadSensors(hardware,sensors);
                            var memory=new MemoryStatus {Length=(uint)Marshal.SizeOf(typeof(MemoryStatus))};RamUsage usage=null;
                            if(GlobalMemoryStatusEx(ref memory)&&memory.TotalPhysical>0)usage=new RamUsage {totalGb=memory.TotalPhysical/1073741824.0,usedGb=(memory.TotalPhysical-memory.AvailablePhysical)/1073741824.0};
                            var raw=new RawSnapshot {schema=2,time=DateTimeOffset.Now.ToString("o"),sequence=++sequence,pid=System.Diagnostics.Process.GetCurrentProcess().Id,sensors=sensors.ToArray(),memoryName=memoryName,memoryModules=modules,disks=disks,ramUsage=usage,boardName=board==null?"Motherboard":board.Name};
                            try{Json.WriteAtomic(paths.Snapshot,raw);}catch(IOException e){File.WriteAllText(Path.Combine(paths.Runtime,"write-warning.txt"),DateTimeOffset.Now.ToString("o")+" "+e.Message);}
                            if(samples>0&&sequence>=samples)break;Thread.Sleep(2000);
                        }
                        return 0;
                    }catch(Exception e){File.WriteAllText(Path.Combine(paths.Runtime,"collector-error.txt"),e.ToString());return 1;}
                    finally{computer.Close();mutex.ReleaseMutex();}
                }
            }
        }
    }
}
