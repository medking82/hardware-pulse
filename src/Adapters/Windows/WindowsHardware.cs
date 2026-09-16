using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;

namespace HardwarePulse {
    public static class WindowsHardware {
        // Windows returns the same usable physical-memory capacity as Win32_OperatingSystem,
        // without running a WMI query on every sensor sample.
        [StructLayout(LayoutKind.Sequential)] struct MemoryStatus {
            public uint Length,Load;public ulong TotalPhysical,AvailablePhysical,TotalPage,AvailablePage,TotalVirtual,AvailableVirtual,AvailableExtended;
        }
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool GlobalMemoryStatusEx(ref MemoryStatus state);
        static string Text(ManagementBaseObject value,string key) {return Convert.ToString(value[key])??"";}
        static double Number(ManagementBaseObject value,string key){try{return Convert.ToDouble(value[key]);}catch{return 0;}}
        public static RamUsage ReadMemory(){
            var memory=new MemoryStatus {Length=(uint)Marshal.SizeOf(typeof(MemoryStatus))};
            if(!GlobalMemoryStatusEx(ref memory)||memory.TotalPhysical==0)return null;
            return new RamUsage {totalGb=memory.TotalPhysical/1073741824.0,usedGb=(memory.TotalPhysical-memory.AvailablePhysical)/1073741824.0};
        }
        public static MemoryModule[] ReadMemoryModules(out string name) {
            var modules=new List<MemoryModule>();var speeds=new HashSet<int>();var types=new List<int>();
            // Win7 lacks newer optional fields. SELECT * keeps the query valid;
            // Number tolerates absent properties and falls back to legacy metadata.
            using(var query=new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory"))
            using(var rows=query.Get())foreach(ManagementObject row in rows)using(row){
                modules.Add(new MemoryModule {brand=Text(row,"Manufacturer").Trim(),part=Text(row,"PartNumber").Trim(),slot=Text(row,"DeviceLocator"),capacityGb=Math.Round(Number(row,"Capacity")/1073741824.0)});
                int speed=(int)Number(row,"ConfiguredClockSpeed");if(speed<=0)speed=(int)Number(row,"Speed");if(speed>0)speeds.Add(speed);
                int memoryType=(int)Number(row,"SMBIOSMemoryType");if(memoryType<=0)memoryType=(int)Number(row,"MemoryType");types.Add(memoryType);
            }
            double gb=modules.Sum(m=>m.capacityGb);string type=types.Count>0&&types.All(t=>t==34)?"DDR5":types.Count>0&&types.All(t=>t==26)?"DDR4":"RAM";
            name=gb>0?gb+" GB "+type:"Memory";if(speeds.Count==1)name+="-"+speeds.First()+" configured";return modules.ToArray();
        }
        public static DiskInfo[] ReadDisks() {
            var disks=new List<DiskInfo>();
            using(var query=new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive"))
            using(var rows=query.Get())foreach(ManagementObject drive in rows)using(drive){
                var volumes=new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                using(var parts=drive.GetRelated("Win32_DiskPartition"))foreach(ManagementObject part in parts)using(part)
                using(var letters=part.GetRelated("Win32_LogicalDisk"))foreach(ManagementObject letter in letters)using(letter)volumes.Add(Text(letter,"DeviceID"));
                disks.Add(new DiskInfo {model=Text(drive,"Model"),volumes=volumes.ToArray()});
            }return disks.ToArray();
        }
    }
}
