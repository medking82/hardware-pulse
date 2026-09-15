using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

public sealed class FrameCapture : IDisposable {
    readonly object gate = new object();
    readonly HardwarePulse.FrameHistory history = new HardwarePulse.FrameHistory();
    readonly Stopwatch clock = Stopwatch.StartNew();
    Process process;
    string executable, sessionName;
    string[] header;
    int target;
    string status = "FPS capture stopped";
    public static string[] ParseCsv(string line) {
        var fields = new List<string>(); var value = new StringBuilder(); bool quoted = false;
        for (int i = 0; i < line.Length; i++) {
            char c = line[i];
            if (c == '"') { if (quoted && i+1 < line.Length && line[i+1] == '"') { value.Append('"'); i++; } else quoted = !quoted; }
            else if (c == ',' && !quoted) { fields.Add(value.ToString()); value.Clear(); }
            else value.Append(c);
        }
        fields.Add(value.ToString()); return fields.ToArray();
    }
    public void Reset(int pid) { lock(gate) { target = pid; history.Clear(); header = null; status = "Waiting for frames"; } }
    static int Column(string[] fields,string name) { return Array.FindIndex(fields,value=>String.Equals(value,name,StringComparison.OrdinalIgnoreCase)); }
    public void Feed(string line) {
        if (String.IsNullOrEmpty(line)) return;
        lock(gate) {
            var fields = ParseCsv(line);
            if (Column(fields, "MsBetweenPresents") >= 0 && Column(fields,"ProcessID") >= 0) { header = fields; return; }
            if (header == null || fields.Length != header.Length) return;
            int pid; double ms;
            int p = Column(header,"ProcessID"), m = Column(header,"MsBetweenPresents"), s = Column(header,"SwapChainAddress");
            if (s < 0 || !Int32.TryParse(fields[p],out pid) || pid != target || !Double.TryParse(fields[m],NumberStyles.Float,CultureInfo.InvariantCulture,out ms)) return;
            Add(fields[s], ms, clock.Elapsed.TotalSeconds);
        }
    }
    public void Add(string stream, double ms, double time) {
        lock(gate) { if(history.Add(stream,ms,time))status="Live"; }
    }
    public FrameMetrics ReadAt(double now) {
        lock(gate) {
            var metrics=history.ReadAt(now);
            if(!metrics.Ready && status!="Live")metrics.Status=status;
            return metrics;
        }
    }
    public FrameMetrics Read() { return ReadAt(clock.Elapsed.TotalSeconds); }
    public bool IsRunning { get { try { return process!=null&&!process.HasExited; } catch { return false; } } }
    public void Start(string exe, int pid) {
        Dispose(); Reset(pid);
        try {
            executable=exe; sessionName="HardwarePulse-" + Guid.NewGuid().ToString("N");
            var info = new ProcessStartInfo(exe, "--process_id " + pid.ToString(CultureInfo.InvariantCulture) + " --output_stdout --v1_metrics --no_console_stats --terminate_on_proc_exit --session_name " + sessionName);
            info.UseShellExecute=false; info.CreateNoWindow=true; info.RedirectStandardOutput=true; info.RedirectStandardError=true;
            process=new Process { StartInfo=info };
            process.OutputDataReceived += (s,e) => Feed(e.Data);
            process.ErrorDataReceived += (s,e) => { if(e.Data != null && e.Data.IndexOf("error:",StringComparison.OrdinalIgnoreCase)>=0) lock(gate) status=e.Data.IndexOf("access denied",StringComparison.OrdinalIgnoreCase)>=0 ? "FPS capture needs administrator" : "FPS capture failed"; };
            process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
        } catch { lock(gate) status="FPS capture failed"; }
    }
    public void Dispose() {
        var old=process; process=null;
        if(old!=null) {
            try {
                if(!old.HasExited) {
                    var stopInfo=new ProcessStartInfo(executable,"--session_name " + sessionName + " --terminate_existing_session") { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true };
                    using(var stop=Process.Start(stopInfo)) { stop.BeginOutputReadLine();stop.BeginErrorReadLine();if(!stop.WaitForExit(2000))stop.Kill(); }
                    if(!old.WaitForExit(1500))old.Kill();
                }
                old.WaitForExit(1500);
            } catch {} old.Dispose();
        }
        lock(gate) { history.Clear(); status="FPS capture stopped"; }
    }
}
