using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace HardwarePulse {
    public sealed record MacCpuIdentity(string Model,int? PhysicalCores,int? LogicalCores);
    // Static machine description, cached per source. No process launch or per-poll sysctl.
    public sealed class MacCpuIdentityReader {
        readonly Func<string,byte[]> query;
        MacCpuIdentity cached;
        public MacCpuIdentityReader(){
            if(!OperatingSystem.IsMacOS())throw new PlatformNotSupportedException("macOS CPU identity is required");
            query=QueryNative;
        }
        public MacCpuIdentityReader(Func<string,byte[]> query){this.query=query??throw new ArgumentNullException(nameof(query));}
        public MacCpuIdentity Read() {
            if(cached!=null)return cached;
            string model=null;byte[] data=Query("machdep.cpu.brand_string");
            if(data!=null&&data.Length<=512) {
                int length=Array.IndexOf(data,(byte)0);if(length<0)length=data.Length;
                try {model=new UTF8Encoding(false,true).GetString(data,0,length).Trim();}
                catch(DecoderFallbackException){}
                if(string.IsNullOrWhiteSpace(model)||model.Any(char.IsControl))model=null;
            }
            int? physical=Count(Query("hw.physicalcpu_max")),logical=Count(Query("hw.logicalcpu_max"));
            if(physical>logical)logical=null;
            return cached=new MacCpuIdentity(model,physical,logical);
        }
        byte[] Query(string name) {
            try{return query(name);}
            catch(Exception e) when(e is IOException||e is DllNotFoundException||e is EntryPointNotFoundException){return null;}
        }
        static int? Count(byte[] data) {
            if(data==null||(data.Length!=4&&data.Length!=8))return null;
            long value=data.Length==4?BinaryPrimitives.ReadInt32LittleEndian(data):BinaryPrimitives.ReadInt64LittleEndian(data);
            return value>0&&value<=65536?(int)value:null;
        }
        [DllImport(MacMach.LibSystem)] static extern int sysctlbyname([MarshalAs(UnmanagedType.LPUTF8Str)] string name,[Out] byte[] value,ref nuint size,IntPtr newValue,nuint newSize);
        static byte[] QueryNative(string name) {
            var buffer=new byte[512];nuint size=(nuint)buffer.Length;
            if(sysctlbyname(name,buffer,ref size,IntPtr.Zero,0)!=0||size==0||size>(nuint)buffer.Length)return null;
            return buffer.AsSpan(0,(int)size).ToArray();
        }
    }
}
