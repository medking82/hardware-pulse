using System.Text.Json;
using HardwarePulse;

static class WindowsSnapshotTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run() {
        string directory=Directory.CreateTempSubdirectory("pulse-snapshot-").FullName;
        try {
            var now=DateTimeOffset.UtcNow;
            string path=Path.Combine(directory,"snapshot.json");
            var reader=new WindowsSnapshotReadings(path);
            Check(reader.Read(now).state=="OFFLINE","Missing collector remains unavailable");
            var raw=new RawSnapshot{schema=2,pid=123,sequence=1,time=now.ToString("o"),sensors=[
                new Sensor{id="/cpu/0/temp",name="CPU Package",hardware="Fixture CPU",hardwareId="/cpu/0",hardwareType="Cpu",type="Temperature",value=59},
                new Sensor{id="/cpu/0/load",name="CPU Total",hardware="Fixture CPU",hardwareId="/cpu/0",hardwareType="Cpu",type="Load",value=17}]};
            string json=JsonSerializer.Serialize(raw,new JsonSerializerOptions{IncludeFields=true});
            File.WriteAllText(path,json);
            var live=reader.Read(now);var direct=SensorProfile.Parse(raw,now);
            Check(live.state=="LIVE"&&live.values["cpu"]==59&&live.values["cpuLoad"]==17,"Modern snapshot consumes original collector protocol");
            Check(live.values.OrderBy(x=>x.Key).SequenceEqual(direct.values.OrderBy(x=>x.Key)),"Same SensorProfile semantic mapping");
            Check(reader.Read(now.AddSeconds(16)).state=="STALE","Expired readings never appear live");
            File.WriteAllText(path,json,new System.Text.UTF8Encoding(true));
            Check(reader.Read(now).state=="LIVE","Existing UTF8 BOM snapshot supported");
            foreach(string invalid in new[]{"{broken","null","{\"schema\":99}",new string(' ',8*1024*1024+1)}) {
                File.WriteAllText(path,invalid);Check(reader.Read(now).state=="OFFLINE","Invalid or oversized snapshot rejected");
            }
            Console.WriteLine("PASS Windows snapshot: read-only protocol, parser parity, freshness, BOM and bounded input");
        } finally {
            Check(Path.GetFullPath(directory).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase),"Temporary fixture boundary");
            Directory.Delete(directory,true);
        }
    }
}
