using System;
using System.Text;
using HardwarePulse;

static class CpuIdentityTests {
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
    public static void Run(bool live) {
        int queries=0;
        var reader=new MacCpuIdentityReader(key=>{queries++;return key=="machdep.cpu.brand_string"?Encoding.UTF8.GetBytes(" Apple M4 Pro\0"):key=="hw.physicalcpu_max"?BitConverter.GetBytes(14):BitConverter.GetBytes(14L);});
        var info=reader.Read();
        Check(info.Model=="Apple M4 Pro"&&info.PhysicalCores==14&&info.LogicalCores==14,"CPU model and native 32/64-bit core counts");
        Check(ReferenceEquals(info,reader.Read())&&queries==3,"Identity queries cached outside sampling loop");
        var absent=new MacCpuIdentityReader(_=>null).Read();
        Check(absent.Model==null&&absent.PhysicalCores==null&&absent.LogicalCores==null,"No guessed model/count from missing metadata");
        var malformed=new MacCpuIdentityReader(key=>key=="machdep.cpu.brand_string"?new byte[]{255,255}:BitConverter.GetBytes(-1)).Read();
        Check(malformed.Model==null&&malformed.PhysicalCores==null&&malformed.LogicalCores==null,"Reject invalid text/counts");
        if(live) {
            var native=new MacCpuIdentityReader().Read();
            Check(native.PhysicalCores>0&&native.LogicalCores>=native.PhysicalCores,"Live CPU topology");
            Check(!string.IsNullOrWhiteSpace(native.Model),"Live CPU model");
            Console.WriteLine($"CPU_IDENTITY model={native.Model} physical={native.PhysicalCores} logical={native.LogicalCores}");
        }
        Console.WriteLine("PASS macOS CPU identity: model, core counts, cache and unavailable metadata");
    }
}
