namespace HardwarePulse;

public sealed class WindowsNetworkReadings {
    readonly BclNetworkReadings inner;
    public WindowsNetworkReadings(string name) {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        inner=new BclNetworkReadings(name);
    }
    public Reading Read(DateTimeOffset now)=>inner.Read(now);
}

public sealed class WindowsFileCodexQuota:FileCodexQuota {
    public WindowsFileCodexQuota():base(LoginPath()){}
    static string LoginPath() {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        return DefaultLoginPath();
    }
}
