#nullable enable
using System.Text;

namespace HardwarePulse;

// Bounded modern JSON bridge for the unchanged Windows quota provider.
internal static class QuotaData {
    internal static object Parse(string text) {
        if(text.Length>1048576)throw new QuotaFailure("Quota unavailable");
        using var stream=new MemoryStream(Encoding.UTF8.GetBytes(text));
        return QuotaJson.ReadGraph(stream,CancellationToken.None).GetAwaiter().GetResult();
    }
}
