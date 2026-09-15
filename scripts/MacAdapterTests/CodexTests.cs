using System;
using HardwarePulse;

static class CodexTests {
    public static void Run(){
        FileCodexQuotaTests.Run((path,handler)=>new MacCodexQuota(path,handler));
        if(!OperatingSystem.IsMacOS()){
            bool rejected=false;try{using(var adapter=new MacCodexQuota()){};}catch(PlatformNotSupportedException){rejected=true;}
            if(!rejected)throw new Exception("Default macOS source must reject unsupported OS");
        }
    }
}
