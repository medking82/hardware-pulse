using System;
using System.IO;
using System.Reflection;
using System.Security.Principal;

[assembly:AssemblyTitle("Hardware Pulse Collector")]
[assembly:AssemblyProduct("Hardware Pulse")]
[assembly:AssemblyVersion("0.7.0.0")]
namespace HardwarePulse {
    // Dedicated .NET Framework worker. UI assemblies and credentials are never
    // loaded here. The installer owns elevation and task registration.
    internal static class CollectorProgram {
        static int Main(string[] args) {
            if(args.Length!=1)return 2;
            string command=args[0];
            if(command!="--collector"&&command!="--install-startup"&&command!="--remove-startup"&&command!="--enable-startup"&&command!="--disable-startup"&&command!="--startup-enabled"&&command!="--start-collector")return 2;
            string worker=AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            if(!string.Equals(Path.GetFileName(worker),"worker",StringComparison.OrdinalIgnoreCase))return 4;
            string root=Path.GetDirectoryName(worker);
            // The existing FPS server authenticates this exact UI path. Never
            // accept a peer executable, tool path or command from arguments.
            if(!File.Exists(Path.Combine(root,"HardwarePulse.exe")))return 4;
            // Management can affect elevated tasks: fixed protected payload only.
            if(command!="--collector"&&(!FpsProtocol.ProtectedTool(Path.Combine(root,"HardwarePulse.exe"))||!FpsProtocol.ProtectedTool(Path.Combine(worker,"HardwarePulse.Collector.exe"))))return 4;
            var installed=PulsePaths.Installed();
            var paths=new PulsePaths(root,installed.State,installed.Runtime);
            try{
                if(command=="--collector")return Collector.Run(paths);
                using(var store=new SchedulerStore()){
                    var startup=Startup.Shared(store,paths.Exe,WindowsIdentity.GetCurrent().User.Value);
                    if(command=="--startup-enabled")return startup.IsEnabled()?0:3;
                    if(command=="--install-startup"){
                        // Filesystem admission must finish before task mutation.
                        startup.ValidateInstall();
                        Directory.CreateDirectory(paths.Runtime);
                        if(File.Exists(paths.Stop))File.Delete(paths.Stop);
                        startup.InstallAndStartCollector();
                    }else if(command=="--remove-startup")startup.Remove();
                    else if(command=="--start-collector")startup.StartCollector();
                    else startup.SetEnabled(command=="--enable-startup");
                }
                return 0;
            }
            catch(Exception e) {
                try{Directory.CreateDirectory(paths.State);File.WriteAllText(Path.Combine(paths.State,"collector-host-error.txt"),e.ToString());}catch{}
                return 1;
            }
        }
    }
}
