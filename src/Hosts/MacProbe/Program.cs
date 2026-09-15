using System;
using System.Runtime.InteropServices;
using HardwarePulse;

static class Program {
    const string Help="Pulse macOS probe (.NET 10)\nUsage: dotnet Pulse.Mac.Probe.dll\nReads CPU load and RAM estimate, waits one second, emits one JSON snapshot and exits. Network is not yet supported.\nExit codes: 0 complete, 2 invalid arguments, 3 requested metrics unavailable, 4 unsupported OS.\n";
    static int Main(string[] args){
        if(args.Length==1&&args[0]=="--help"){Console.Out.Write(Help);return 0;}
        if(args.Length!=0){Console.Error.Write(Help);return 2;}
        if(!OperatingSystem.IsMacOS()){
            Console.Error.WriteLine("This probe requires macOS Mach statistics. It does not run the Windows App.");return 4;
        }
        var cpu=new ReadingSession(new MacCpuReadings().Read);
        var memory=new ReadingSession(new MacMemoryReadings().Read);
        ProbeRuntime.Capture(cpu,memory);
        bool complete=cpu.Latest.error==null&&cpu.Latest.values.ContainsKey("cpuLoad")
            &&memory.Latest.state=="LIVE"&&memory.Latest.error==null&&memory.HasUsage("ram");
        ProbeRuntime.Write(new {
            schema=1,platform="macos",architecture=RuntimeInformation.ProcessArchitecture.ToString(),complete,
            units=new {cpuLoad="percent",ram="GiB"},cpu=cpu.Latest,memory=memory.Latest
        });
        return complete?0:3;
    }
}
