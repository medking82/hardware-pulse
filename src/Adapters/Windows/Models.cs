using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
#if !NET
using System.Web.Script.Serialization;
#endif

namespace HardwarePulse {
    public sealed class Sensor {
        public string id, name, hardware, hardwareId, hardwareType, type;
        public double? value;
    }
    public sealed class MemoryModule { public string brand, part, slot; public double capacityGb; }
    public sealed class DiskInfo { public string model; public string[] volumes; }
    public sealed class RamUsage { public double? usedGb, totalGb; }
    public sealed class NetworkLink {public string hardwareId,connectionType;public long? bitsPerSecond;public int? signalPercent;public bool connected;public bool? physical;}
    public sealed class RawSnapshot {
        public int schema, pid; public long sequence; public string time, memoryName, boardName;
        public Sensor[] sensors; public MemoryModule[] memoryModules; public DiskInfo[] disks; public RamUsage ramUsage;
        public NetworkLink[] networkLinks;
    }
#if !NET
    public static class Json {
        public static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength=8*1024*1024, RecursionLimit=64 }; }
        public static string Read(string path) {
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
            using(var reader=new StreamReader(stream)) return reader.ReadToEnd();
        }
        public static void WriteAtomic(string path,object value) {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp=path+".tmp";
            File.WriteAllText(temp,Serializer().Serialize(value),new UTF8Encoding(false));
            if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
        }
    }
#endif
}
