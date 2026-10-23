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
        const int MaxLength=8*1024*1024;
        public static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength=MaxLength, RecursionLimit=64 }; }
        public static string Read(string path) {
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
            using(var reader=new StreamReader(stream)) {
                if(stream.Length>MaxLength)throw new InvalidDataException("JSON file exceeds the size limit.");
                var text=new StringBuilder();
                var buffer=new char[4096];
                int count;
                while((count=reader.Read(buffer,0,buffer.Length))>0) {
                    // The shared file can grow after the initial length check.
                    if(stream.Position>MaxLength||text.Length>MaxLength-count)
                        throw new InvalidDataException("JSON file exceeds the size limit.");
                    text.Append(buffer,0,count);
                }
                return text.ToString();
            }
        }
        public static void WriteAtomic(string path,object value) {
            byte[] bytes=new UTF8Encoding(false).GetBytes(Serializer().Serialize(value));
            if(bytes.Length>MaxLength)throw new InvalidDataException("JSON file exceeds the size limit.");
            AtomicFile.WriteAllBytes(path,bytes);
        }
    }
#endif
}
