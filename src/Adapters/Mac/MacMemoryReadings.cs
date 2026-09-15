using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace HardwarePulse {
    public struct MacMemoryCounters {
        public ulong TotalBytes;
        public uint PageSize,InternalPages,PurgeablePages,WiredPages,CompressorPages;
    }

    public sealed class MacMemoryReadings {
        readonly Func<MacMemoryCounters> read;
        readonly string source=Guid.NewGuid().ToString("N");
        long sequence;
        public MacMemoryReadings(){
            if(!OperatingSystem.IsMacOS())throw new PlatformNotSupportedException("macOS memory statistics are required");
            read=ReadNative;
        }
        public MacMemoryReadings(Func<MacMemoryCounters> read){this.read=read??throw new ArgumentNullException("read");}

        public Reading Read(DateTimeOffset now){
            var result=new Reading {time=now,identity=source+":"+(++sequence).ToString(CultureInfo.InvariantCulture)};
            try{
                var counters=read();
                if(counters.TotalBytes==0||counters.PageSize==0||(counters.PageSize&(counters.PageSize-1))!=0
                    ||counters.PurgeablePages>counters.InternalPages)throw new IOException("Inconsistent macOS memory counters");
                // Anonymous non-purgeable + wired + physical compressor storage.
                // Excludes file cache; this is an estimate, not Activity Monitor parity.
                double pages=(double)(counters.InternalPages-counters.PurgeablePages)+counters.WiredPages+counters.CompressorPages;
                double used=pages*counters.PageSize;
                if(used>counters.TotalBytes)throw new IOException("Used memory exceeds physical memory");
                result.usage["ram"]=new Usage {used=used/1073741824.0,total=counters.TotalBytes/1073741824.0,
                    percent=100*used/counters.TotalBytes,label="Used memory estimate"};
                result.state="LIVE";
            }catch(Exception e) when(e is IOException||e is DllNotFoundException||e is EntryPointNotFoundException){result.error=e.Message;}
            return result;
        }

        [DllImport(MacMach.LibSystem)] static extern int host_statistics64(uint host,int flavor,[Out] uint[] data,ref uint count);
        [DllImport(MacMach.LibSystem)] static extern int host_page_size(uint host,out nuint size);
        [DllImport(MacMach.LibSystem)] static extern int sysctlbyname([MarshalAs(UnmanagedType.LPUTF8Str)] string name,out ulong value,ref nuint size,IntPtr newValue,nuint newSize);
        static MacMemoryCounters ReadNative(){
            uint host=MacMach.AcquireHost();
            try{
                nuint totalSize=8;
                if(sysctlbyname("hw.memsize",out ulong total,ref totalSize,IntPtr.Zero,0)!=0||totalSize!=8)
                    throw new IOException("Physical memory size unavailable");
                if(host_page_size(host,out nuint pageSize)!=0||pageSize>uint.MaxValue)throw new IOException("Kernel page size unavailable");
                // Request the established rev1 prefix, not a guessed latest struct size.
                // Offsets and count are checked against the native SDK in CI.
                uint count=38;var data=new uint[38];
                int status=host_statistics64(host,4,data,ref count); // HOST_VM_INFO64
                if(status!=0||count!=38)throw new IOException("Mach memory statistics unavailable: "+status.ToString(CultureInfo.InvariantCulture));
                return new MacMemoryCounters {TotalBytes=total,PageSize=(uint)pageSize,WiredPages=data[3],
                    PurgeablePages=data[22],CompressorPages=data[32],InternalPages=data[35]};
            }finally{MacMach.ReleaseHost(host);}
        }
    }
}
