using System;
using System.Net.Http;

namespace HardwarePulse {
    public sealed class MacCodexQuota:FileCodexQuota {
        public MacCodexQuota():base(DefaultPath()){}
        public MacCodexQuota(string loginPath,HttpMessageHandler handler):base(loginPath,handler){}
        static string DefaultPath(){
            if(!OperatingSystem.IsMacOS())throw new PlatformNotSupportedException("macOS quota adapter requires macOS");
            return DefaultLoginPath();
        }
    }
}
