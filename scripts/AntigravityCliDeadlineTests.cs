using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using HardwarePulse;

internal static class AntigravityCliDeadlineTests {
    const string Delay="AG_FIXTURE_DELAY_MS",Mode="AG_FIXTURE_MODE",PidFile="AG_FIXTURE_PID_FILE";
    static void Check(bool ok,string message){if(!ok)throw new Exception("Antigravity CLI deadline: "+message);}
    static string ValidReport(){return "Gemini Models\tWeekly Limit Remaining\t0%\t2026-10-01T00:00:00Z\nGemini Models\tFive Hour Limit Remaining\t100%\t2026-09-30T12:00:00+08:00\n";}
    static object Invoke(MethodInfo method,params object[] args){try{return method.Invoke(null,args);}catch(TargetInvocationException error){throw error.InnerException;}}
    static void Fixture(){
        string pidPath=Environment.GetEnvironmentVariable(PidFile);if(!string.IsNullOrEmpty(pidPath))File.WriteAllText(pidPath,Process.GetCurrentProcess().Id.ToString());
        int delay;int.TryParse(Environment.GetEnvironmentVariable(Delay),out delay);if(delay>0)Thread.Sleep(delay);
        string mode=Environment.GetEnvironmentVariable(Mode)??"valid";if(mode=="hang")Thread.Sleep(60000);if(mode=="nonzero")Environment.Exit(7);if(mode=="invalid"){Console.WriteLine("malformed");Console.Out.Flush();return;}
        Console.Write(ValidReport());Console.Out.Flush();
    }
    static void WaitGone(string path){if(!File.Exists(path))return;int pid=int.Parse(File.ReadAllText(path));var until=DateTime.UtcNow.AddSeconds(4);while(DateTime.UtcNow<until){if(!ProcessExists(pid))return;Thread.Sleep(25);}Check(!ProcessExists(pid),"owned fixture remained after cleanup");}
    static bool ProcessExists(int pid){try{using(var process=Process.GetProcessById(pid))return !process.HasExited;}catch{return false;}}
    static object RunCore(MethodInfo method,MethodInfo report,string executable,CancellationToken cancel,bool require,TimeSpan deadline){var parser=(Func<TextReader,TextWriter,CancellationToken,object>)Delegate.CreateDelegate(typeof(Func<TextReader,TextWriter,CancellationToken,object>),report);return method.Name=="ReadWithDeadline"?Invoke(method,executable,"--synthetic",parser,cancel,require,deadline):Invoke(method,executable,"--synthetic",parser,cancel,require);}
    internal static void Run(string stateDirectory){
        string executable=Process.GetCurrentProcess().MainModule.FileName;var assembly=typeof(QuotaProviders).Assembly;var cli=assembly.GetType("HardwarePulse.AntigravityCliQuota").GetMethod("Read",BindingFlags.NonPublic|BindingFlags.Static);var childType=assembly.GetType("HardwarePulse.QuotaChildProcess");var defaultChild=childType.GetMethod("Read",BindingFlags.NonPublic|BindingFlags.Static);var extended=childType.GetMethod("ReadWithDeadline",BindingFlags.NonPublic|BindingFlags.Static);var report=assembly.GetType("HardwarePulse.AntigravityCliQuota").GetMethod("ReadReport",BindingFlags.NonPublic|BindingFlags.Static);Check(cli!=null&&defaultChild!=null&&extended!=null&&report!=null,"deadline entrypoints missing");
        string pidPath=Path.Combine(stateDirectory,"antigravity-cli-fixture.pid");Directory.CreateDirectory(stateDirectory);string oldDelay=Environment.GetEnvironmentVariable(Delay),oldMode=Environment.GetEnvironmentVariable(Mode),oldPid=Environment.GetEnvironmentVariable(PidFile);
        try{
            Environment.SetEnvironmentVariable(PidFile,pidPath);Environment.SetEnvironmentVariable(Delay,"16000");Environment.SetEnvironmentVariable(Mode,"valid");var reading=(QuotaReading)Invoke(cli,executable,CancellationToken.None);Check(reading.Status=="Live"&&reading.Source=="CLI","production Antigravity 25-second budget did not admit delayed valid report");WaitGone(pidPath);
            Environment.SetEnvironmentVariable(Delay,"16000");bool oldTimed=false;try{RunCore(defaultChild,report,executable,CancellationToken.None,true,TimeSpan.FromSeconds(15));}catch(QuotaFailure error){oldTimed=error.FailureKind=="CLI timeout";}Check(oldTimed,"15-second default boundary did not time out delayed report");WaitGone(pidPath);
            Environment.SetEnvironmentVariable(Delay,"0");Environment.SetEnvironmentVariable(Mode,"invalid");bool invalid=false;try{RunCore(extended,report,executable,CancellationToken.None,true,TimeSpan.FromSeconds(3));}catch(QuotaFailure error){invalid=error.FailureKind=="Invalid response";}Check(invalid,"invalid report classification missing");WaitGone(pidPath);
            Environment.SetEnvironmentVariable(Mode,"nonzero");bool exit=false;try{RunCore(extended,report,executable,CancellationToken.None,true,TimeSpan.FromSeconds(3));}catch(QuotaFailure error){exit=error.FailureKind=="CLI exit failure";}Check(exit,"nonzero exit classification missing");WaitGone(pidPath);
            Environment.SetEnvironmentVariable(Mode,"hang");using(var cancel=new CancellationTokenSource(200)){bool canceled=false;try{RunCore(extended,report,executable,cancel.Token,true,TimeSpan.FromSeconds(3));}catch(OperationCanceledException){canceled=true;}catch(QuotaFailure){canceled=false;}Check(canceled,"external cancellation was converted or not bounded");}WaitGone(pidPath);
            bool timeout=false;try{RunCore(extended,report,executable,CancellationToken.None,true,TimeSpan.FromMilliseconds(500));}catch(QuotaFailure error){timeout=error.FailureKind=="CLI timeout";}Check(timeout,"internal timeout classification missing");WaitGone(pidPath);
            File.WriteAllText(Path.Combine(stateDirectory,"passed"),"ok");Console.WriteLine("PASS Antigravity CLI deadline: production 25s vs legacy 15s, timeout, cancellation, invalid report and exit failure; self-contained child only");
        }finally{Environment.SetEnvironmentVariable(Delay,oldDelay);Environment.SetEnvironmentVariable(Mode,oldMode);Environment.SetEnvironmentVariable(PidFile,oldPid);if(File.Exists(pidPath))File.Delete(pidPath);}
    }
    public static int Main(string[] args){try{if(args.Length>0&&(args[0]=="--fixture"||args[0]=="--print"||args[0]=="--synthetic")){Fixture();return 0;}if(args.Length!=1)throw new ArgumentException("State directory is required");Run(args[0]);return 0;}catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
