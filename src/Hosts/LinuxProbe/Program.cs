using System;
using System.Runtime.InteropServices;
using HardwarePulse;

static class Program {
    const string Help="Pulse Linux probe (.NET 10)\nUsage: dotnet Pulse.Linux.Probe.dll [--interface NAME]\nReads CPU/RAM and optionally one network interface, waits one second, emits one JSON snapshot and exits.\nExit codes: 0 complete, 2 invalid arguments, 3 requested metrics unavailable, 4 unsupported OS.\n";
    static int Main(string[] args){
        if(args.Length==1&&args[0]=="--help"){Console.Out.Write(Help);return 0;}
        string networkName=null;
        if(args.Length!=0){
            if(args.Length!=2||args[0]!="--interface"||string.IsNullOrWhiteSpace(args[1])
                ||args[1].IndexOfAny(new[]{':','\r','\n',' ','\t'})>=0){Console.Error.Write(Help);return 2;}
            networkName=args[1];
        }
        if(!OperatingSystem.IsLinux()){
            Console.Error.WriteLine("This probe requires Linux procfs. It does not run the Windows App.");return 4;
        }
        var cpuRam=new ReadingSession(new LinuxReadings().Read);
        var network=networkName==null?null:new ReadingSession(new LinuxNetworkReadings(networkName).Read);
        ProbeRuntime.Capture(cpuRam,network);
        bool complete=cpuRam.Latest.error==null&&cpuRam.Latest.values.ContainsKey("cpuLoad")&&cpuRam.HasUsage("ram")
            &&(network==null||(network.Latest.error==null&&network.Latest.values.ContainsKey("netDown")&&network.Latest.values.ContainsKey("netUp")));
        ProbeRuntime.Write(new {
            schema=1,platform="linux",architecture=RuntimeInformation.ProcessArchitecture.ToString(),complete,
            units=new {cpuLoad="percent",ram="GiB",network="bytes/second"},
            cpuRam=cpuRam.Latest,network=network?.Latest
        });
        return complete?0:3;
    }
}
