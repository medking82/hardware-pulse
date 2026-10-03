using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using HardwarePulse;

static class AppResourceTests {
    [DllImport("user32.dll")]static extern bool IsWindowEnabled(IntPtr window);
    sealed class FixtureForm:Form {
        protected override CreateParams CreateParams {get{var value=base.CreateParams;value.ExStyle|=0x80;return value;}} // Tool window: no taskbar entry, no hidden owner.
    }
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static bool WaitUntil(Func<bool> condition,int timeout){var wait=Stopwatch.StartNew();while(wait.ElapsedMilliseconds<timeout){if(condition())return true;Thread.Sleep(25);}return condition();}
    static AppResourceProcess Candidate(int pid,string name,long ram,long? gpu){return new AppResourceProcess {Pid=pid,StartedUtcTicks=100+pid,Name=name,RamBytes=ram,GpuBytes=gpu,CanClose=true};}
    static string CandidateState(AppResourceProcess row){return row==null?"null":string.Join("|",new object[]{row.Pid,row.StartedUtcTicks,row.Name,row.RamBytes,row.GpuBytes,row.CanClose,row.IsForeground,row.ExecutablePath});}
    static void TestReviewCandidates(){
        const long ram=536870912L,gpu=134217728L;
        var boundary=Candidate(1,"Zulu",ram,gpu);var below=Candidate(5,"Below",ram-1,gpu-1);
        Check(WindowsAppResources.ReviewCandidates(null,false).Length==0,"Null candidate snapshot");
        Check(WindowsAppResources.ReviewCandidates(new AppResourceProcess[0],true).Length==0,"Empty candidate snapshot");
        foreach(bool graphics in new[]{false,true}){
            Check(WindowsAppResources.ReviewCandidates(new[]{below,boundary},graphics).Single()==boundary,"Inclusive candidate threshold");
            foreach(string name in new[]{"ExPlOrEr","dwm","csrss","winlogon","sihost","ShellExperienceHost","StartMenuExperienceHost","TextInputHost","ctfmon","RuntimeBroker"})
                Check(WindowsAppResources.ReviewCandidates(new[]{Candidate(8,name,ram*8,gpu*8)},graphics).Length==0,"Windows shell/input/session component excluded: "+name);
        }
        var foreground=Candidate(6,"Foreground",ram*8,gpu*8);foreground.IsForeground=true;
        var background=Candidate(7,"No close",ram*8,gpu*8);background.CanClose=false;
        var unknown=Candidate(9,"Unknown GPU",ram+1,null);
        var missingIdentity=Candidate(10,"Missing identity",ram*8,gpu*8);missingIdentity.StartedUtcTicks=0;
        var rows=new[]{boundary,Candidate(3,"Alpha",ram*2,gpu*4),below,foreground,background,
            Candidate(4,"beta",ram*3,gpu*2),unknown,Candidate(2,"Alpha",ram*2,gpu*2),missingIdentity,null};
        var original=rows.ToArray();var states=rows.Select(CandidateState).ToArray();
        var ramCandidates=WindowsAppResources.ReviewCandidates(rows,false);
        var gpuCandidates=WindowsAppResources.ReviewCandidates(rows,true);
        Check(ramCandidates.Select(p=>p.Pid).SequenceEqual(new[]{4,2,3}),"RAM numeric ranking, name/PID ties and three-row limit");
        Check(gpuCandidates.Select(p=>p.Pid).SequenceEqual(new[]{3,2,4}),"GPU numeric ranking and stable name ties");
        Check(WindowsAppResources.ReviewCandidates(rows.Reverse().ToArray(),false).Select(p=>p.Pid).SequenceEqual(new[]{4,2,3}),"Candidate ties independent of input order");
        Check(WindowsAppResources.ReviewCandidates(new[]{foreground,background,missingIdentity},false).Length==0,"Foreground, no-close and invalid identity excluded");
        Check(WindowsAppResources.ReviewCandidates(new[]{unknown},false).Single()==unknown,"Unknown GPU still permits RAM review");
        Check(WindowsAppResources.ReviewCandidates(new[]{unknown},true).Length==0,"Unknown GPU excluded from GPU review");
        Check(rows.SequenceEqual(original)&&rows.Select(CandidateState).SequenceEqual(states),"Candidate policy must not reorder input or mutate process facts");
    }
    [STAThread]static int Main(string[] args){
        if(args.Length==1&&args[0].StartsWith("--fixture-")){
            string mode=args[0].Substring("--fixture-".Length);
            if(!new[]{"accept","refuse","disabled","modal"}.Contains(mode))return 2;
            using(var form=new FixtureForm {Text="Pulse resource fixture",Opacity=0,Width=100,Height=100})
            using(var timer=new System.Windows.Forms.Timer {Interval=10000}){
                // All fixtures expire independently, including a refused close or nested modal loop.
                timer.Tick+=delegate{Environment.Exit(0);};timer.Start();
                form.FormClosing+=delegate(object sender,FormClosingEventArgs e){if(mode=="refuse"){e.Cancel=true;form.Text="Pulse close refused";}};
                form.Shown+=delegate{
                    if(mode=="disabled")form.Enabled=false;
                    if(mode=="modal"){
                        using(var dialog=new FixtureForm {Text="Pulse hidden modal fixture",Opacity=0,Width=80,Height=80}){
                            dialog.Shown+=delegate{form.Text="Pulse fixture ready "+mode;};
                            dialog.ShowDialog(form);
                        }
                    }else form.Text="Pulse fixture ready "+mode;
                };
                Application.Run(form);return 0;
            }
        }
        TestReviewCandidates();
        var imagePath=typeof(WindowsAppResources).GetMethod("ReadExecutablePath",BindingFlags.Static|BindingFlags.NonPublic);
        Check((string)imagePath.Invoke(null,new object[]{0})==string.Empty,"Invalid PID path must stay unavailable");
        Check((string)imagePath.Invoke(null,new object[]{-1})==string.Empty,"Negative PID path must stay unavailable");
        int pid;
        Check(WindowsAppResources.TryGpuInstance("pid_42_luid_0x00000000_0x00001234_phys_0",100,out pid)&&pid==42,"GPU instance PID parse");
        Check(!WindowsAppResources.TryGpuInstance("pid_42_luid_invalid",100,out pid),"Unknown GPU instance must stay unavailable");
        Check(!WindowsAppResources.TryGpuInstance("pid_42_luid_0x00000000_0x00001234_phys_0",-1,out pid),"Negative GPU usage must not become zero");
        Check(!WindowsAppResources.TryGpuInstance("pid_2147483648_luid_0x00000000_0x00001234_phys_0",100,out pid),"Overflow PID rejected");
        Check(WindowsAppResources.RequestClose(null)==AppCloseResult.NotAllowed,"Null selection");
        using(var own=Process.GetCurrentProcess())Check(WindowsAppResources.RequestClose(new AppResourceProcess {Pid=own.Id,StartedUtcTicks=own.StartTime.ToUniversalTime().Ticks,CanClose=true})==AppCloseResult.NotAllowed,"Pulse host must not close itself");
        foreach(string mode in new[]{"accept","refuse","disabled","modal"}){
            var info=new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,"--fixture-"+mode){UseShellExecute=false,CreateNoWindow=true};
            using(var child=Process.Start(info))try{
                IntPtr hwnd=IntPtr.Zero;
                Check(WaitUntil(delegate{child.Refresh();hwnd=child.MainWindowHandle;return hwnd!=IntPtr.Zero&&child.MainWindowTitle=="Pulse fixture ready "+mode;},4000),mode+" fixture main window ready");
                bool blocked=mode=="disabled"||mode=="modal";
                Check(IsWindowEnabled(hwnd)!=blocked,mode+" fixture enabled state");
                var row=new AppResourceProcess {Pid=child.Id,StartedUtcTicks=child.StartTime.ToUniversalTime().Ticks,CanClose=true};
                row.StartedUtcTicks--;Check(WindowsAppResources.RequestClose(row)==AppCloseResult.IdentityChanged,"PID reuse / stale birth must not dispatch close");
                Check(!child.HasExited,"Identity rejection preserves process");row.StartedUtcTicks++;
                var snapshot=WindowsAppResources.Read();var observed=snapshot.Processes.SingleOrDefault(p=>p.Pid==child.Id);
                Check(observed!=null&&observed.CanClose!=blocked&&observed.StartedUtcTicks==row.StartedUtcTicks,"Current-user fixture listed with identity and enabled state");
                Check(observed.RamBytes>0,"Resident RAM observed");
                Check(string.Equals(observed.ExecutablePath,Assembly.GetExecutingAssembly().Location,StringComparison.OrdinalIgnoreCase),"Isolated fixture exposes its exact executable path for local grouping");
                if(blocked){
                    Check(WindowsAppResources.RequestClose(observed)==AppCloseResult.NotAllowed,"Disabled snapshot cannot dispatch close");
                    Check(WindowsAppResources.RequestClose(row)==AppCloseResult.Unavailable,"Fresh disabled/modal window rejects stale enabled selection");
                    Check(!child.HasExited,"Disabled/modal rejection preserves process");
                    continue;
                }
                Check(WindowsAppResources.RequestClose(row)==AppCloseResult.Requested,"Normal close dispatch");
                if(mode=="accept"){
                    Check(child.WaitForExit(4000),"Accepted close actually exits");
                    Check(WindowsAppResources.RequestClose(row)==AppCloseResult.Unavailable,"Exited selection unavailable");
                }else{
                    Check(WaitUntil(delegate{child.Refresh();return !child.HasExited&&child.MainWindowTitle=="Pulse close refused";},4000),"App actually handled and refused normal close");
                    Check(!child.HasExited,"App refusal must not escalate to Kill");
                }
            }finally{if(!child.HasExited)Check(child.WaitForExit(12000),"Test-owned fixture must exit on its own lifetime timer");}
        }
        Console.WriteLine("PASS app resources: optional local executable metadata without path diagnostics, candidate thresholds/ranking/exclusions without mutation, isolated close/refusal, disabled/modal rejection, identity guard, own-host exclusion, RAM observations and GPU parse bounds; no user app touched");
        return 0;
    }
}
