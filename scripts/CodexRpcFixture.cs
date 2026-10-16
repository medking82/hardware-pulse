using System;
using System.Threading;
using System.Runtime.InteropServices;

// Build-time fixture only. Never copies executables or starts other processes.
static class CodexRpcFixture {
    [DllImport("kernel32.dll")]static extern IntPtr GetConsoleWindow();
    [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr window);
    static int Main(string[] args){
        if(IsWindowVisible(GetConsoleWindow()))return 3;
        if(string.Join(" ",args)!="-s read-only -a untrusted app-server --stdio")return 2;
#if RPC_HANG
        Thread.Sleep(60000);
#elif RPC_STDERR
        Console.Error.Write(new string('x',70000));Console.Error.Flush();
        Thread.Sleep(60000);
#else
        Console.ReadLine();Console.WriteLine("{\"id\":1,\"result\":{}}");
        Console.ReadLine();Console.ReadLine();
        Console.WriteLine("{\"id\":2,\"result\":{\"rateLimits\":{\"secondary\":{\"usedPercent\":36,\"windowDurationMins\":10080}}}}");
#endif
        return 0;
    }
}
