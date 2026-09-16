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
            if(args.Length!=1||args[0]!="--collector")return 2;
            string worker=AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            if(!string.Equals(Path.GetFileName(worker),"worker",StringComparison.OrdinalIgnoreCase))return 4;
            string root=Path.GetDirectoryName(worker);
            // The existing FPS server authenticates this exact UI path. Never
            // accept a peer executable, tool path or command from arguments.
            if(!File.Exists(Path.Combine(root,"HardwarePulse.exe")))return 4;
            var installed=PulsePaths.Installed();
            var paths=new PulsePaths(root,installed.State,installed.Runtime);
            try{return Collector.Run(paths);}
            catch(Exception e) {
                try{Directory.CreateDirectory(paths.State);File.WriteAllText(Path.Combine(paths.State,"collector-host-error.txt"),e.ToString());}catch{}
                return 1;
            }
        }
    }
}
