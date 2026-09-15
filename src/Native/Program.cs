using System;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Windows;

[assembly:AssemblyTitle("Hardware Pulse")]
[assembly:AssemblyProduct("Hardware Pulse")]
[assembly:AssemblyCompany("Marck Wong")]
[assembly:AssemblyCopyright("Copyright 2026 Marck Wong")]
[assembly:AssemblyVersion("0.6.24.0")]
namespace HardwarePulse {
    internal static class Program {
        [STAThread] static int Main(string[] args){
            var paths=PulsePaths.Installed();Directory.CreateDirectory(paths.State);
            AppDomain.CurrentDomain.AssemblyResolve+=delegate(object sender,ResolveEventArgs e){string name=new AssemblyName(e.Name).Name;if(name.IndexOfAny(new[]{'/','\\',':'})>=0)return null;string path=Path.Combine(paths.Root,"lib",name+".dll");return File.Exists(path)?Assembly.LoadFrom(path):null;};
            try{
                if(args.Length==1&&args[0]=="--collector")return Collector.Run(paths);
                if(args.Length==1&&(args[0]=="--install-startup"||args[0]=="--remove-startup"||args[0]=="--enable-startup"||args[0]=="--disable-startup")){
                    using(var store=new SchedulerStore()){
                        var startup=new Startup(store,paths.Exe,WindowsIdentity.GetCurrent().User.Value);
                        if(args[0]=="--install-startup"){
                            startup.Install();Directory.CreateDirectory(paths.Runtime);if(File.Exists(paths.Stop))File.Delete(paths.Stop);startup.StartCollector();
                            // Registration is complete even when first hardware enumeration is slow.
                            // Sensor readiness is reported separately by the UI/collector diagnostics.
                            Json.WriteAtomic(Path.Combine(paths.State,"startup.json"),new {installed=DateTimeOffset.Now.ToString("o"),task=Startup.CollectorTask});
                        }else if(args[0]=="--remove-startup"){
                            if(Directory.Exists(paths.Runtime))File.WriteAllText(paths.Stop,"Uninstall requested");startup.Remove();
                        }else startup.SetEnabled(args[0]=="--enable-startup");
                    }return 0;
                }
                if(args.Length!=0)return 2;
                using(var activation=new AppActivation("Local\\HardwarePulse.ShowHome."+WindowsIdentity.GetCurrent().User.Value))
                using(var mutex=new Mutex(false,"Local\\HardwarePulseGlass")){
                    bool owned;try{owned=mutex.WaitOne(0);}catch(AbandonedMutexException){owned=true;}if(!owned){activation.Notify(paths.Exe);return 0;}
                    try{var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};using(var shell=new Shell(paths)){app.MainWindow=shell.Window;shell.Window.Closed+=delegate{app.Shutdown();};activation.Listen(app.Dispatcher,shell.ShowHome);shell.Start();app.Run();}return 0;}finally{mutex.ReleaseMutex();}
                }
            }catch(Exception e){File.WriteAllText(Path.Combine(paths.State,"host-error.txt"),e.ToString());if(args.Length==0)MessageBox.Show("Hardware Pulse could not start. See %LocalAppData%\\HardwarePulse\\host-error.txt.","Hardware Pulse");return 1;}
        }
    }
}
