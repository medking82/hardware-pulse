using System;
using System.IO;
using System.Reflection;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;
using System.Security.Principal;
[assembly: AssemblyTitle("Hardware Pulse")]
[assembly: AssemblyProduct("Hardware Pulse")]
[assembly: AssemblyCompany("Marck Wong")]
[assembly: AssemblyCopyright("Copyright 2026 Marck Wong")]
[assembly: AssemblyVersion("0.4.0.0")]
internal static class WidgetHost {
    [STAThread]
    private static int Main(string[] args) {
        string folder = AppDomain.CurrentDomain.BaseDirectory;
        string state = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HardwarePulse");
        Directory.CreateDirectory(state);
        string script = "Glass.ps1";
        if (args.Length == 1 && args[0] == "--collector") script = "Collector.ps1";
        else if (args.Length == 1 && args[0] == "--install-startup") script = "Install-Startup.ps1";
        else if (args.Length == 1 && args[0] == "--remove-startup") script = "Remove-Startup.ps1";
        else if (args.Length == 1 && (args[0] == "--enable-startup" || args[0] == "--disable-startup")) script = "Set-Startup.ps1";
        else if (args.Length != 0) return 2;
        // Use the OS PowerShell host for the library's collector environment.
        // The GUI host stays STA; the sensor worker is windowless and independently logged.
        if (script == "Collector.ps1") {
            string sensorState = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"HardwarePulse",WindowsIdentity.GetCurrent().User.Value,"runtime");
            var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"));
            info.Arguments = "-NoProfile -File \"" + Path.Combine(folder, script) + "\"";
            info.UseShellExecute = false; info.CreateNoWindow = true;
            info.RedirectStandardOutput = true; info.RedirectStandardError = true;
            using (var child = Process.Start(info)) {
                var output = child.StandardOutput.ReadToEndAsync();
                var error = child.StandardError.ReadToEndAsync();
                while (!child.WaitForExit(500)) {
                    if (File.Exists(Path.Combine(sensorState,"STOP"))) {
                        // Bound shutdown even if a driver read stops responding.
                        child.Kill(); child.WaitForExit(); break;
                    }
                }
                File.WriteAllText(Path.Combine(state,"collector-output.txt"),output.Result);
                File.WriteAllText(Path.Combine(state,"collector-host-error.txt"),error.Result);
                return child.ExitCode;
            }
        }
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e) {
            string name = new AssemblyName(e.Name).Name;
            if (name.IndexOfAny(new char[] {'/', '\\', ':'}) >= 0) return null;
            string candidate = Path.Combine(folder, "lib", name + ".dll");
            return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
        };
        try {
            using (Runspace runspace = RunspaceFactory.CreateRunspace()) {
                runspace.ApartmentState = ApartmentState.STA;
                runspace.ThreadOptions = PSThreadOptions.UseCurrentThread;
                runspace.Open();
                using (PowerShell ps = PowerShell.Create()) {
                    ps.Runspace = runspace;
                    ps.AddCommand(Path.Combine(folder, script));
                    if(script == "Set-Startup.ps1") ps.AddParameter("Enabled",args[0] == "--enable-startup");
                    ps.Invoke();
                    if (ps.HadErrors) {
                        File.WriteAllText(Path.Combine(state,"host-error.txt"),string.Join(Environment.NewLine,ps.Streams.Error));
                        return 1;
                    }
                }
            }
            return 0;
        } catch (Exception ex) {
            File.WriteAllText(Path.Combine(state,"host-error.txt"),ex.ToString());
            if (script == "Glass.ps1") MessageBox.Show("Hardware Pulse could not start. See %LocalAppData%\\HardwarePulse\\host-error.txt.","Hardware Pulse");
            return 1;
        }
    }
}
