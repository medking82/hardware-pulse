using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using HardwarePulse;

static class WindowsCapabilityProbe {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    // COFF machine is file metadata, not a claim of execution or hardware support.
    static string Machine(Stream stream){
        using(var reader=new BinaryReader(stream)){
            if(stream.Length<64||reader.ReadUInt16()!=0x5a4d)return "invalid";
            stream.Position=60;long pe=reader.ReadUInt32();
            if(pe<64||pe>stream.Length-24)return "invalid";
            stream.Position=pe;if(reader.ReadUInt32()!=0x4550)return "invalid";
            ushort machine=reader.ReadUInt16();
            return machine==0x8664?"AMD64":machine==0xaa64?"ARM64":machine==0xa641?"ARM64EC":machine==0x14c?"I386":"unknown";
        }
    }
    static string Image(string path){
        try{using(var stream=File.OpenRead(path))return Machine(stream);}
        catch(FileNotFoundException){return "missing";}catch(DirectoryNotFoundException){return "missing";}
        catch(IOException){return "unreadable";}catch(UnauthorizedAccessException){return "unreadable";}
    }
    static object PawnIo(){
        bool? registered=null;
        try{using(var machine=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64))
            using(var key=machine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\PawnIO"))registered=key!=null;}
        catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}
        string library=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"PawnIO","PawnIOLib.dll");
        return new {serviceRegistered=registered,libraryMachine=Image(library),driverUsability="not-tested"};
    }
    static object Report(string package){
        bool ram=false;try{var value=WindowsHardware.ReadMemory();ram=value!=null&&value.totalGb>0&&value.usedGb>=0&&value.usedGb<=value.totalGb;}catch{}
        int? links=null;try{links=new WindowsNetwork(id=>id).Read().Length;}catch{}
        var images=new Dictionary<string,string>();
        if(package!=null)foreach(string file in new[]{"HardwarePulse.exe","Pulse.Core.dll","Pulse.Adapters.Windows.dll","tools/PresentMon.exe","lib/LibreHardwareMonitorLib.dll"})images[file]=Image(Path.Combine(package,file));
        return new {
            schema=1,osArchitecture=RuntimeInformation.OSArchitecture.ToString(),processArchitecture=RuntimeInformation.ProcessArchitecture.ToString(),
            framework=RuntimeInformation.FrameworkDescription,ramRead=ram,networkInterfaceCount=links,
            packageInspected=package!=null,packageMachines=images,pawnIo=PawnIo(),
            cpuTemperature="not-tested",fans="not-tested",gameFps="not-tested",desktopIntegration="not-tested",
            note="I386 may be managed AnyCPU. PE metadata and driver presence do not prove runtime support. No driver or package code is loaded."
        };
    }
    static void SelfTest(){
        string expected=Environment.GetEnvironmentVariable("PULSE_TEST_ARCH");
        Check(string.IsNullOrEmpty(expected)||expected==RuntimeInformation.ProcessArchitecture.ToString(),"Unexpected native probe architecture");
        foreach(ushort machine in new ushort[]{0x8664,0xaa64,0xa641,0x14c,0xffff}){
            var bytes=new byte[88];bytes[0]=0x4d;bytes[1]=0x5a;bytes[60]=64;bytes[64]=0x50;bytes[65]=0x45;bytes[68]=(byte)machine;bytes[69]=(byte)(machine>>8);
            string value=Machine(new MemoryStream(bytes));
            Check(value==(machine==0x8664?"AMD64":machine==0xaa64?"ARM64":machine==0xa641?"ARM64EC":machine==0x14c?"I386":"unknown"),"PE machine decoding");
            bytes[60]=255;Check(Machine(new MemoryStream(bytes))=="invalid","Out-of-file header rejected");
        }
        Check(Machine(new MemoryStream(new byte[8]))=="invalid","Short input rejected");
        var memory=WindowsHardware.ReadMemory();Check(memory!=null&&memory.totalGb>0,"Native RAM capability");
        Console.WriteLine("PASS native Windows capability probe: architecture, PE bounds and RAM read; no package execution");
    }
    static int Main(string[] args){
        if(args.Length==1&&args[0]=="--help"){Console.WriteLine("WindowsCapabilityProbe.exe [--package DIRECTORY | --self-test]");return 0;}
        if(args.Length==1&&args[0]=="--self-test"){SelfTest();Console.WriteLine(new JavaScriptSerializer().Serialize(Report(null)));return 0;}
        if(args.Length!=0&&(args.Length!=2||args[0]!="--package"||!Directory.Exists(args[1]))){Console.Error.WriteLine("Use --package with an existing directory, or --help.");return 2;}
        Console.WriteLine(new JavaScriptSerializer().Serialize(Report(args.Length==2?args[1]:null)));return 0;
    }
}
