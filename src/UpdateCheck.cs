using System;
using System.Net;
using System.Threading.Tasks;
public sealed class UpdateCheck {
    public Task<string> Pending { get; private set; }
    public void Start() {
        if(Pending!=null && !Pending.IsCompleted)return;
        Pending=Task.Run(()=> {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var request=(HttpWebRequest)WebRequest.Create("https://api.github.com/repos/medking82/hardware-pulse/releases/latest");
            request.UserAgent="HardwarePulse-UpdateCheck";request.Timeout=10000;request.ReadWriteTimeout=10000;
            using(var response=request.GetResponse())using(var reader=new System.IO.StreamReader(response.GetResponseStream()))return reader.ReadToEnd();
        });
    }
}
