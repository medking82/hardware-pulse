using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Linq;
using HardwarePulse;

static class SmcTests {
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
    public static void Run(bool live) {
        Check(MacSmcReadings.Decode("sp78",new byte[]{54,128},out var value)&&value==54.5,"Intel fixed point temperature");
        Check(MacSmcReadings.Decode("sp78",new byte[]{255,128},out value)&&value==-.5,"Signed fixed point decoding");
        Check(MacSmcReadings.Decode("fpe2",new byte[]{31,65},out value)&&value==2000.25,"Fractional fan RPM");
        Check(MacSmcReadings.Decode("flt ",new byte[]{0,0,90,66},out value)&&value==54.5,"Apple Silicon little endian float");
        Check(!MacSmcReadings.Decode("flt ",new byte[]{0,0,128,127},out value),"Reject infinity");
        Check(!MacSmcReadings.Decode("flt ",new byte[]{0,0,192,127},out value),"Reject NaN");
        Check(!MacSmcReadings.Decode("sp78",new byte[]{54},out value)&&!MacSmcReadings.Decode("bad!",new byte[]{1,2},out value),"Reject malformed/unknown formats");
        var fixture=new Fixture();var reader=new MacSmcReadings(()=>fixture);
        var session=new ReadingSession(reader.Read);session.Poll(DateTimeOffset.UtcNow);
        Check(reader.Channels.Count==2&&session.Latest.values["smc/TC0P"]==54.5&&session.Latest.values["smc/F0Ac"]==0,"Discovered temperature and real stopped fan");
        Check(!session.Latest.values.ContainsKey("smc/F0Tg"),"Fan target is not actual RPM");
        int discovery=fixture.DiscoveryCalls;fixture.FailTemperature=true;
        session.Poll(DateTimeOffset.UtcNow);
        Check(!session.Latest.values.ContainsKey("smc/TC0P")&&session.Peaks["smc/TC0P"]==54.5,"Failed read clears live data but retains session peak");
        Check(fixture.DiscoveryCalls==discovery&&fixture.Disposals==2,"Cached discovery and per-poll connection disposal");
        var oversized=new Fixture{Count=20000};var invalid=new MacSmcReadings(()=>oversized);
        Check(invalid.Read(DateTimeOffset.UtcNow).error!=null&&oversized.DiscoveryCalls==0&&oversized.Disposals==1,"Oversized enumeration fails closed and disposes connection");
        Check(invalid.Read(DateTimeOffset.UtcNow).values.Count==0&&oversized.Disposals==1,"Unavailable discovery is backed off");
        if(live) {
            var actual=new MacSmcReadings();var watch=Stopwatch.StartNew();var result=actual.Read(DateTimeOffset.UtcNow);
            double discoveryMs=watch.Elapsed.TotalMilliseconds;
            Check(actual.Channels.Count<=256&&result.values.All(x=>double.IsFinite(x.Value)),"Native bounded SMC values");
            Console.WriteLine($"SMC_NATIVE channels={actual.Channels.Count} readings={result.values.Count} unavailable={result.error!=null}");
            // CI VMs may omit AppleSMC. Explicitly report availability; absence is not sensor coverage.
            if(result.values.Count==0)Console.WriteLine("SMC native sensor availability unverified on this runner");
            else {
                long allocated=GC.GetAllocatedBytesForCurrentThread();watch.Restart();
                for(int i=0;i<10;i++)actual.Read(DateTimeOffset.UtcNow);
                double pollMs=watch.Elapsed.TotalMilliseconds/10;
                allocated=(GC.GetAllocatedBytesForCurrentThread()-allocated)/10;
                Console.WriteLine($"SMC_COST discoveryMs={discoveryMs:F2} meanPollMs={pollMs:F2} bytesPerPoll={allocated}");
            }
        } else if(!OperatingSystem.IsMacOS()) {
            bool rejected=false;try{new MacSmcReadings();}catch(PlatformNotSupportedException){rejected=true;}
            Check(rejected,"Native SMC rejects non-macOS hosts");
        }
        Console.WriteLine("PASS macOS SMC protocol: typed readings, firmware errors, cached discovery, bounds and disposal");
    }
    sealed class Fixture:IMacSmcConnection {
        public int Disposals,DiscoveryCalls;
        public uint Count=4;
        public bool FailTemperature;
        static uint Key(string text)=>((uint)text[0]<<24)|((uint)text[1]<<16)|((uint)text[2]<<8)|text[3];
        public bool Call(byte[] input,byte[] output) {
            Check(input.Length==80&&output.Length==80,"SMC ABI buffer size");
            uint key=BinaryPrimitives.ReadUInt32LittleEndian(input);
            byte command=input[42];Check(command==5||command==8||command==9,"No SMC write commands");
            if(command==8) {
                DiscoveryCalls++;uint index=BinaryPrimitives.ReadUInt32LittleEndian(input.AsSpan(44));
                BinaryPrimitives.WriteUInt32LittleEndian(output,Key(new[]{"TC0P","F0Ac","F0Tg","TC0P"}[index]));return true;
            }
            if(command==9) {
                BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(28),key==Key("#KEY")?4u:2u);
                BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(32),Key(key==Key("#KEY")?"ui32":key==Key("TC0P")?"sp78":"fpe2"));return true;
            }
            Check(BinaryPrimitives.ReadUInt32LittleEndian(input.AsSpan(28))==(key==Key("#KEY")?4u:2u),"Read length from metadata");
            if(key==Key("#KEY"))BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(48),Count);
            else if(key==Key("TC0P")){if(FailTemperature)output[40]=132;else{output[48]=54;output[49]=128;}}
            return true;
        }
        public void Dispose(){Disposals++;}
    }
}
