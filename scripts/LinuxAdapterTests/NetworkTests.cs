using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using HardwarePulse;

static class NetworkTests {
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static string Line(string name,ulong rx,ulong tx){return name+": "+rx+" 0 0 0 0 0 0 0 "+tx+" 0 0 0 0 0 0 0\n";}
    public static void Run(bool live){
        double clock=10;bool fail=false;
        string input=Line("eth0",1000,2000)+Line("eth01",900000,900000);
        var reader=new LinuxNetworkReadings("eth0",()=>fail?throw new IOException("Fixture unavailable"):input,()=>clock);
        var now=DateTimeOffset.UtcNow;
        Check(reader.Read(now).values.Count==0,"First network sample warms up");
        clock=12;input=Line("eth0",3000,6000)+Line("eth01",999999,999999);
        var reading=reader.Read(now.AddDays(-1));
        Check(reading.values["netDown"]==1000&&reading.values["netUp"]==2000,"Exact interface, bytes/sec and monotonic time");
        Check(reading.names["Network"]=="eth0"&&!reading.values.ContainsKey("netLink"),"Named interface without invented link speed");
        clock=14;Check(reader.Read(now).values["netDown"]==0,"Idle network is real zero");
        Check(reader.Read(now).values.Count==0,"Zero interval unavailable");
        clock=13;Check(reader.Read(now).values.Count==0,"Backwards clock resets baseline");
        clock=15;input=Line("eth0",1,1);Check(reader.Read(now).values.Count==0,"Counter reset warms up");
        clock=16;input=Line("eth0",2,3);Check(reader.Read(now).values["netUp"]==2,"Counter recovery");
        input=Line("eth01",3,3);Check(reader.Read(now).error!=null,"Missing exact interface");
        input=Line("eth0",3,4);clock=18;Check(reader.Read(now).values.Count==0,"Reappearing interface warms up");
        fail=true;Check(reader.Read(now).state=="OFFLINE","Read failure unavailable");fail=false;
        clock=19;Check(reader.Read(now).values.Count==0,"Read recovery warms up");
        input=Line("eth0",4,5)+Line("eth0",4,5);Check(reader.Read(now).error!=null,"Duplicate rows rejected");
        input="eth0: -1 0 0 0 0 0 0 0 2 0 0 0 0 0 0 0";Check(reader.Read(now).error!=null,"Negative counter rejected");
        input=Line("eth0",ulong.MaxValue-2,ulong.MaxValue-2);clock=20;reader.Read(now);
        input=Line("eth0",ulong.MaxValue,ulong.MaxValue);clock=21;
        Check(reader.Read(now).values["netDown"]==2,"Subtract counters before floating point conversion");
        clock=double.NaN;Check(reader.Read(now).error!=null,"Invalid clock rejected");
        if(live){
            var network=new LinuxNetworkReadings("lo");var session=new ReadingSession(network.Read);
            session.Poll(DateTimeOffset.UtcNow);
            using(var receiver=new UdpClient(new IPEndPoint(IPAddress.Loopback,0)))
            using(var sender=new UdpClient()){
                receiver.Client.ReceiveTimeout=3000;
                var endpoint=(IPEndPoint)receiver.Client.LocalEndPoint;
                sender.Send(new byte[1024],1024,endpoint);
                IPEndPoint peer=null;Check(receiver.Receive(ref peer).Length==1024,"Local UDP probe");
            }
            Thread.Sleep(100);session.Poll(DateTimeOffset.UtcNow);
            Check(session.Latest.state=="LIVE"&&session.Latest.values["netDown"]>0&&session.Latest.values["netUp"]>0,"Live loopback throughput");
            Console.WriteLine("PASS Linux live loopback throughput / Core ReadingSession");
        }
        Console.WriteLine("PASS Linux network fixtures");
    }
}
