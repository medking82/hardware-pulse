using System;
using System.Runtime.InteropServices;

namespace HardwarePulse {
    public static class WifiSignal {
        [StructLayout(LayoutKind.Sequential)] struct Ssid {public uint Length;[MarshalAs(UnmanagedType.ByValArray,SizeConst=32)]public byte[] Bytes;}
        [StructLayout(LayoutKind.Sequential)] struct Association {public Ssid Ssid;public uint BssType;[MarshalAs(UnmanagedType.ByValArray,SizeConst=6)]public byte[] Bssid;public uint Phy,PhyIndex,Signal,Rx,Tx;}
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Connection {public uint State,Mode;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string Profile;public Association Association;public int SecurityEnabled,OneXEnabled;public uint Auth,Cipher;}
        [DllImport("wlanapi.dll")]static extern uint WlanOpenHandle(uint version,IntPtr reserved,out uint negotiated,out IntPtr handle);
        [DllImport("wlanapi.dll")]static extern uint WlanQueryInterface(IntPtr handle,ref Guid id,uint opcode,IntPtr reserved,out uint size,out IntPtr data,out uint type);
        [DllImport("wlanapi.dll")]static extern uint WlanCloseHandle(IntPtr handle,IntPtr reserved);
        [DllImport("wlanapi.dll")]static extern void WlanFreeMemory(IntPtr data);
        internal delegate uint Query(uint opcode,out uint size,out IntPtr data);
        static readonly Action<IntPtr> releaseNative=WlanFreeMemory;
        internal static int? ReadSignal(Query query,Action<IntPtr> release){
            IntPtr data=IntPtr.Zero;
            try{
                uint size,status=query(19,out size,out data);
                // realtime_connection_quality excludes location-sensitive information.
                // Its fixed 24-byte prefix stores ulLinkQuality at byte offset 4.
                if(status==0){
                    if(data==IntPtr.Zero||size<24)return null;
                    uint signal=unchecked((uint)Marshal.ReadInt32(data,4));return signal<=100?(int?)signal:null;
                }
                // Older Windows versions may not implement opcode 19. Access denied,
                // disconnection and malformed replies must not trigger a sensitive query.
                if(status!=50&&status!=87)return null;
                if(data!=IntPtr.Zero){release(data);data=IntPtr.Zero;}
                if(query(7,out size,out data)!=0||data==IntPtr.Zero||size<Marshal.SizeOf(typeof(Connection)))return null;
                var current=(Connection)Marshal.PtrToStructure(data,typeof(Connection));return current.State==1&&current.Association.Signal<=100?(int?)current.Association.Signal:null;
            }finally{if(data!=IntPtr.Zero)release(data);}
        }
        // Query the connected adapter only: no scan, connection change, or persisted network identity.
        public static int? Read(string adapterId){
            Guid id;if(!Guid.TryParse(adapterId,out id))return null;IntPtr handle=IntPtr.Zero;
            try{uint version;if(WlanOpenHandle(2,IntPtr.Zero,out version,out handle)!=0)return null;
                return ReadSignal(delegate(uint opcode,out uint size,out IntPtr data){uint type;return WlanQueryInterface(handle,ref id,opcode,IntPtr.Zero,out size,out data,out type);},releaseNative);
            }catch(DllNotFoundException){return null;}catch(EntryPointNotFoundException){return null;}
            finally{if(handle!=IntPtr.Zero)WlanCloseHandle(handle,IntPtr.Zero);}
        }
    }
}
