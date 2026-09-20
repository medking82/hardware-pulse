namespace HardwarePulse.Desktop;

// Composition belongs to the host; panels and Desktop consume the same results.
internal static class DesktopQuotaReaders {
    public static QuotaReading Read(string provider,bool demo,CancellationToken cancel) {
        cancel.ThrowIfCancellationRequested();
        if(provider is not ("Codex" or "Claude" or "Antigravity"))throw new ArgumentOutOfRangeException(nameof(provider));
        if(demo) {
            var rows=new List<QuotaWindow>{new(){Label="5-hour",Remaining=provider=="Codex"?72.5:provider=="Claude"?63.0:48.0,Reset=DateTimeOffset.UtcNow.AddHours(2)},new(){Label="Weekly",Remaining=54.0,Reset=DateTimeOffset.UtcNow.AddDays(4)}};
            return new(){Provider=provider,Status="Live",Observed=DateTimeOffset.UtcNow,Windows=rows,AllWindows=rows};
        }
        if(provider=="Antigravity")return OperatingSystem.IsWindows()?QuotaProviders.Read(provider,cancel):
            new(){Provider=provider,Status="Antigravity source unavailable on this platform",Observed=DateTimeOffset.UtcNow};
        if(provider=="Claude") {
            using var adapter=new ClaudeQuotaClient(OperatingSystem.IsMacOS()?MacClaudeLogin.Default().Read:ClaudeFileLogin.Default().Read);
            return adapter.Read(cancel);
        }
        using FileCodexQuota codex=OperatingSystem.IsLinux()?new LinuxCodexQuota():OperatingSystem.IsMacOS()?new MacCodexQuota():OperatingSystem.IsWindows()?new WindowsFileCodexQuota():throw new PlatformNotSupportedException();
        return codex.Read(cancel);
    }
}
