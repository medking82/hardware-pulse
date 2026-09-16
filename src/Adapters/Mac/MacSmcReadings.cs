using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace HardwarePulse {
    public sealed record MacSmcChannel(string Id,string Label,string Unit);

    // A serial, read-only AppleSMC session boundary. No timer, shell, elevation or writes.
    public interface IMacSmcConnection : IDisposable {
        bool Call(byte[] request,byte[] response);
    }

    public sealed class MacSmcReadings {
        sealed record Entry(MacSmcChannel Channel,uint Key,string Type,uint Size);
        readonly Func<IMacSmcConnection> connect;
        readonly string source=Guid.NewGuid().ToString("N");
        readonly byte[] request=new byte[80],response=new byte[80];
        Entry[] entries=Array.Empty<Entry>();
        bool discovered;
        long sequence,retryAt;
        public IReadOnlyList<MacSmcChannel> Channels {get;private set;}=Array.Empty<MacSmcChannel>();

        public MacSmcReadings() {
            if(!OperatingSystem.IsMacOS())throw new PlatformNotSupportedException("AppleSMC requires macOS");
            connect=()=>new NativeConnection();
        }
        public MacSmcReadings(Func<IMacSmcConnection> connect){this.connect=connect??throw new ArgumentNullException(nameof(connect));}

        public Reading Read(DateTimeOffset now) {
            var result=new Reading{time=now,identity=source+":"+(++sequence).ToString(CultureInfo.InvariantCulture),available=new Dictionary<string,bool>()};
            foreach(var entry in entries){result.names[entry.Channel.Id]=entry.Channel.Label;result.available[entry.Channel.Id]=false;}
            if(Stopwatch.GetTimestamp()<retryAt)return result;
            try {
                using var connection=connect();
                if(!discovered)Discover(connection);
                foreach(var entry in entries) {
                    string id=entry.Channel.Id;
                    result.names[id]=entry.Channel.Label;result.available[id]=false;
                    if(!Call(connection,5,entry.Key,entry.Size,0)||!Decode(entry.Type,response.AsSpan(48,(int)entry.Size),out double value))continue;
                    // Reject sensor sentinels, non-finite values and invalid RPM. A stopped fan is real zero.
                    if(entry.Channel.Unit=="°C"?(value<=0||value>150):(value<0||value>100000))continue;
                    result.values[id]=value;result.available[id]=true;
                }
                if(result.values.Count>0)result.state="LIVE";
                // Virtual machines and fanless/unsupported devices must not scan on every poll.
                if(entries.Length==0){discovered=false;retryAt=Stopwatch.GetTimestamp()+30*Stopwatch.Frequency;}
            }catch(Exception e) when(e is IOException||e is DllNotFoundException||e is EntryPointNotFoundException) {
                result.error=e.Message;retryAt=Stopwatch.GetTimestamp()+30*Stopwatch.Frequency;
            }
            return result;
        }

        bool Call(IMacSmcConnection connection,byte command,uint key,uint size,uint index) {
            Array.Clear(request);Array.Clear(response);
            BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(0),key);
            BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(28),size);
            request[42]=command;
            BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(44),index);
            return connection.Call(request,response)&&response[40]==0;
        }
        void Discover(IMacSmcConnection connection) {
            uint countKey=Key("#KEY");
            if(!Call(connection,9,countKey,0,0)||BinaryPrimitives.ReadUInt32LittleEndian(response.AsSpan(28))!=4
                ||BinaryPrimitives.ReadUInt32LittleEndian(response.AsSpan(32))!=Key("ui32")||!Call(connection,5,countKey,4,0))
                throw new IOException("AppleSMC channel discovery unavailable");
            uint count=BinaryPrimitives.ReadUInt32BigEndian(response.AsSpan(48));
            if(count>16384)throw new IOException("AppleSMC channel count exceeds discovery bound");
            var found=new List<Entry>();var seen=new HashSet<uint>();
            for(uint i=0;i<count&&found.Count<256;i++) {
                if(!Call(connection,8,0,0,i))continue;
                uint key=BinaryPrimitives.ReadUInt32LittleEndian(response);
                string name=KeyText(key);
                bool temperature=name[0]=='T',fan=name[0]=='F'&&name[2]=='A'&&name[3]=='c';
                if((!temperature&&!fan)||name.Any(c=>c<' '||c>'~')||!seen.Add(key)||!Call(connection,9,key,0,0))continue;
                uint size=BinaryPrimitives.ReadUInt32LittleEndian(response.AsSpan(28));
                string type=KeyText(BinaryPrimitives.ReadUInt32LittleEndian(response.AsSpan(32)));
                if(!((type=="sp78"||type=="fpe2")&&size==2||type=="flt "&&size==4))continue;
                var channel=new MacSmcChannel("smc/"+name,"SMC · "+name,temperature?"°C":"RPM");
                found.Add(new Entry(channel,key,type,size));
            }
            entries=found.ToArray();Channels=Array.AsReadOnly(entries.Select(x=>x.Channel).ToArray());discovered=true;
        }
        public static bool Decode(string type,ReadOnlySpan<byte> bytes,out double value) {
            value=0;
            if(type=="sp78"&&bytes.Length==2)value=BinaryPrimitives.ReadInt16BigEndian(bytes)/256.0;
            else if(type=="fpe2"&&bytes.Length==2)value=BinaryPrimitives.ReadUInt16BigEndian(bytes)/4.0;
            else if(type=="flt "&&bytes.Length==4)value=BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes));
            else return false;
            return double.IsFinite(value);
        }
        static uint Key(string text)=>((uint)text[0]<<24)|((uint)text[1]<<16)|((uint)text[2]<<8)|text[3];
        static string KeyText(uint key)=>new string(new[]{(char)(key>>24),(char)((key>>16)&255),(char)((key>>8)&255),(char)(key&255)});

        sealed class NativeConnection : IMacSmcConnection {
            const string IOKit="/System/Library/Frameworks/IOKit.framework/IOKit";
            uint connection;
            [DllImport(IOKit)] static extern IntPtr IOServiceMatching([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
            [DllImport(IOKit)] static extern uint IOServiceGetMatchingService(uint port,IntPtr matching);
            [DllImport(IOKit)] static extern int IOServiceOpen(uint service,uint task,uint type,out uint connection);
            [DllImport(IOKit)] static extern int IOObjectRelease(uint value);
            [DllImport(IOKit)] static extern int IOServiceClose(uint connection);
            [DllImport(IOKit)] static extern int IOConnectCallStructMethod(uint connection,uint selector,[In] byte[] input,nuint inputSize,[Out] byte[] output,ref nuint outputSize);
            public NativeConnection() {
                IntPtr match=IOServiceMatching("AppleSMC");
                if(match==IntPtr.Zero)throw new IOException("AppleSMC matching unavailable");
                uint service=IOServiceGetMatchingService(0,match); // IOKit consumes the matching dictionary.
                if(service==0)throw new IOException("AppleSMC unavailable on this device");
                try {
                    if(IOServiceOpen(service,MacMach.TaskPort,0,out connection)!=0||connection==0){Dispose();throw new IOException("AppleSMC read connection unavailable");}
                }finally{IOObjectRelease(service);}
            }
            public bool Call(byte[] input,byte[] output) {
                // Reject all control/write selectors even if a future caller is changed incorrectly.
                if(connection==0||input.Length!=80||output.Length!=80||(input[42]!=5&&input[42]!=8&&input[42]!=9))return false;
                nuint size=80;
                return IOConnectCallStructMethod(connection,2,input,80,output,ref size)==0&&size==80;
            }
            public void Dispose(){if(connection!=0){IOServiceClose(connection);connection=0;}}
        }
    }
}
