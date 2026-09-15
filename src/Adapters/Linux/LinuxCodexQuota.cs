using System;
using System.Net.Http;

namespace HardwarePulse {
    public sealed class LinuxCodexQuota:FileCodexQuota {
        public LinuxCodexQuota():base(DefaultPath()){}
        public LinuxCodexQuota(string loginPath,HttpMessageHandler handler):base(loginPath,handler){}
        static string DefaultPath(){
            if(!OperatingSystem.IsLinux())throw new PlatformNotSupportedException("Linux quota adapter requires Linux");
            return DefaultLoginPath();
        }
    }
}