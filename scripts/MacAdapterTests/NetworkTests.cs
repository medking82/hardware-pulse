using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using HardwarePulse;

static class NetworkTests {
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    public static void Run(bool live){
        CheckResolution();
        double clock=10;int failure=0;
        var counters=new MacNetworkCounters {Received=1000,Sent=2000};
        var reader=new MacNetworkReadings("en0",()=>failure==1?throw new IOException("Missing interface"):
            failure==2?throw new NetworkInformationException():counters,()=>clock);
        var now=DateTimeOffset.UtcNow;var first=reader.Read(now);
        Check(first.values.Count==0&&first.error==null&&!first.available["netDown"],"First network sample warms up");
        counters.Received=3000;counters.Sent=6000;clock=12;
        var reading=reader.Read(now.AddDays(-1));
        Check(reading.values["netDown"]==1000&&reading.values["netUp"]==2000&&reading.state=="LIVE","Bytes/sec independent of wall clock");
        Check(reading.names["Network"]=="en0"&&reading.identity!=first.identity&&reading.values.Count==2&&reading.usage.Count==0,"Identity, interface and supported metrics only");
        clock=14;Check(reader.Read(now).values["netDown"]==0,"Idle is real zero");
        failure=1;Check(reader.Read(now).error!=null,"Missing interface unavailable");failure=0;
        clock=15;Check(reader.Read(now).values.Count==0,"Reappearing interface warms up");
        failure=2;Check(reader.Read(now).state=="OFFLINE","Native statistics failure unavailable");failure=0;
        clock=16;Check(reader.Read(now).values.Count==0,"Native failure recovery warms up");
        counters.Sent=-1;Check(reader.Read(now).error!=null,"Negative BCL counters rejected");
        counters.Sent=6000;clock=17;Check(reader.Read(now).values.Count==0,"Invalid counter recovery warms up");
        clock=double.NaN;Check(reader.Read(now).error!=null,"Invalid clock rejected");
        if(live){
            Check(OperatingSystem.IsMacOS(),"Live network requires macOS");
            var network=new MacNetworkReadings("lo0");var session=new ReadingSession(network.Read);
            session.Poll(DateTimeOffset.UtcNow);
            Check(session.Latest.error==null,"Live loopback baseline");
            using(var receiver=new UdpClient(new IPEndPoint(IPAddress.Loopback,0)))
            using(var sender=new UdpClient()){
                receiver.Client.ReceiveTimeout=3000;
                var endpoint=(IPEndPoint)receiver.Client.LocalEndPoint;
                sender.Send(new byte[1024],1024,endpoint);
                IPEndPoint peer=null;Check(receiver.Receive(ref peer).Length==1024,"Local UDP traffic");
            }
            Thread.Sleep(100);session.Poll(DateTimeOffset.UtcNow);
            Check(session.Latest.state=="LIVE"&&session.Latest.values["netDown"]>0&&session.Latest.values["netUp"]>0,"Live loopback throughput through Core");
            Check(new MacNetworkReadings("pulse_missing_interface").Read(now).error!=null,"Exact missing native interface unavailable");
            Console.WriteLine("PASS macOS live loopback throughput / Core ReadingSession");
        }else if(!OperatingSystem.IsMacOS()){
            bool rejected=false;try{new MacNetworkReadings("lo0");}catch(PlatformNotSupportedException){rejected=true;}
            Check(rejected,"Reject native network calls on unsupported OS");
        }
        Console.WriteLine("PASS macOS network fixtures");
    }
    static void CheckResolution(){
        int resolutions=0,reads=0;double clock=1;bool missing=false,broken=false;
        var counters=new MacNetworkCounters {Received=100,Sent=200};
        Func<Func<MacNetworkCounters>> resolve=()=>{
            resolutions++;
            if(missing)throw new IOException("Removed interface");
            return ()=>{reads++;if(broken)throw new NetworkInformationException();return counters;};
        };
        var reader=new MacNetworkReadings("en0",resolve,()=>clock);var now=DateTimeOffset.UtcNow;
        reader.Read(now);clock++;counters.Received+=10;counters.Sent+=20;
        Check(reader.Read(now).values["netDown"]==10&&resolutions==1&&reads==2,"Reuse selection, always read fresh counters");
        broken=true;Check(reader.Read(now).error!=null,"Failed cached statistics unavailable");broken=false;missing=true;
        Check(reader.Read(now).error!=null&&resolutions==2,"Failed reader discarded before resolving again");
        missing=false;clock++;counters=new MacNetworkCounters {Received=1,Sent=2};
        Check(reader.Read(now).values.Count==0&&resolutions==3,"Reappearing interface warms a new baseline");
        clock++;counters.Received+=4;counters.Sent+=8;
        Check(reader.Read(now).values["netUp"]==8&&resolutions==3,"Recovered selection reused with fresh counters");
        counters=new MacNetworkCounters();clock++;
        Check(reader.Read(now).values.Count==0,"Same-name counter reset cannot spike");
        clock++;counters.Sent=5;
        Check(reader.Read(now).values["netUp"]==5,"Same-name counter reset recovers");
        Console.WriteLine("PASS macOS network selection reuse, invalidation and recovery");
    }
}
