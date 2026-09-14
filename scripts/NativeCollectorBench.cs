using System;
using System.IO;
using HardwarePulse;
internal static class NativeCollectorBench {
    static int Main(string[] args){if(args.Length!=1)return 2;string state=Path.GetFullPath(args[0]);return Collector.Run(new PulsePaths(AppDomain.CurrentDomain.BaseDirectory,state,Path.Combine(state,"runtime")),0,"Local\\HardwarePulseNativeCollectorBenchmark");}
}
