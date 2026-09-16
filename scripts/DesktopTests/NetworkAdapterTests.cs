using HardwarePulse;
using System.Net.NetworkInformation;

static class NetworkAdapterTests {
    public static void Run() {
        double seconds=0;int resolves=0;bool fail=false;
        var count=new BclNetworkCounters {Received=100,Sent=200};
        var source=new BclNetworkReadings("Ethernet 2",()=>{
            resolves++;return new Func<BclNetworkCounters>(()=>fail?throw new IOException():count);
        },()=>seconds);
        Reading Read()=>source.Read(DateTimeOffset.UtcNow);
        if(Read().values.Count!=0)throw new Exception("Network fabricated initial rate");
        seconds=2;count=new(){Received=300,Sent=260};
        var live=Read();if(live.values["netDown"]!=100||live.values["netUp"]!=30||resolves!=1)throw new Exception("Network interval/cache failed");
        fail=true;seconds=3;if(Read().values.Count!=0)throw new Exception("Stale network rate");
        fail=false;seconds=4;if(Read().values.Count!=0||resolves!=2)throw new Exception("Network recovery baseline/cache");
        seconds=5;count=new(){Received=1,Sent=1};if(Read().values.Count!=0)throw new Exception("Counter reset spike");
        if(OperatingSystem.IsWindows()) {
            var selected=NetworkInterface.GetAllNetworkInterfaces().First(x=>x.OperationalStatus==OperationalStatus.Up);
            var native=new WindowsNetworkReadings(selected.Name);native.Read(DateTimeOffset.UtcNow);Thread.Sleep(100);
            var reading=native.Read(DateTimeOffset.UtcNow);
            if(!reading.values.TryGetValue("netDown",out double down)||down<0||!double.IsFinite(down)
                ||!reading.values.TryGetValue("netUp",out double up)||up<0||!double.IsFinite(up))throw new Exception("Native Windows interface statistics unavailable");
            Console.WriteLine("PASS native Windows selected interface byte-rate read; no interface identifiers emitted");
        }
        Console.WriteLine("PASS shared BCL network: names with spaces, cached resolution, rate units, failure recovery and reset");
    }
}
