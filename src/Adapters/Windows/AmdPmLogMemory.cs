// SPDX-License-Identifier: MIT
using System;
using System.Runtime.InteropServices;

namespace HardwarePulse {
    // ADL can revoke its PMLog mapping while the collector is running. Checking
    // a pointer before PtrToStructure would still race a subsequent unmap.
    public static class AmdPmLogMemory {
        [DllImport("kernel32.dll",SetLastError=true)]
        [return:MarshalAs(UnmanagedType.Bool)]
        static extern bool ReadProcessMemory(IntPtr process,IntPtr address,[Out] byte[] destination,UIntPtr size,out UIntPtr copied);

        public static object Read(IntPtr address,Type structureType){
            if(structureType==null||structureType.FullName!="LibreHardwareMonitor.Interop.AtiAdlxx+ADLPMLogData")
                throw new ArgumentException("Expected the pinned inline AMD PMLog structure","structureType");
            var snapshot=new byte[Marshal.SizeOf(structureType)];
            UIntPtr copied;
            // Only an owned, pinned copy is passed to the CLR marshaler. A
            // cleared timestamp makes LHM reject PMLog and use its existing
            // queried OD8 fallback; partial data must never look current.
            if(address==IntPtr.Zero||!ReadProcessMemory(new IntPtr(-1),address,snapshot,new UIntPtr((uint)snapshot.Length),out copied)||copied.ToUInt64()!=(ulong)snapshot.Length)
                Array.Clear(snapshot,0,snapshot.Length);
            var pinned=GCHandle.Alloc(snapshot,GCHandleType.Pinned);
            try{return Marshal.PtrToStructure(pinned.AddrOfPinnedObject(),structureType);}
            finally{pinned.Free();}
        }
    }
}
