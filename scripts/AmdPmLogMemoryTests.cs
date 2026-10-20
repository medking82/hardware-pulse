using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

static class AmdPmLogMemoryTests {
    [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr VirtualAlloc(IntPtr address,UIntPtr size,uint allocation,uint protection);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool VirtualProtect(IntPtr address,UIntPtr size,uint protection,out uint old);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool VirtualFree(IntPtr address,UIntPtr size,uint freeType);
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static uint Version(object value){return (uint)value.GetType().GetField("ulVersion").GetValue(value);}
    static void Empty(object value){
        Check(Version(value)==0,"Invalid PMLog memory retained a partial header");
        Check((ulong)value.GetType().GetField("ulLastUpdated").GetValue(value)==0,"Invalid PMLog memory looks current");
        var values=(uint[])value.GetType().GetField("ulValues").GetValue(value);
        Check(values!=null&&values.Length>0,"Unavailable PMLog must retain its inline array layout");
        foreach(uint item in values)Check(item==0,"Invalid PMLog memory retained a partial sensor");
    }
    static int Main(string[] args){
        if(args.Length!=1)return 2;
        string app=Path.GetFullPath(args[0]);
        AppDomain.CurrentDomain.AssemblyResolve+=delegate(object sender,ResolveEventArgs e){
            string name=new AssemblyName(e.Name).Name+".dll";
            foreach(string directory in new[]{app,Path.Combine(app,"lib")}){
                string path=Path.Combine(directory,name);if(File.Exists(path))return Assembly.LoadFrom(path);
            }return null;
        };
        var adapters=Assembly.LoadFrom(Path.Combine(app,"Pulse.Adapters.Windows.dll"));
        var helper=adapters.GetType("HardwarePulse.AmdPmLogMemory");
        Check(helper!=null,"Guarded AMD PMLog reader is missing; driver-owned memory is marshaled directly");
        var read=(Func<IntPtr,Type,object>)Delegate.CreateDelegate(typeof(Func<IntPtr,Type,object>),helper.GetMethod("Read"));
        var lhm=Assembly.LoadFrom(Path.Combine(app,"lib","LibreHardwareMonitorLib.dll"));
        var type=lhm.GetType("LibreHardwareMonitor.Interop.AtiAdlxx+ADLPMLogData",true);
        int size=Marshal.SizeOf(type);
        IntPtr owned=Marshal.AllocHGlobal(size);
        try{
            var data=new byte[size];Array.Copy(BitConverter.GetBytes(0x12345678U),data,4);
            Array.Copy(BitConverter.GetBytes(1000U),0,data,4,4);
            Array.Copy(BitConverter.GetBytes(123456789UL),0,data,8,8);
            Array.Copy(BitConverter.GetBytes(42U),0,data,16,4);
            Marshal.Copy(data,0,owned,size);
            var value=read(owned,type);
            Check(Version(value)==0x12345678U,"Readable PMLog header changed");
            Check(((uint[])type.GetField("ulValues").GetValue(value))[0]==42,"Readable PMLog sensors changed");
            Marshal.WriteInt32(owned,16,99);
            Check(((uint[])type.GetField("ulValues").GetValue(value))[0]==42,"PMLog snapshot aliases driver memory");
        }finally{Marshal.FreeHGlobal(owned);}
        Empty(read(IntPtr.Zero,type));Empty(read(new IntPtr(1),type));
        int page=Environment.SystemPageSize;
        IntPtr pages=VirtualAlloc(IntPtr.Zero,new UIntPtr((uint)(page*2)),0x3000,0x04);
        Check(pages!=IntPtr.Zero,"Could not allocate isolated PMLog guard-page fixture");
        try{
            uint old;Check(VirtualProtect(IntPtr.Add(pages,page),new UIntPtr((uint)page),0x01,out old),"Could not protect fixture page");
            IntPtr partial=IntPtr.Add(pages,page-8);
            Marshal.WriteInt32(partial,123);Marshal.WriteInt32(partial,4,1000);
            Empty(read(partial,type));
            Empty(read(IntPtr.Add(pages,page),type));
        }finally{Check(VirtualFree(pages,UIntPtr.Zero,0x8000),"Could not release fixture pages");}
        Console.WriteLine("PASS AMD PMLog: valid detached snapshot; null, invalid and guard-page pointers stay unavailable without a native crash");
        return 0;
    }
}
