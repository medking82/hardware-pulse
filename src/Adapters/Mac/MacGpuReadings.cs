using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace HardwarePulse {
    public sealed record MacGpuSample(ulong RegistryId,string Name,double? Utilization,double? Activity,int? Cores=null);
    public sealed record MacGpuChannel(string Id,string Label,int? Cores);

    // Read-only driver statistics; unavailable properties are not idle GPU measurements.
    public sealed class MacGpuReadings {
        readonly Func<IReadOnlyList<MacGpuSample>> read;
        readonly string source=Guid.NewGuid().ToString("N");
        long sequence;
        readonly Dictionary<ulong,int?> nativeCoreCounts=new();
        public IReadOnlyList<MacGpuChannel> Channels {get;private set;}=Array.Empty<MacGpuChannel>();
        public MacGpuReadings(){
            if(!OperatingSystem.IsMacOS())throw new PlatformNotSupportedException("macOS GPU registry is required");
            read=ReadNative;
        }
        public MacGpuReadings(Func<IReadOnlyList<MacGpuSample>> read){this.read=read??throw new ArgumentNullException(nameof(read));}
        static bool Valid(double? value)=>value.HasValue&&double.IsFinite(value.Value)&&value>=0&&value<=100;
        public Reading Read(DateTimeOffset now) {
            var result=new Reading{time=now,identity=source+":"+(++sequence).ToString(CultureInfo.InvariantCulture),available=new Dictionary<string,bool>()};
            foreach(var channel in Channels){result.names[channel.Id]=channel.Label;result.available[channel.Id]=false;}
            try {
                var samples=read();
                if(samples==null||samples.Count>32)throw new IOException("Invalid GPU registry result");
                var channels=new List<MacGpuChannel>();var ids=new HashSet<ulong>();
                result.names.Clear();result.available.Clear();
                foreach(var sample in samples) {
                    if(sample==null||sample.RegistryId==0||!ids.Add(sample.RegistryId))continue;
                    string id="gpu/"+sample.RegistryId.ToString(CultureInfo.InvariantCulture);
                    string name=sample.Name;
                    if(string.IsNullOrWhiteSpace(name)||name.Length>128||name.Any(char.IsControl))name="GPU";
                    int? cores=sample.Cores>0&&sample.Cores<=65536?sample.Cores:null;
                    channels.Add(new MacGpuChannel(id,name,cores));result.names[id]=name;result.available[id]=false;
                    double? value=Valid(sample.Utilization)?sample.Utilization:Valid(sample.Activity)?sample.Activity:null;
                    if(!value.HasValue)continue;
                    result.values[id]=value.Value;result.available[id]=true;
                }
                Channels=channels.AsReadOnly();
                if(result.values.Count>0)result.state="LIVE";
            }catch(Exception e) when(e is IOException||e is DllNotFoundException||e is EntryPointNotFoundException){result.error=e.Message;}
            return result;
        }

        const string IOKit="/System/Library/Frameworks/IOKit.framework/IOKit";
        const string CF="/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
        [DllImport(IOKit)] static extern IntPtr IOServiceMatching([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
        [DllImport(IOKit)] static extern int IOServiceGetMatchingServices(uint port,IntPtr match,out uint iterator);
        [DllImport(IOKit)] static extern uint IOIteratorNext(uint iterator);
        [DllImport(IOKit)] static extern int IOObjectRelease(uint value);
        [DllImport(IOKit)] static extern int IORegistryEntryGetRegistryEntryID(uint entry,out ulong id);
        [DllImport(IOKit)] static extern int IORegistryEntryGetName(uint entry,[Out] byte[] name);
        [DllImport(IOKit)] static extern IntPtr IORegistryEntryCreateCFProperty(uint entry,IntPtr key,IntPtr allocator,uint options);
        [DllImport(CF)] static extern IntPtr CFStringCreateWithCString(IntPtr allocator,[MarshalAs(UnmanagedType.LPUTF8Str)] string text,uint encoding);
        [DllImport(CF)] static extern void CFRelease(IntPtr value);
        [DllImport(CF)] static extern nuint CFGetTypeID(IntPtr value);
        [DllImport(CF)] static extern nuint CFDictionaryGetTypeID();
        [DllImport(CF)] static extern nuint CFNumberGetTypeID();
        [DllImport(CF)] static extern IntPtr CFDictionaryGetValue(IntPtr dictionary,IntPtr key);
        [DllImport(CF)][return:MarshalAs(UnmanagedType.I1)] static extern bool CFNumberGetValue(IntPtr number,nint type,out double value);
        static IntPtr StringKey(string text) {
            var value=CFStringCreateWithCString(IntPtr.Zero,text,0x08000100);
            return value!=IntPtr.Zero?value:throw new IOException("GPU registry property key unavailable");
        }
        static double? Number(IntPtr dictionary,IntPtr key) {
            var value=CFDictionaryGetValue(dictionary,key); // Borrowed while dictionary is retained.
            return value!=IntPtr.Zero&&CFGetTypeID(value)==CFNumberGetTypeID()&&CFNumberGetValue(value,13,out double number)?number:null;
        }
        IReadOnlyList<MacGpuSample> ReadNative() {
            IntPtr statsKey=IntPtr.Zero,utilizationKey=IntPtr.Zero,activityKey=IntPtr.Zero,coresKey=IntPtr.Zero;
            uint iterator=0;
            try {
                statsKey=StringKey("PerformanceStatistics");utilizationKey=StringKey("Device Utilization %");activityKey=StringKey("GPU Activity(%)");
                coresKey=StringKey("gpu-core-count");
                IntPtr match=IOServiceMatching("IOAccelerator");
                if(match==IntPtr.Zero)throw new IOException("GPU registry matching unavailable");
                if(IOServiceGetMatchingServices(0,match,out iterator)!=0)throw new IOException("GPU registry enumeration unavailable");
                var found=new List<MacGpuSample>();var nameBuffer=new byte[128];
                for(int i=0;i<32;i++) {
                    uint entry=IOIteratorNext(iterator);if(entry==0)break;
                    IntPtr stats=IntPtr.Zero;
                    try {
                        if(IORegistryEntryGetRegistryEntryID(entry,out ulong id)!=0||id==0)continue;
                        Array.Clear(nameBuffer);string name="GPU";
                        if(IORegistryEntryGetName(entry,nameBuffer)==0){int length=Array.IndexOf(nameBuffer,(byte)0);name=Encoding.UTF8.GetString(nameBuffer,0,length<0?128:length);}
                        if(!nativeCoreCounts.TryGetValue(id,out int? cores)) {
                            IntPtr property=IORegistryEntryCreateCFProperty(entry,coresKey,IntPtr.Zero,0);
                            try {
                                cores=property!=IntPtr.Zero&&CFGetTypeID(property)==CFNumberGetTypeID()&&CFNumberGetValue(property,13,out double count)
                                    &&double.IsFinite(count)&&count>0&&count<=65536&&count==Math.Truncate(count)?(int)count:null;
                                nativeCoreCounts[id]=cores;
                            }finally{if(property!=IntPtr.Zero)CFRelease(property);}
                        }
                        stats=IORegistryEntryCreateCFProperty(entry,statsKey,IntPtr.Zero,0);
                        bool dictionary=stats!=IntPtr.Zero&&CFGetTypeID(stats)==CFDictionaryGetTypeID();
                        found.Add(new MacGpuSample(id,name,dictionary?Number(stats,utilizationKey):null,dictionary?Number(stats,activityKey):null,cores));
                    }finally{if(stats!=IntPtr.Zero)CFRelease(stats);IOObjectRelease(entry);}
                }
                var currentIds=found.Select(x=>x.RegistryId).ToHashSet();
                foreach(var id in nativeCoreCounts.Keys.Where(id=>!currentIds.Contains(id)).ToArray())nativeCoreCounts.Remove(id);
                return found;
            }finally {
                if(iterator!=0)IOObjectRelease(iterator);
                if(coresKey!=IntPtr.Zero)CFRelease(coresKey);
                if(activityKey!=IntPtr.Zero)CFRelease(activityKey);
                if(utilizationKey!=IntPtr.Zero)CFRelease(utilizationKey);
                if(statsKey!=IntPtr.Zero)CFRelease(statsKey);
            }
        }
    }
}
