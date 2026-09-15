using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading;

namespace HardwarePulse {
    internal static class AntigravityQuota {
        [DllImport("iphlpapi.dll",SetLastError=true)]static extern uint GetExtendedTcpTable(IntPtr table,ref int size,bool order,int family,int tableClass,uint reserved);
        [StructLayout(LayoutKind.Sequential)]struct TcpRow {public uint State,LocalAddress,LocalPort,RemoteAddress,RemotePort,Pid;}
        static int[] Ports(uint pid){
            int size=0;GetExtendedTcpTable(IntPtr.Zero,ref size,false,2,3,0);if(size<=0||size>16777216)return new int[0];
            IntPtr buffer=Marshal.AllocHGlobal(size);try{if(GetExtendedTcpTable(buffer,ref size,false,2,3,0)!=0)return new int[0];int count=Marshal.ReadInt32(buffer);var ports=new List<int>();int rowSize=Marshal.SizeOf(typeof(TcpRow));
                for(int i=0;i<count&&4+(i+1)*rowSize<=size;i++){var row=(TcpRow)Marshal.PtrToStructure(IntPtr.Add(buffer,4+i*rowSize),typeof(TcpRow));if(row.Pid==pid&&row.State==2&&(row.LocalAddress==0||row.LocalAddress==0x0100007f)){int port=(int)(((row.LocalPort&255)<<8)|((row.LocalPort>>8)&255));if(port>0)ports.Add(port);}}
                return ports.Distinct().Take(4).ToArray();
            }finally{Marshal.FreeHGlobal(buffer);}
        }
        internal static bool IsOwnedProcess(uint pid,string sid){
            // Projected WMI query objects can have no callable instance path.
            using(var instance=new ManagementObject("Win32_Process.Handle='"+pid+"'"))
            using(var owner=instance.InvokeMethod("GetOwnerSid",null,new InvokeMethodOptions{Timeout=TimeSpan.FromSeconds(5)}))
                return owner!=null&&Convert.ToUInt32(owner["ReturnValue"])==0&&string.Equals(owner["Sid"] as string,sid,StringComparison.Ordinal);
        }
        internal static object Read(CancellationToken cancel){
            string sid=WindowsIdentity.GetCurrent().User.Value;
            using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancel)){
                deadline.CancelAfter(TimeSpan.FromSeconds(25));
                using(var search=new ManagementObjectSearcher("SELECT ProcessId,CommandLine,ExecutablePath FROM Win32_Process WHERE Name LIKE 'language_server%'") {Options=new EnumerationOptions{Timeout=TimeSpan.FromSeconds(5),ReturnImmediately=false}})
                using(var found=search.Get())foreach(ManagementObject process in found)using(process){
                    deadline.Token.ThrowIfCancellationRequested();string command=process["CommandLine"] as string??"";string path=process["ExecutablePath"] as string??"";
                    if(path.IndexOf("antigravity",StringComparison.OrdinalIgnoreCase)<0)continue;
                    uint pid=Convert.ToUInt32(process["ProcessId"]);
                    if(!IsOwnedProcess(pid,sid))continue;
                    var match=Regex.Match(command,@"(?:^|\s)--csrf_token(?:=|\s+)(?:""([^""]+)""|([^\s]+))");if(!match.Success)continue;string token=match.Groups[1].Success?match.Groups[1].Value:match.Groups[2].Value;
                    foreach(int port in Ports(pid))foreach(string scheme in new[]{"https","http"}){
                        deadline.Token.ThrowIfCancellationRequested();if(!Ports(pid).Contains(port))continue;
                        try{var body=QuotaProviders.Request(scheme+"://127.0.0.1:"+port+"/exa.language_server_pb.LanguageServerService/RetrieveUserQuotaSummary",new Dictionary<string,string>{{"x-codeium-csrf-token",token},{"connect-protocol-version","1"}},"{\"forceRefresh\":true}",deadline.Token,true);
                            if(QuotaData.Decode("Antigravity",body,DateTimeOffset.UtcNow).Status=="Live")return body;
                        }catch(QuotaFailure){}
                    }
                }
            }
            throw new QuotaFailure("Open Antigravity to read quota");
        }
    }
}
