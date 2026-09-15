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
        // Query the connected adapter only: no scan, connection change, or persisted network identity.
        public static int? Read(string adapterId){
            Guid id;if(!Guid.TryParse(adapterId,out id))return null;IntPtr handle=IntPtr.Zero,data=IntPtr.Zero;
            try{uint version,size,type;if(WlanOpenHandle(2,IntPtr.Zero,out version,out handle)!=0)return null;
                if(WlanQueryInterface(handle,ref id,7,IntPtr.Zero,out size,out data,out type)!=0||data==IntPtr.Zero||size<Marshal.SizeOf(typeof(Connection)))return null;
                var current=(Connection)Marshal.PtrToStructure(data,typeof(Connection));return current.State==1&&current.Association.Signal<=100?(int?)current.Association.Signal:null;
            }catch(DllNotFoundException){return null;}catch(EntryPointNotFoundException){return null;}
            finally{if(data!=IntPtr.Zero)WlanFreeMemory(data);if(handle!=IntPtr.Zero)WlanCloseHandle(handle,IntPtr.Zero);}
        }
    }
}
