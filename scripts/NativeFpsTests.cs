using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using HardwarePulse;

internal static class NativeFpsTests {
    static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
    public static void Run(){
        var metrics=new FrameMetrics{Current=60,Average=58,Minimum=30,Low=double.NaN,Count=90,Ready=true,Status="Live"};
        byte[] response=FpsProtocol.Response(metrics);var decoded=FpsProtocol.Metrics(response);
        Assert(response.Length==FpsProtocol.ResponseSize&&decoded.Ready&&decoded.Current==60&&double.IsNaN(decoded.Low),"FPS fixed response and low sample count");
        using(var process=Process.GetCurrentProcess()){
            long started=process.StartTime.ToUniversalTime().Ticks;
            Assert(FpsProtocol.Target(process.Id,started)&&!FpsProtocol.Target(process.Id,started+1)&&!FpsProtocol.Target(0,0),"FPS process birth prevents PID reuse and invalid targets");
            Assert(FpsProtocol.Peer((uint)process.Id,process.MainModule.FileName)&&!FpsProtocol.Peer((uint)process.Id,"C:\\wrong.exe"),"FPS peer executable binding");
            byte[] request=FpsProtocol.Request(process.Id,started,37);Assert(request.Length==20&&BitConverter.ToInt64(request,12)==37,"FPS bounded request");
        }
        Assert(!FpsProtocol.ProtectedTool(typeof(NativeFpsTests).Assembly.Location),"FPS cannot elevate a workspace tool");
        Assert(OverlayTarget.Excluded("explorer")&&OverlayTarget.Excluded("HARDWAREPULSE")&&!OverlayTarget.Excluded("Game"),"FPS desktop and self exclusions");
        using(var capture=new FrameCapture()){capture.Reset(42);capture.Add("main",16,10);Assert(!capture.ReadAt(11.5).Ready&&capture.ReadAt(11.5).Status=="Waiting for frames","FPS stale frame status");}
        // Repeated cancellation exercises the native completion/event lifetime race.
        for(int attempt=0;attempt<12;attempt++){
        string name="Pulse-Fps-Test-"+Guid.NewGuid().ToString("N");Exception failure=null;
        using(var server=new NamedPipeServerStream(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous)){
            var worker=new Thread(delegate(){try{server.WaitForConnection();var request=FpsProtocol.Read(server,20,1000);Assert(BitConverter.ToInt32(request,0)==42,"FPS fragmented request");FpsProtocol.Write(server,response);try{FpsProtocol.Read(server,20,150);throw new Exception("FPS idle connection did not expire");}catch(IOException){}}catch(Exception e){failure=e;server.Dispose();}});worker.Start();
            using(var client=new NamedPipeClientStream(".",name,PipeDirection.InOut,PipeOptions.Asynchronous)){client.Connect(1000);byte[] request=FpsProtocol.Request(42,123,0);var first=new byte[3];var rest=new byte[17];Array.Copy(request,first,3);Array.Copy(request,3,rest,0,17);FpsProtocol.Write(client,first);FpsProtocol.Write(client,rest);Assert(FpsProtocol.Metrics(FpsProtocol.Read(client,44,1000)).Ready,"FPS metrics transport");Assert(worker.Join(3000),"FPS read timeout bounded");}
            if(failure!=null)throw failure;
        }
        }
        Console.WriteLine("FPS protocol, peer identity, PID reuse, fragmentation, repeated timeout cancellation and stale metrics passed.");
    }
}
