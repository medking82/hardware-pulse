using System;
using System.Text.Json;
using System.Threading;

namespace HardwarePulse {
    // Shared host source, intentionally outside Core: owns CLI timing and output.
    internal static class ProbeRuntime {
        internal static void Capture(params ReadingSession[] sessions){
            var now=DateTimeOffset.UtcNow;
            foreach(var session in sessions)session?.Poll(now);
            Thread.Sleep(1000);
            now=DateTimeOffset.UtcNow;
            foreach(var session in sessions)session?.Poll(now);
        }
        internal static void Write(object snapshot){
            Console.Out.WriteLine(JsonSerializer.Serialize(snapshot,new JsonSerializerOptions {IncludeFields=true,WriteIndented=true}));
        }
    }
}
