using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using HardwarePulse;

static class CodexRpcTests {
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static int Main(string[] args){
        try{
            var read=typeof(QuotaProviders).Assembly.GetType("HardwarePulse.CodexAppServerQuota").GetMethod("Read",BindingFlags.NonPublic|BindingFlags.Static);
            string executable=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"NativeCodexRpcFixture.exe");
            var result=(QuotaReading)read.Invoke(null,new object[]{executable,CancellationToken.None});
            Check(result.Status=="Live"&&result.Windows.Single().Remaining==64,"Child-process quota result");
            string hanging=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"NativeCodexRpcFixture-hang.exe");
            using(var cancellation=new CancellationTokenSource(200)){
                var watch=Stopwatch.StartNew();bool failed=false;
                try{read.Invoke(null,new object[]{hanging,cancellation.Token});}
                catch(TargetInvocationException error){failed=error.InnerException is OperationCanceledException||error.InnerException is QuotaFailure;}
                Check(failed&&watch.Elapsed<TimeSpan.FromSeconds(5),"Cancellation must bound a stalled child");
            }
            Check(Process.GetProcessesByName(Path.GetFileNameWithoutExtension(hanging)).Length==0,"Owned child survived cancellation");
            string noisy=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"NativeCodexRpcFixture-stderr.exe");
            bool bounded=false;var limitWatch=Stopwatch.StartNew();
            try{read.Invoke(null,new object[]{noisy,CancellationToken.None});}
            catch(TargetInvocationException error){bounded=error.InnerException is QuotaFailure;}
            Check(bounded&&limitWatch.Elapsed<TimeSpan.FromSeconds(5),"Diagnostic overflow must fail promptly");
            Check(Process.GetProcessesByName(Path.GetFileNameWithoutExtension(noisy)).Length==0,"Noisy child survived output limit");
            Check(Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable)).Length==0,"Owned completed child survived");
            Console.WriteLine("PASS Codex RPC child: quota decode, bounded cancellation and owned process cleanup");return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
