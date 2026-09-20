using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HardwarePulse {
    // Recovery through the installed official CLI. No login, turns or credential arguments.
    internal static class CodexAppServerQuota {
        internal static string InstalledExecutable(){
            string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","OpenAI","Codex","bin","codex.exe");
            return File.Exists(path)?path:null;
        }
        internal static QuotaReading Read(string executable,CancellationToken cancel){
            using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancel))
            using(var process=new Process()){
                deadline.CancelAfter(TimeSpan.FromSeconds(15));
                process.StartInfo=new ProcessStartInfo(executable,"-s read-only -a untrusted app-server --stdio"){
                    UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,
                    WorkingDirectory=Path.GetDirectoryName(executable),RedirectStandardInput=true,
                    RedirectStandardOutput=true,RedirectStandardError=true,
                    StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
                cancel.ThrowIfCancellationRequested();
                if(!process.Start())throw new QuotaFailure("Quota unavailable");
                Action stop=delegate{try{if(!process.HasExited)process.Kill();}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}};
                using(deadline.Token.Register(()=>stop())){
                    int diagnosticOverflow=0;
                    var errors=Task.Run(()=>{try{var buffer=new char[2048];int count,total=0;while((count=process.StandardError.Read(buffer,0,buffer.Length))>0){total+=count;if(total>65536){Interlocked.Exchange(ref diagnosticOverflow,1);stop();break;}}}catch(IOException){}catch(ObjectDisposedException){}});
                    object body;
                    try{
                        body=ReadProtocol(process.StandardOutput,process.StandardInput,deadline.Token);
                    }finally{
                        // A child can close its pipe before the writer flushes. Even if
                        // closing stdin fails, always finish owned-process cleanup.
                        try{process.StandardInput.Close();}
                        finally{
                            if(!process.WaitForExit(500)){stop();if(!process.WaitForExit(2000))throw new QuotaFailure("Quota unavailable");}
                            // Observe completion without retaining or logging server diagnostics.
                            if(!errors.Wait(2000))throw new QuotaFailure("Quota unavailable");
                        }
                    }
                    cancel.ThrowIfCancellationRequested();
                    if(deadline.IsCancellationRequested||diagnosticOverflow!=0)throw new QuotaFailure("Quota unavailable");
                    return QuotaDecoder.Decode("Codex",body,DateTimeOffset.UtcNow);
                }
            }
        }
        internal static object ReadProtocol(TextReader input,TextWriter output,CancellationToken cancel){
            int total=0;
            output.WriteLine("{\"id\":1,\"method\":\"initialize\",\"params\":{\"clientInfo\":{\"name\":\"hardware_pulse\",\"title\":\"Hardware Pulse\",\"version\":\"0.6.27\"}}}");output.Flush();
            Response(input,1,ref total,cancel);
            output.WriteLine("{\"method\":\"initialized\",\"params\":{}}");
            output.WriteLine("{\"id\":2,\"method\":\"account/rateLimits/read\",\"params\":{}}");output.Flush();
            return Response(input,2,ref total,cancel);
        }
        static object Response(TextReader input,int id,ref int total,CancellationToken cancel){
            for(int messages=0;messages<64;messages++){
                var line=new StringBuilder();
                while(true){cancel.ThrowIfCancellationRequested();int value=input.Read();if(value<0)throw new QuotaFailure("Quota unavailable");if(++total>1048576)throw new QuotaFailure("Quota unavailable");if(value=='\n')break;line.Append((char)value);}
                object message=QuotaData.Parse(line.ToString());
                if(QuotaDecoder.Get(message,"method")!=null)continue;
                object responseId=QuotaDecoder.Get(message,"id");
                if(responseId==null||Convert.ToString(responseId,System.Globalization.CultureInfo.InvariantCulture)!=id.ToString())continue;
                if(QuotaDecoder.Get(message,"error")!=null)throw new QuotaFailure("Quota unavailable");
                object result=QuotaDecoder.Get(message,"result");if(result==null)throw new QuotaFailure("Quota unavailable");return result;
            }
            throw new QuotaFailure("Quota unavailable");
        }
    }
}
