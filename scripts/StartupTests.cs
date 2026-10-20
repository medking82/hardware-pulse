using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;
using System.Xml;
using HardwarePulse;

// Focused startup harness: task mutations and launches use only in-memory
// stores. NativeStartupTests also makes a read-only missing-task COM query.
static class StartupTests {
    sealed class Store : ITaskStore {
        public readonly Dictionary<string,string> Tasks=new Dictionary<string,string>();
        public int Writes,FailAt=-1;public string Ran;
        public string Get(string name){string xml;return Tasks.TryGetValue(name,out xml)?xml:null;}
        public void Put(string name,string xml){if(++Writes==FailAt)throw new IOException("Injected registration failure");Tasks[name]=xml;}
        public void Delete(string name){Tasks.Remove(name);}
        public void Run(string name){Ran=name;}
        public void Stop(string name){}
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static XmlDocument Parse(string xml){var doc=new XmlDocument();doc.XmlResolver=null;doc.LoadXml(xml);return doc;}
    static XmlNode Select(XmlDocument doc,string path){
        var ns=new XmlNamespaceManager(doc.NameTable);ns.AddNamespace("t",doc.DocumentElement.NamespaceURI);
        return doc.SelectSingleNode("/t:Task/"+path,ns);
    }
    static void CheckPolicy(Store store){
        var collector=Parse(store.Tasks[Startup.CollectorTask]);
        var interval=Select(collector,"t:Settings/t:RestartOnFailure/t:Interval");
        var count=Select(collector,"t:Settings/t:RestartOnFailure/t:Count");
        Check(interval!=null&&interval.InnerText=="PT1M"&&count!=null&&count.InnerText=="3","Collector must retry three times at one-minute intervals");
        Check(Select(Parse(store.Tasks[Startup.Widget]),"t:Settings/t:RestartOnFailure")==null,"Widget must not restart on failure");
    }
    static Startup Create(Store store,string exe,string sid,bool shared){return shared?Startup.Shared(store,exe,sid):new Startup(store,exe,sid);}
    static void RestartPolicy(){
        string exe=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Hardware Pulse","HardwarePulse.exe");
        string sid=WindowsIdentity.GetCurrent().User.Value;
        foreach(bool shared in new[]{false,true}){
            var fresh=new Store();Create(fresh,exe,sid,shared).Install();CheckPolicy(fresh);
            foreach(bool missing in new[]{true,false})foreach(int preferences in new[]{0,1,2,3}){
                var store=new Store();var startup=Create(store,exe,sid,shared);startup.Install();
                foreach(string name in new[]{Startup.Widget,Startup.CollectorTask}){
                    var doc=Parse(store.Tasks[name]);
                    bool enabled=(preferences&(name==Startup.Widget?1:2))!=0;
                    Select(doc,"t:Triggers/t:LogonTrigger/t:Enabled").InnerText=enabled?"true":"false";
                    // Also repair a previously task-disabled collector to retain
                    // on-demand access, without turning its logon trigger on.
                    if(name==Startup.CollectorTask){
                        if(preferences==0)Select(doc,"t:Settings/t:Enabled").InnerText="false";
                        var restart=Select(doc,"t:Settings/t:RestartOnFailure");
                        if(missing)restart.ParentNode.RemoveChild(restart);
                        else {Select(doc,"t:Settings/t:RestartOnFailure/t:Interval").InnerText="PT5M";Select(doc,"t:Settings/t:RestartOnFailure/t:Count").InnerText="10";}
                    }
                    store.Tasks[name]=doc.OuterXml;
                }
                var before=new Dictionary<string,string>(store.Tasks);
                // Reinstall must roll back exact pre-policy definitions if the
                // collector write fails after the widget was registered.
                store.FailAt=store.Writes+2;bool failed=false;
                try{startup.Install();}catch(IOException){failed=true;}
                Check(failed,"Restart repair failure injection not reached");
                foreach(var task in before)Check(store.Tasks[task.Key]==task.Value,"Failed restart repair changed prior task XML");
                store.FailAt=-1;startup.Install();CheckPolicy(store);
                Check(store.Tasks[Startup.Widget]==before[Startup.Widget],"Collector repair changed widget definition");
                foreach(string name in new[]{Startup.Widget,Startup.CollectorTask}){
                    startup.Validate(name,store.Tasks[name]);
                    var doc=Parse(store.Tasks[name]);
                    Check(Select(doc,"t:Settings/t:Enabled").InnerText=="true","Repair lost on-demand task execution");
                    bool enabled=(preferences&(name==Startup.Widget?1:2))!=0;
                    Check(Select(doc,"t:Triggers/t:LogonTrigger/t:Enabled").InnerText==(enabled?"true":"false"),"Repair merged independent startup preferences");
                }
                startup.StartCollector();Check(store.Ran==Startup.CollectorTask,"Repaired collector cannot launch on demand");
                startup.SetEnabled(false);CheckPolicy(store);Check(!startup.IsEnabled(),"Restart policy prevents disabling startup");
                startup.SetEnabled(true);CheckPolicy(store);Check(startup.IsEnabled(),"Restart policy prevents enabling startup");
            }
        }
        Console.WriteLine("PASS collector restart: native/shared registration, bounded policy, owned-task repair, independent preferences, on-demand access and exact repair rollback");
    }
    static int Main(){
        try{RestartPolicy();NativeStartupTests.Run();SharedStartupTests.Run();return 0;}
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
