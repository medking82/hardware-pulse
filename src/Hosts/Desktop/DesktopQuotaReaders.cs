namespace HardwarePulse.Desktop;

// Composition belongs to the host. Panels and floating presentation receive readings only.
static class DesktopQuotaReaders {
    public static QuotaReading Read(bool demo,string provider,CancellationToken cancel) {
        cancel.ThrowIfCancellationRequested();
        if(demo) {
            var rows=new List<QuotaWindow>{new(){Label="5-hour",Remaining=72.5,Reset=DateTimeOffset.UtcNow.AddHours(2)},new(){Label="Weekly",Remaining=54,Reset=DateTimeOffset.UtcNow.AddDays(4)}};
            return new(){Provider=provider,Status="Live",Observed=DateTimeOffset.UtcNow,Windows=rows,AllWindows=rows};
        }
        if(provider=="Antigravity")return OperatingSystem.IsWindows()?QuotaProviders.Read(provider,cancel):
            new(){Provider=provider,Status="Antigravity source unavailable on this platform",Observed=DateTimeOffset.UtcNow};
        if(provider=="Claude") {
            Func<CancellationToken,string> login=OperatingSystem.IsMacOS()?MacClaudeLogin.Default().Read:
                OperatingSystem.IsWindows()?WindowsClaudeLogin.Default().Read:ClaudeFileLogin.Default().Read;
            using var claude=new ClaudeQuotaClient(login);return claude.Read(cancel);
        }
        using FileCodexQuota adapter=OperatingSystem.IsLinux()?new LinuxCodexQuota():OperatingSystem.IsMacOS()?new MacCodexQuota():OperatingSystem.IsWindows()?new WindowsFileCodexQuota():throw new PlatformNotSupportedException();
        return adapter.Read(cancel);
    }
}
