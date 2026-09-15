using System;
using HardwarePulse;

static class CodexTests {
    public static void Run(){
        FileCodexQuotaTests.Run((path,handler)=>new LinuxCodexQuota(path,handler));
        if(!OperatingSystem.IsLinux()){
            bool rejected=false;try{using(var adapter=new LinuxCodexQuota()){};}catch(PlatformNotSupportedException){rejected=true;}
            if(!rejected)throw new Exception("Default Linux source must reject unsupported OS");
        }
    }
}