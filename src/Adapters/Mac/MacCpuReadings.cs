using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace HardwarePulse {
    public struct MacCpuTicks {
        public uint User,System,Idle,Nice;
    }

    // One serially polled source; no timer, driver, shell or persistent Mach right.
    public sealed class MacCpuReadings {
        readonly Func<MacCpuTicks> read;
        readonly string source=Guid.NewGuid().ToString("N");
        MacCpuTicks? previous;
        long sequence;
        public MacCpuReadings(){
            if(!OperatingSystem.IsMacOS())throw new PlatformNotSupportedException("macOS Mach statistics are required");
            read=ReadNative;
        }
        public MacCpuReadings(Func<MacCpuTicks> read){this.read=read??throw new ArgumentNullException("read");}

        public Reading Read(DateTimeOffset now){
            var result=new Reading {time=now,identity=source+":"+(++sequence).ToString(CultureInfo.InvariantCulture),
                available=new Dictionary<string,bool>{{"cpuLoad",false}}};
            try{
                var current=read();
                if(previous.HasValue){
                    var before=previous.Value;
                    // A reset or 32-bit wrap establishes a new baseline, never a spike.
                    if(current.User>=before.User&&current.System>=before.System&&current.Idle>=before.Idle&&current.Nice>=before.Nice){
                        double busy=(double)(current.User-before.User)+(current.System-before.System)+(current.Nice-before.Nice);
                        double total=busy+(current.Idle-before.Idle);
                        if(total>0){result.values["cpuLoad"]=100*busy/total;result.available["cpuLoad"]=true;result.state="LIVE";}
                    }
                }
                previous=current;
            }catch(Exception e) when(e is IOException||e is DllNotFoundException||e is EntryPointNotFoundException){
                previous=null;result.error=e.Message;
            }
            return result;
        }

        const string LibSystem="/usr/lib/libSystem.B.dylib";
        static readonly Lazy<uint> taskPort=new Lazy<uint>(()=>{
            // mach_task_self() is a macro over an exported uint, not a function.
            IntPtr library=NativeLibrary.Load(LibSystem);
            try{return unchecked((uint)Marshal.ReadInt32(NativeLibrary.GetExport(library,"mach_task_self_")));}
            finally{NativeLibrary.Free(library);}
        });
        [DllImport(LibSystem)] static extern uint mach_host_self();
        [DllImport(LibSystem)] static extern int host_statistics(uint host,int flavor,[Out] uint[] ticks,ref uint count);
        [DllImport(LibSystem)] static extern int mach_port_deallocate(uint task,uint port);

        static MacCpuTicks ReadNative(){
            uint task=taskPort.Value;
            if(task==0)throw new IOException("Mach task port unavailable");
            uint host=mach_host_self();
            if(host==0)throw new IOException("Mach host port unavailable");
            try{
                uint count=4;var ticks=new uint[4];
                int status=host_statistics(host,3,ticks,ref count); // HOST_CPU_LOAD_INFO
                if(status!=0||count!=4)throw new IOException("Mach CPU statistics unavailable: "+status.ToString(CultureInfo.InvariantCulture));
                return new MacCpuTicks {User=ticks[0],System=ticks[1],Idle=ticks[2],Nice=ticks[3]};
            }finally{
                int status=mach_port_deallocate(task,host);
                if(status!=0)throw new IOException("Mach host port release failed: "+status.ToString(CultureInfo.InvariantCulture));
            }
        }
    }
}
