using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;
using HardwarePulse;

internal static class NativeStartupTests {
    sealed class Store : ITaskStore {
        public Dictionary<string,string> Tasks=new Dictionary<string,string>();public int Writes,FailAt=-1;public string Ran;
        public string Get(string name){string value;return Tasks.TryGetValue(name,out value)?value:null;}
        public void Put(string name,string xml){Writes++;if(Writes==FailAt)throw new IOException("Injected registration failure");Tasks[name]=xml;}
        public void Delete(string name){Writes++;Tasks.Remove(name);}
        public void Run(string name){Ran=name;}
        public void Stop(string name){}
    }
    static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
    public static void Run(){
        // Exercise the real COM adapter without creating/removing any Windows task.
        // The in-memory store below cannot reproduce CLR HRESULT translation.
        using(var scheduler=new SchedulerStore()){
            Assert(scheduler.Get("Hardware Pulse Missing Regression "+Guid.NewGuid().ToString("N"))==null,"Missing task must allow fresh installation");
        }
        string exe=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Hardware Pulse","HardwarePulse.exe"),sid=WindowsIdentity.GetCurrent().User.Value;
        var store=new Store();var startup=new Startup(store,exe,sid);startup.Install();Assert(startup.IsEnabled(),"Native startup install");Assert(store.Tasks.Count==2,"Native startup task count");
        string widget=store.Tasks[Startup.Widget],collector=store.Tasks[Startup.CollectorTask];startup.SetEnabled(false);Assert(!startup.IsEnabled(),"Native startup disable");Assert(store.Tasks[Startup.CollectorTask].Contains("<Enabled>true</Enabled>"),"On-demand collector retained");startup.StartCollector();Assert(store.Ran==Startup.CollectorTask,"On-demand collector launch");
        startup.Install();Assert(!startup.IsEnabled(),"Upgrade lost disabled login preference");startup.SetEnabled(true);Assert(startup.IsEnabled(),"Native startup enable");
        var before=new Dictionary<string,string>(store.Tasks);store.FailAt=store.Writes+2;bool failed=false;try{startup.SetEnabled(false);}catch(IOException){failed=true;}Assert(failed,"Failure injection not reached");foreach(var pair in before)Assert(store.Tasks[pair.Key]==pair.Value,"Startup rollback changed XML");
        store.FailAt=-1;store.Tasks[Startup.Widget]=widget.Replace("<Command>","<Command>C:\\Unrelated\\");int writes=store.Writes;failed=false;try{startup.SetEnabled(false);}catch(System.IO.InvalidDataException){failed=true;}Assert(failed&&writes==store.Writes,"Foreign task modified before ownership preflight");
        store.Tasks[Startup.Widget]=widget.Replace("<RunLevel>LeastPrivilege</RunLevel>","<RunLevel>HighestAvailable</RunLevel>");failed=false;try{startup.IsEnabled();}catch(InvalidDataException){failed=true;}Assert(failed,"Elevated UI accepted");
        store.Tasks[Startup.Widget]=widget.Replace("<RunLevel>LeastPrivilege</RunLevel>","").Replace("<Enabled>true</Enabled>","");
        store.Tasks[Startup.CollectorTask]=collector.Replace("<Enabled>true</Enabled>","");
        Assert(startup.IsEnabled(),"Scheduler-omitted defaults rejected");startup.SetEnabled(false);Assert(!startup.IsEnabled(),"Cannot disable tasks with omitted defaults");
        foreach(string task in store.Tasks.Values){int trigger=task.IndexOf("<LogonTrigger>",StringComparison.Ordinal);Assert(task.IndexOf("<Enabled>false</Enabled>",trigger,StringComparison.Ordinal)<task.IndexOf("<UserId>",trigger,StringComparison.Ordinal),"Optional trigger Enabled violates schema order");}
        startup.SetEnabled(true);Assert(startup.IsEnabled(),"Cannot enable tasks with omitted defaults");
        store.Tasks[Startup.CollectorTask]=collector.Replace("<RunLevel>HighestAvailable</RunLevel>","");failed=false;try{startup.IsEnabled();}catch(InvalidDataException){failed=true;}Assert(failed,"Collector elevation was not required");
        store.Tasks[Startup.Widget]=widget;store.Tasks[Startup.CollectorTask]=collector;startup.Remove();Assert(store.Tasks.Count==0,"Native uninstall registration cleanup");
        Console.WriteLine("PASS native startup: exact action/SID/privilege ownership, on-demand launches, disabled preference migration, atomic rollback and unrelated-task rejection");
    }
}
