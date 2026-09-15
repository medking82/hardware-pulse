using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace HardwarePulse {
    // Shared ownership of host send rights for the CPU and RAM readers.
    internal static class MacMach {
        internal const string LibSystem="/usr/lib/libSystem.B.dylib";
        static readonly Lazy<uint> taskPort=new Lazy<uint>(()=>{
            IntPtr library=NativeLibrary.Load(LibSystem);
            try{return unchecked((uint)Marshal.ReadInt32(NativeLibrary.GetExport(library,"mach_task_self_")));}
            finally{NativeLibrary.Free(library);}
        });
        [DllImport(LibSystem)] static extern uint mach_host_self();
        [DllImport(LibSystem)] static extern int mach_port_deallocate(uint task,uint port);
        internal static uint AcquireHost(){
            if(taskPort.Value==0)throw new IOException("Mach task port unavailable");
            uint host=mach_host_self();
            if(host==0)throw new IOException("Mach host port unavailable");
            return host;
        }
        internal static void ReleaseHost(uint host){
            int status=mach_port_deallocate(taskPort.Value,host);
            if(status!=0)throw new IOException("Mach host port release failed: "+status.ToString(CultureInfo.InvariantCulture));
        }
    }
}
