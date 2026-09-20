using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;
using HardwarePulse;

static class SharedStartupTests {
    sealed class Store : ITaskStore {
        public readonly Dictionary<string,string> Tasks=new Dictionary<string,string>();
        public int Writes,FailBefore=-1,FailAfter=-1,Stops;public bool FailRun,FailStop;public string Ran;public Action<string> Reading;public Action Running;
        public string Get(string name){if(Reading!=null)Reading(name);string value;return Tasks.TryGetValue(name,out value)?value:null;}
        public void Put(string name,string xml){Writes++;if(Writes==FailBefore)throw new IOException("Before write");Tasks[name]=xml;if(Writes==FailAfter)throw new IOException("After write");}
        public void Delete(string name){Writes++;Tasks.Remove(name);}
        public void Run(string name){Ran=name;if(Running!=null)Running();if(FailRun)throw new IOException("Injected collector launch failure");}
        public void Stop(string name){Stops++;if(FailStop)throw new IOException("Injected collector stop failure");}
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Rejected(Action action){bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}Check(rejected,"Foreign ownership accepted");}
    public static void Run() {
        string exe=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Hardware Pulse","HardwarePulse.exe");
        string worker=Path.Combine(Path.GetDirectoryName(exe),"worker","HardwarePulse.Collector.exe");
        string sid=WindowsIdentity.GetCurrent().User.Value;
        var fresh=new Store();var shared=Startup.Shared(fresh,exe,sid);shared.Install();
        Check(fresh.Tasks[Startup.Widget].Contains("<Command>"+exe+"</Command>")&&fresh.Tasks[Startup.CollectorTask].Contains("<Command>"+worker+"</Command>"),"Shared task actions incorrect");
        Check(shared.IsEnabled(),"Fresh shared startup disabled");shared.SetEnabled(false);Check(!shared.IsEnabled(),"Shared startup disable");shared.StartCollector();Check(fresh.Ran==Startup.CollectorTask,"Disabled logon loses on-demand collector");shared.Remove();Check(fresh.Tasks.Count==0,"Shared uninstall did not remove owned tasks");
        for(int failure=0;failure<5;failure++) {
            var store=new Store();var legacy=new Startup(store,exe,sid);legacy.Install();legacy.SetEnabled(false);
            var before=new Dictionary<string,string>(store.Tasks);
            if(failure==1)store.FailBefore=store.Writes+1;
            if(failure==2)store.FailBefore=store.Writes+2;
            if(failure==3)store.FailAfter=store.Writes+1;
            if(failure==4)store.FailAfter=store.Writes+2;
            shared=Startup.Shared(store,exe,sid);bool failed=false;
            try{shared.InstallAndStartCollector();}catch(IOException){failed=true;}
            Check(failed==(failure!=0),"Migration failure injection mismatch");
            Check(!failed||store.Ran==null,"Registration failure attempted collector launch");
            if(failed)foreach(var task in before)Check(store.Tasks[task.Key]==task.Value,"Migration failed to restore exact prior XML");
            else {
                Check(!shared.IsEnabled(),"Migration lost disabled preference");shared.Validate(Startup.CollectorTask,store.Tasks[Startup.CollectorTask]);
                Rejected(()=>legacy.Validate(Startup.CollectorTask,store.Tasks[Startup.CollectorTask]));
                shared.Install();Check(!shared.IsEnabled(),"Repeated install changed disabled preference");
            }
        }
        foreach(string mutation in new[]{"path","arguments","user","level"}) {
            var store=new Store();new Startup(store,exe,sid).Install();string original=store.Tasks[Startup.CollectorTask];
            store.Tasks[Startup.CollectorTask]=mutation=="path"?original.Replace(exe,exe+".foreign"):
                mutation=="arguments"?original.Replace("--collector","--collector --extra"):
                mutation=="user"?original.Replace(sid,"S-1-5-18"):original.Replace("HighestAvailable","LeastPrivilege");
            int writes=store.Writes;shared=Startup.Shared(store,exe,sid);Rejected(()=>shared.ValidateInstall());Rejected(()=>shared.Install());Check(store.Writes==writes,"Ownership rejection wrote tasks");
        }
        var changed=new Store();new Startup(changed,exe,sid).Install();string widget=changed.Tasks[Startup.Widget];string foreign=changed.Tasks[Startup.CollectorTask].Replace(exe,exe+".foreign");int reads=0;
        changed.Reading=name=>{if(name==Startup.CollectorTask&&++reads==2)changed.Tasks[name]=foreign;};
        bool conflict=false;try{Startup.Shared(changed,exe,sid).Install();}catch(IOException){conflict=true;}
        Check(conflict&&changed.Tasks[Startup.Widget]==widget&&changed.Tasks[Startup.CollectorTask]==foreign,"Concurrent task change overwritten or preceding write not restored");
        var mixed=new Store();new Startup(mixed,exe,sid).Install();mixed.Tasks[Startup.Widget]=mixed.Tasks[Startup.Widget].Replace("<Enabled>true</Enabled>","<Enabled>false</Enabled>");
        shared=Startup.Shared(mixed,exe,sid);shared.Install();Check(mixed.Tasks[Startup.Widget].Contains("<Enabled>false</Enabled>")&&!mixed.Tasks[Startup.CollectorTask].Contains("<Enabled>false</Enabled>"),"Mixed task preferences merged");
        var old=new Store();new Startup(old,exe,sid).Install();shared=Startup.Shared(old,exe,sid);
        Rejected(()=>shared.SetEnabled(false));Rejected(()=>shared.StartCollector());Rejected(()=>shared.Remove());
        var empty=new Store{FailAfter=2};bool freshFailed=false;try{Startup.Shared(empty,exe,sid).Install();}catch(IOException){freshFailed=true;}
        Check(freshFailed&&empty.Tasks.Count==0,"Failed fresh install left new tasks");
        var broken=new Store();new Startup(broken,exe,sid).Install();broken.FailAfter=broken.Writes+2;broken.FailBefore=broken.Writes+3;
        bool incomplete=false;try{Startup.Shared(broken,exe,sid).Install();}catch(AggregateException error){incomplete=error.InnerExceptions.Count==2;}
        Check(incomplete&&broken.Tasks[Startup.CollectorTask].Contains(worker),"Incomplete rollback reported success");
        foreach(bool upgrade in new[]{false,true}) {
            var launch=new Store();
            if(upgrade){var previous=new Startup(launch,exe,sid);previous.Install();previous.SetEnabled(false);}
            var preimage=new Dictionary<string,string>(launch.Tasks);launch.FailRun=true;
            int admittedWrites=launch.Writes;Startup.Shared(launch,exe,sid).ValidateInstall();
            Check(launch.Writes==admittedWrites&&launch.Ran==null,"Installation preflight mutated or launched tasks");
            bool launchFailed=false;try{Startup.Shared(launch,exe,sid).InstallAndStartCollector();}catch(IOException){launchFailed=true;}
            Check(launchFailed&&launch.Tasks.Count==preimage.Count,"Launch failure left committed startup tasks");
            foreach(var task in preimage)Check(launch.Tasks[task.Key]==task.Value,"Launch failure lost exact startup XML/preference");
            Check(launch.Stops==1,"Uncertain collector launch was not stopped before rollback");
            launch.FailRun=false;shared=Startup.Shared(launch,exe,sid);shared.InstallAndStartCollector();
            Check(launch.Ran==Startup.CollectorTask&&shared.IsEnabled()==!upgrade,"Successful install/launch changed login preference");
        }
        var failedStop=new Store{FailRun=true,FailStop=true};bool stopFailureReported=false;
        try{Startup.Shared(failedStop,exe,sid).InstallAndStartCollector();}catch(AggregateException error){stopFailureReported=error.InnerExceptions.Count==2;}
        Check(stopFailureReported&&failedStop.Tasks.Count==0,"Collector cleanup failure was hidden or task rollback skipped");
        var replaced=new Store{FailRun=true};string replacedXml=null;
        replaced.Running=()=>{replacedXml=replaced.Tasks[Startup.CollectorTask].Replace(worker,worker+".foreign");replaced.Tasks[Startup.CollectorTask]=replacedXml;};
        bool ownershipFailure=false;try{Startup.Shared(replaced,exe,sid).InstallAndStartCollector();}catch(AggregateException error){ownershipFailure=error.InnerExceptions.Count>=2;}
        Check(ownershipFailure&&replaced.Stops==0&&replaced.Tasks.Count==1&&replaced.Tasks[Startup.CollectorTask]==replacedXml,"Launch cleanup stopped or overwrote a concurrently replaced foreign task");
        Rejected(()=>Startup.Shared(new Store(),exe+".other",sid));
        Console.WriteLine("PASS shared startup: fixed actions, legacy migration, disabled/mixed preferences, registration/launch rollback, cleanup failures, concurrent changes and foreign ownership");
    }
}
