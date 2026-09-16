using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;
using HardwarePulse;

static class SharedStartupTests {
    sealed class Store : ITaskStore {
        public readonly Dictionary<string,string> Tasks=new Dictionary<string,string>();
        public int Writes,FailBefore=-1,FailAfter=-1;public string Ran;public Action<string> Reading;
        public string Get(string name){if(Reading!=null)Reading(name);string value;return Tasks.TryGetValue(name,out value)?value:null;}
        public void Put(string name,string xml){Writes++;if(Writes==FailBefore)throw new IOException("Before write");Tasks[name]=xml;if(Writes==FailAfter)throw new IOException("After write");}
        public void Delete(string name){Writes++;Tasks.Remove(name);}
        public void Run(string name){Ran=name;}
        public void Stop(string name){}
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
            try{shared.Install();}catch(IOException){failed=true;}
            Check(failed==(failure!=0),"Migration failure injection mismatch");
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
            int writes=store.Writes;shared=Startup.Shared(store,exe,sid);Rejected(()=>shared.Install());Check(store.Writes==writes,"Ownership rejection wrote tasks");
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
        Rejected(()=>Startup.Shared(new Store(),exe+".other",sid));
        Console.WriteLine("PASS shared startup: fixed actions, legacy migration, disabled/mixed preferences, pre/post-write rollback, concurrent changes and foreign ownership");
    }
}
