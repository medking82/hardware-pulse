using System;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// In-process pipes only: no CLI, network, account, process tree or live collector.
static class QuotaPipeTests {
    static Type pipeType;
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static Stream Wrap(Stream stream,CancellationToken cancel){return (Stream)Activator.CreateInstance(pipeType,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{stream,cancel},null);}
    static int Main(string[] args){
        try{
            var assembly=Assembly.LoadFrom(args[0]);pipeType=assembly.GetType("HardwarePulse.QuotaPipeStream",true);
            foreach(string name in new[]{"CodexAppServerQuota","AntigravityCliQuota"}){
                var method=assembly.GetType("HardwarePulse."+name).GetMethod(name=="CodexAppServerQuota"?"ReadProtocol":"ReadReport",BindingFlags.Static|BindingFlags.NonPublic);
                using(var pipe=new AnonymousPipeServerStream(PipeDirection.In,HandleInheritability.None))
                using(var writer=new AnonymousPipeClientStream(PipeDirection.Out,pipe.ClientSafePipeHandle))
                using(var cancel=new CancellationTokenSource())
                using(var reader=new StreamReader(Wrap(pipe,cancel.Token),Encoding.UTF8))
                using(var entered=new ManualResetEventSlim()){
                    var task=Task.Run(()=>{entered.Set();try{method.Invoke(null,new object[]{reader,new StringWriter(),cancel.Token});return false;}catch(TargetInvocationException e){return e.InnerException is OperationCanceledException;}});
                    bool bounded=false;
                    try{Check(entered.Wait(2000),"Parser started");Thread.Sleep(100);cancel.Cancel();bounded=task.Wait(1000);}
                    finally{writer.Dispose();Check(task.Wait(2000),"Parser worker cleaned up");}
                    Check(bounded&&task.Result,name+" cancellation while writer remains open");
                }
            }
            using(var pipe=new AnonymousPipeServerStream(PipeDirection.In,HandleInheritability.None))
            using(var writer=new AnonymousPipeClientStream(PipeDirection.Out,pipe.ClientSafePipeHandle))
            using(var cancel=new CancellationTokenSource())
            using(var reader=new StreamReader(Wrap(pipe,cancel.Token),Encoding.UTF8)){
                byte[] text=Encoding.UTF8.GetBytes("quota 中文 🙂\n");
                var task=Task.Run(()=>reader.ReadLine());
                foreach(byte value in text){writer.WriteByte(value);writer.Flush();Thread.Sleep(2);}
                Check(task.Wait(2000)&&task.Result=="quota 中文 🙂","Fragmented UTF-8 preserved");
                writer.Dispose();Check(reader.Read()==-1,"Closed writer yields EOF");
            }
            using(var pipe=new AnonymousPipeServerStream(PipeDirection.In,HandleInheritability.None))
            using(var writer=new AnonymousPipeClientStream(PipeDirection.Out,pipe.ClientSafePipeHandle))
            using(var cancel=new CancellationTokenSource())
            using(var reader=new StreamReader(Wrap(pipe,cancel.Token),Encoding.UTF8)){
                // Match the stderr worker: Read(buffer) must also cancel without EOF.
                var task=Task.Run(()=>{try{reader.Read(new char[2048],0,2048);return false;}catch(OperationCanceledException){return true;}});
                bool bounded=false;
                try{Thread.Sleep(100);cancel.Cancel();bounded=task.Wait(1000);}
                finally{writer.Dispose();Check(task.Wait(2000),"Diagnostic worker cleaned up");}
                Check(bounded&&task.Result,"Idle stderr cancellation");
            }
            Console.WriteLine("PASS quota pipes: both parsers, idle stderr, fragmented UTF-8, EOF, worker cleanup");return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
