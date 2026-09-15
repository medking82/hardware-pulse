using System;
using System.Runtime.InteropServices;

static class Program {
    static void Main(){
        string expected=Environment.GetEnvironmentVariable("PULSE_TEST_ARCH");
        if(!string.IsNullOrEmpty(expected)&&expected!=RuntimeInformation.ProcessArchitecture.ToString())throw new Exception("Unexpected process architecture");
        Console.WriteLine(RuntimeInformation.OSDescription+" / "+RuntimeInformation.ProcessArchitecture+" / "+RuntimeInformation.FrameworkDescription);
        CodexQuotaTests.Run();
    }
}
