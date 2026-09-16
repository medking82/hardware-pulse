using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace HardwarePulse {
    public struct WindowsSystemSample {
        public bool CpuValid,MemoryValid;
        public ulong Idle,Kernel,User,TotalBytes,AvailableBytes;
        public WindowsSystemSample(bool cpuValid,ulong idle,ulong kernel,ulong user,bool memoryValid,ulong totalBytes,ulong availableBytes) {
            CpuValid=cpuValid;Idle=idle;Kernel=kernel;User=user;MemoryValid=memoryValid;TotalBytes=totalBytes;AvailableBytes=availableBytes;
        }
    }
    // Framework and modern hosts share the same driver-free mapping and native calls.
    // Serial polling; no elevation, WMI, process launch or owned timer.
    public sealed class WindowsSystemReadings {
        readonly Func<WindowsSystemSample> read;
        readonly string source=Guid.NewGuid().ToString("N");
        WindowsSystemSample? previous;
        long sequence;
        public WindowsSystemReadings() {
            if(Environment.OSVersion.Platform!=PlatformID.Win32NT)throw new PlatformNotSupportedException("Windows system counters required");
            read=ReadNative;
        }
        public WindowsSystemReadings(Func<WindowsSystemSample> read) {
            if(read==null)throw new ArgumentNullException("read");this.read=read;
        }
        public Reading Read(DateTimeOffset now) {
            var result=new Reading {time=now,identity=source+":"+(++sequence).ToString(CultureInfo.InvariantCulture),available=new Dictionary<string,bool>{{"cpuLoad",false}}};
            try {
                var current=read();
                if(current.MemoryValid&&current.TotalBytes>0&&current.AvailableBytes<=current.TotalBytes) {
                    double used=current.TotalBytes-current.AvailableBytes;
                    result.usage["ram"]=new Usage {used=used/1073741824.0,total=current.TotalBytes/1073741824.0,percent=100*used/current.TotalBytes,label="Physical memory in use"};
                }
                if(current.CpuValid&&previous.HasValue) {
                    var before=previous.Value;
                    if(current.Idle>=before.Idle&&current.Kernel>=before.Kernel&&current.User>=before.User) {
                        double idle=current.Idle-before.Idle,total=(double)(current.Kernel-before.Kernel)+(current.User-before.User);
                        // Kernel includes idle. Reset/zero intervals remain unavailable.
                        if(total>0&&idle<=total){result.values["cpuLoad"]=100*(total-idle)/total;result.available["cpuLoad"]=true;}
                    }
                }
                previous=current.CpuValid?(WindowsSystemSample?)current:null;
                if(result.usage.Count>0||result.values.Count>0)result.state="LIVE";
            } catch(IOException){Unavailable(result);}
              catch(DllNotFoundException){Unavailable(result);}
              catch(EntryPointNotFoundException){Unavailable(result);}
            return result;
        }
        void Unavailable(Reading result){previous=null;result.error="Windows system counters unavailable";}
        [StructLayout(LayoutKind.Sequential)] struct FileTime {public uint Low,High;public ulong Ticks {get{return ((ulong)High<<32)|Low;}}}
        [StructLayout(LayoutKind.Sequential)] struct MemoryStatus {
            public uint Length,Load;
            public ulong Total,Available,PageTotal,PageAvailable,VirtualTotal,VirtualAvailable,ExtendedAvailable;
        }
        [DllImport("kernel32.dll")][return:MarshalAs(UnmanagedType.Bool)] static extern bool GetSystemTimes(out FileTime idle,out FileTime kernel,out FileTime user);
        [DllImport("kernel32.dll")][return:MarshalAs(UnmanagedType.Bool)] static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
        [DllImport("kernel32.dll")] static extern ushort GetActiveProcessorGroupCount();
        static WindowsSystemSample ReadNative() {
            var memory=new MemoryStatus {Length=(uint)Marshal.SizeOf(typeof(MemoryStatus))};
            bool ram=GlobalMemoryStatusEx(ref memory);
            FileTime idle=default(FileTime),kernel=default(FileTime),user=default(FileTime);
            // This Windows 7+ API avoids pretending a single group is the whole machine.
            bool cpu=GetActiveProcessorGroupCount()==1&&GetSystemTimes(out idle,out kernel,out user);
            return new WindowsSystemSample(cpu,idle.Ticks,kernel.Ticks,user.Ticks,ram,memory.Total,memory.Available);
        }
    }
}
