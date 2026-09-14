using System; using System.Runtime.InteropServices;
public static class PulseBackdrop {
 [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd,int attr,ref int value,int size);
 [StructLayout(LayoutKind.Sequential)] public struct Margins { public int Left,Right,Top,Bottom; }
 [DllImport("dwmapi.dll")] public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd,ref Margins margins);
 [StructLayout(LayoutKind.Sequential)] struct Blur { public uint Flags; public int Enabled; public IntPtr Region; public int Transition; }
 [StructLayout(LayoutKind.Sequential)] struct Accent { public int State,Flags,Color,Animation; }
 [StructLayout(LayoutKind.Sequential)] struct Composition { public int Attribute; public IntPtr Data; public IntPtr Size; }
 [DllImport("dwmapi.dll")] static extern int DwmEnableBlurBehindWindow(IntPtr hwnd,ref Blur value);
 [DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int left,int top,int right,int bottom);
 [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr value);
 [DllImport("user32.dll")] static extern bool SetWindowCompositionAttribute(IntPtr hwnd,ref Composition value);
 public static bool ApplyStable(IntPtr hwnd,bool solid,bool clear) {
   // Disable focus-dependent system Acrylic. Accent blur is optional and probed at runtime.
   int none=1;DwmSetWindowAttribute(hwnd,38,ref none,4);
   IntPtr region=CreateRectRgn(0,0,-1,-1), memory=IntPtr.Zero;
   if(region==IntPtr.Zero)return false;
   try {
     var blur=new Blur { Flags=3,Enabled=solid?0:1,Region=region };
     if(DwmEnableBlurBehindWindow(hwnd,ref blur)<0)return false;
     var accent=new Accent { State=(solid||clear)?0:3 };
     int size=Marshal.SizeOf(typeof(Accent));memory=Marshal.AllocHGlobal(size);
     Marshal.StructureToPtr(accent,memory,false);
     var data=new Composition { Attribute=19,Data=memory,Size=new IntPtr(size) };
     return SetWindowCompositionAttribute(hwnd,ref data);
   } catch(EntryPointNotFoundException) { return false; }
     finally { if(memory!=IntPtr.Zero)Marshal.FreeHGlobal(memory);DeleteObject(region); }
 }
}
