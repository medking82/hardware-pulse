namespace HardwarePulse.Desktop;

// Both shared views retain the original WPF ten-minute freshness boundary.
internal static class QuotaPresentation {
    public static string Status(QuotaReading reading,DateTimeOffset now)=>
        reading.Status=="Live"&&now-reading.Observed>TimeSpan.FromMinutes(10)?"Quota stale":reading.Status;
}
