using System;
namespace HardwarePulse {
    public static class NetworkRate {
        public static string Link(double bitsPerSecond){if(double.IsNaN(bitsPerSecond)||double.IsInfinity(bitsPerSecond)||bitsPerSecond<0)return "—";if(bitsPerSecond==0)return "Disconnected";return (bitsPerSecond/(bitsPerSecond>=1000000000?1000000000:1000000)).ToString("0.##")+(bitsPerSecond>=1000000000?" Gbit/s":" Mbit/s");}
        public static string Format(double bytesPerSecond,string unit){
            if(double.IsNaN(bytesPerSecond)||double.IsInfinity(bytesPerSecond)||bytesPerSecond<0)return "—";
            if(unit!="KB/s"&&unit!="MB/s"&&unit!="Mbit/s")unit=bytesPerSecond>=1000000?"MB/s":"KB/s";
            double value=unit=="Mbit/s"?bytesPerSecond*8/1000000:unit=="MB/s"?bytesPerSecond/1000000:bytesPerSecond/1000;
            return value.ToString("0.##")+" "+unit;
        }
    }
}
