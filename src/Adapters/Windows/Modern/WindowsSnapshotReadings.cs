#nullable enable
using System.Security.Principal;
using System.Text.Json;

namespace HardwarePulse;

// Consume the established WPF collector protocol without elevation or a second collector.
public sealed class WindowsSnapshotReadings {
    const int Limit=8*1024*1024;
    static readonly JsonSerializerOptions Options=new(){IncludeFields=true,MaxDepth=64};
    readonly string path;
    public WindowsSnapshotReadings(string path){this.path=Path.GetFullPath(path);}
    public static WindowsSnapshotReadings Default() {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        using var identity=WindowsIdentity.GetCurrent();
        string sid=identity.User?.Value??throw new InvalidOperationException("Current user unavailable");
        return new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"HardwarePulse",sid,"runtime","snapshot.json"));
    }
    public Reading Read(DateTimeOffset now)=>SensorProfile.Read(path,now);
    internal static RawSnapshot ReadRaw(string path) {
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        long length=stream.Length;
        if(length<=0||length>Limit)throw new InvalidDataException("Snapshot unavailable");
        var bytes=new byte[(int)length];stream.ReadExactly(bytes);
        if(stream.ReadByte()!=-1)throw new InvalidDataException("Snapshot changed while reading");
        int offset=bytes.Length>=3&&bytes[0]==239&&bytes[1]==187&&bytes[2]==191?3:0;
        return JsonSerializer.Deserialize<RawSnapshot>(bytes.AsSpan(offset),Options)??throw new InvalidDataException("Snapshot unavailable");
    }
}
