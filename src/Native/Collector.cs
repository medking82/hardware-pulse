using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        static readonly WindowsNetwork network=new WindowsNetwork(id=>new Identifier("nic",id).ToString());
        static void ReadSensors(IHardware hardware,List<Sensor> target) {
            hardware.Update();
            foreach(ISensor sensor in hardware.Sensors)target.Add(new Sensor {id=sensor.Identifier.ToString(),name=sensor.Name,hardware=hardware.Name,hardwareId=hardware.Identifier.ToString(),hardwareType=hardware.HardwareType.ToString(),type=sensor.SensorType.ToString(),value=sensor.Value.HasValue?(double?)sensor.Value.Value:null});
            foreach(IHardware child in hardware.SubHardware)ReadSensors(child,target);
        }
        public static int Run(PulsePaths paths,int samples=0,string mutexName="Local\\HardwarePulseCollector",bool driverFree=false) {
            Directory.CreateDirectory(paths.Runtime);
            using(var mutex=new Mutex(false,mutexName)){
                bool owned=false;try{owned=mutex.WaitOne(0);}catch(AbandonedMutexException){owned=true;}if(!owned)return 0;
                bool compatibility=driverFree||WindowsCompatibility.RequiresDriverFreeCollector(WindowsCompatibility.CurrentVersion());
                var basic=compatibility?new WindowsSystemReadings():null;
                var computer=compatibility?null:new Computer {IsCpuEnabled=true,IsGpuEnabled=true,IsMemoryEnabled=true,IsMotherboardEnabled=true,IsStorageEnabled=true,IsNetworkEnabled=true};
                // A blocked driver read must not prevent a cooperative upgrade from completing.
                // Exit the collector process only, never another application's process.
                int stopping=0;
                using(var fps=samples==0&&!compatibility?new FpsServer(paths):null)
                using(var watchdog=new Timer(delegate {if(File.Exists(paths.Stop)&&Interlocked.Exchange(ref stopping,1)==0){if(fps!=null)fps.Dispose();Environment.Exit(0);}},null,500,500)){
                    try {
                        File.WriteAllText(Path.Combine(paths.Runtime,"collector-stage.txt"),compatibility?"Driver-free compatibility counters; temperature, fans and FPS unavailable":"Opening Hardware");if(computer!=null)computer.Open();
                        string memoryName="Memory";MemoryModule[] modules=new MemoryModule[0];DiskInfo[] disks=new DiskInfo[0];
                        try{modules=WindowsHardware.ReadMemoryModules(out memoryName);}catch(Exception e){File.WriteAllText(Path.Combine(paths.Runtime,"memory-warning.txt"),e.Message);}
                        try{disks=WindowsHardware.ReadDisks();}catch(Exception e){File.WriteAllText(Path.Combine(paths.Runtime,"disk-warning.txt"),e.Message);}
                        var board=computer==null?null:computer.Hardware.FirstOrDefault(h=>h.HardwareType==HardwareType.Motherboard);
                        long sequence=0;
                        while(!File.Exists(paths.Stop)){
                            var sensors=new List<Sensor>();
                            if(computer!=null)foreach(var hardware in computer.Hardware)ReadSensors(hardware,sensors);
                            if(basic!=null) {
                                var reading=basic.Read(DateTimeOffset.UtcNow);double load;
                                sensors.Add(new Sensor {id="/windows/cpu/load/0",name="CPU Total",hardware="CPU",hardwareId="/windows/cpu",hardwareType="Cpu",type="Load",value=reading.values.TryGetValue("cpuLoad",out load)?(double?)load:null});
                                sensors.AddRange(network.ReadThroughput());
                            }
                            var usage=WindowsHardware.ReadMemory();
                            var raw=new RawSnapshot {schema=2,time=DateTimeOffset.Now.ToString("o"),sequence=++sequence,pid=System.Diagnostics.Process.GetCurrentProcess().Id,sensors=sensors.ToArray(),memoryName=memoryName,memoryModules=modules,disks=disks,ramUsage=usage,boardName=board==null?"Motherboard":board.Name,networkLinks=network.Read()};
                            try{Json.WriteAtomic(paths.Snapshot,raw);}catch(IOException e){File.WriteAllText(Path.Combine(paths.Runtime,"write-warning.txt"),DateTimeOffset.Now.ToString("o")+" "+e.Message);}
                            if(samples>0&&sequence>=samples)break;Thread.Sleep(2000);
                        }
                        return 0;
                    }catch(Exception e){File.WriteAllText(Path.Combine(paths.Runtime,"collector-error.txt"),e.ToString());return 1;}
                    finally{if(computer!=null)computer.Close();mutex.ReleaseMutex();}
                }
            }
        }
    }
}
