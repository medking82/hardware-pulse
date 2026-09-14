using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Xml;

namespace HardwarePulse {
    // The store isolates OS mutations so ownership and rollback can be tested without
    // touching a user's tasks. XML is retained verbatim for transaction rollback.
    public interface ITaskStore {
        string Get(string name);
        void Put(string name,string xml);
        void Delete(string name);
        void Run(string name);
        void Stop(string name);
    }
    public sealed class SchedulerStore : ITaskStore, IDisposable {
        dynamic service,folder;
        public SchedulerStore(){service=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service",true));service.Connect();folder=service.GetFolder("\\");}
        public string Get(string name){
            dynamic task=null;
            try{task=folder.GetTask(name);return (string)task.Xml;}
            // CLR COM interop maps HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND)
            // to FileNotFoundException on a missing task, not always COMException.
            catch(FileNotFoundException e){if(e.HResult==unchecked((int)0x80070002))return null;throw;}
            catch(COMException e){if(e.ErrorCode==unchecked((int)0x80070002))return null;throw;}
            finally{Release(task);}
        }
        public void Put(string name,string xml){dynamic task=null;try{task=folder.RegisterTask(name,xml,6,null,null,3,null);}finally{Release(task);}}
        public void Delete(string name){folder.DeleteTask(name,0);}
        public void Run(string name){dynamic task=null,instance=null;try{task=folder.GetTask(name);instance=task.Run(null);}finally{Release(instance);Release(task);}}
        public void Stop(string name){dynamic task=null;try{task=folder.GetTask(name);task.Stop(0);}finally{Release(task);}}
        static void Release(object value){if(value!=null&&Marshal.IsComObject(value))Marshal.FinalReleaseComObject(value);}
        public void Dispose(){Release(folder);Release(service);folder=null;service=null;}
    }
    public sealed class Startup {
        public const string Widget="Hardware Pulse Widget",CollectorTask="Hardware Pulse Collector";
        readonly ITaskStore store;readonly string exe,sid;
        public Startup(ITaskStore store,string exe,string sid){this.store=store;this.exe=Path.GetFullPath(exe);this.sid=sid;}
        static XmlDocument Parse(string xml){var doc=new XmlDocument();doc.XmlResolver=null;doc.LoadXml(xml);return doc;}
        static XmlNamespaceManager Ns(XmlDocument doc){var ns=new XmlNamespaceManager(doc.NameTable);ns.AddNamespace("t","http://schemas.microsoft.com/windows/2004/02/mit/task");return ns;}
        static string Value(XmlDocument doc,string path){var node=doc.SelectSingleNode(path,Ns(doc));return node==null?"":node.InnerText;}
        static void Set(XmlDocument doc,string path,string value){
            var node=doc.SelectSingleNode(path,Ns(doc));
            if(node==null){
                // Task Scheduler omits optional values equal to schema defaults.
                string parentPath=path.Substring(0,path.LastIndexOf('/'));
                var parent=doc.SelectSingleNode(parentPath,Ns(doc));
                if(parent==null||!path.EndsWith("/t:Enabled",StringComparison.Ordinal))throw new InvalidDataException("Unexpected startup task XML");
                node=doc.CreateElement("Enabled","http://schemas.microsoft.com/windows/2004/02/mit/task");
                // Base trigger elements precede LogonTrigger's UserId/Delay extension.
                var extension=parent.SelectSingleNode("t:UserId|t:Delay",Ns(doc));
                if(extension==null)parent.AppendChild(node);else parent.InsertBefore(node,extension);
            }
            node.InnerText=value;
        }
        string ResolveSid(string user){return user.StartsWith("S-1-",StringComparison.OrdinalIgnoreCase)?new SecurityIdentifier(user).Value:((SecurityIdentifier)new NTAccount(user).Translate(typeof(SecurityIdentifier))).Value;}
        public void Validate(string name,string xml){
            var doc=Parse(xml);var ns=Ns(doc);
            if(name!=Widget&&name!=CollectorTask)throw new InvalidDataException("Unknown startup task");
            if(doc.SelectNodes("/t:Task/t:Principals/t:Principal",ns).Count!=1)throw new InvalidDataException("Unexpected startup principals");
            string principal=Value(doc,"/t:Task/t:Principals/t:Principal/@id"),context=Value(doc,"/t:Task/t:Actions/@Context");
            if(context.Length>0&&context!=principal)throw new InvalidDataException("Startup action principal mismatch");
            string command=Value(doc,"/t:Task/t:Actions/t:Exec/t:Command"),args=Value(doc,"/t:Task/t:Actions/t:Exec/t:Arguments");
            if(doc.SelectNodes("/t:Task/t:Actions/*",ns).Count!=1||!string.Equals(command,exe,StringComparison.OrdinalIgnoreCase)||args!=(name==CollectorTask?"--collector":""))throw new InvalidDataException("Startup task action ownership mismatch");
            if(ResolveSid(Value(doc,"/t:Task/t:Principals/t:Principal/t:UserId"))!=sid)throw new InvalidDataException("Startup task user ownership mismatch");
            if(doc.SelectNodes("/t:Task/t:Triggers/*",ns).Count!=1||doc.SelectNodes("/t:Task/t:Triggers/t:LogonTrigger",ns).Count!=1)throw new InvalidDataException("Unexpected startup trigger");
            string triggerUser=Value(doc,"/t:Task/t:Triggers/t:LogonTrigger/t:UserId");
            if(triggerUser.Length>0&&ResolveSid(triggerUser)!=sid)throw new InvalidDataException("Startup trigger user mismatch");
            string runLevel=Value(doc,"/t:Task/t:Principals/t:Principal/t:RunLevel");
            if(runLevel.Length==0)runLevel="LeastPrivilege";
            if(runLevel!=(name==CollectorTask?"HighestAvailable":"LeastPrivilege")||Value(doc,"/t:Task/t:Principals/t:Principal/t:LogonType")!="InteractiveToken")throw new InvalidDataException("Unexpected startup privilege");
        }
        bool Enabled(string xml){var doc=Parse(xml);return Value(doc,"/t:Task/t:Settings/t:Enabled")!="false"&&Value(doc,"/t:Task/t:Triggers/t:LogonTrigger/t:Enabled")!="false";}
        public bool IsEnabled(){string a=store.Get(Widget),b=store.Get(CollectorTask);if(a==null||b==null)return false;Validate(Widget,a);Validate(CollectorTask,b);return Enabled(a)&&Enabled(b);}
        public void StartCollector(){string xml=store.Get(CollectorTask);if(xml==null)throw new InvalidDataException("Collector task missing");Validate(CollectorTask,xml);store.Run(CollectorTask);}
        static string Escape(string s){return System.Security.SecurityElement.Escape(s);}
        string NewXml(string name,bool enabled){
            bool collector=name==CollectorTask;
            return "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\"><RegistrationInfo><Description>"+(collector?"Hardware Pulse read-only local sensor collector.":"Hardware Pulse desktop widget for the current interactive user.")+"</Description></RegistrationInfo><Triggers><LogonTrigger><Enabled>"+(enabled?"true":"false")+"</Enabled><UserId>"+Escape(sid)+"</UserId>"+(collector?"":"<Delay>PT10S</Delay>")+"</LogonTrigger></Triggers><Principals><Principal id=\"Author\"><UserId>"+Escape(sid)+"</UserId><LogonType>InteractiveToken</LogonType><RunLevel>"+(collector?"HighestAvailable":"LeastPrivilege")+"</RunLevel></Principal></Principals><Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries><StopIfGoingOnBatteries>false</StopIfGoingOnBatteries><AllowStartOnDemand>true</AllowStartOnDemand><Enabled>true</Enabled><ExecutionTimeLimit>PT0S</ExecutionTimeLimit></Settings><Actions Context=\"Author\"><Exec><Command>"+Escape(exe)+"</Command>"+(collector?"<Arguments>--collector</Arguments>":"")+"<WorkingDirectory>"+Escape(Path.GetDirectoryName(exe))+"</WorkingDirectory></Exec></Actions></Task>";
        }
        Dictionary<string,string> OwnedTasks(bool required){
            var result=new Dictionary<string,string>();foreach(string name in new[]{Widget,CollectorTask}){string xml=store.Get(name);if(xml==null&&required)throw new InvalidDataException("Startup task missing");if(xml!=null)Validate(name,xml);result[name]=xml;}return result;
        }
        void Commit(Dictionary<string,string> before,Dictionary<string,string> after){
            var changed=new List<string>();
            try{foreach(var entry in after){changed.Add(entry.Key);store.Put(entry.Key,entry.Value);string actual=store.Get(entry.Key);Validate(entry.Key,actual);if(Enabled(actual)!=Enabled(entry.Value))throw new IOException("Startup state did not change");}}
            catch(Exception original){
                var failures=new List<Exception>();failures.Add(original);
                for(int i=changed.Count-1;i>=0;i--){string name=changed[i];try{string current=store.Get(name);if(current!=null)Validate(name,current);if(before[name]==null){if(current!=null)store.Delete(name);}else store.Put(name,before[name]);}catch(Exception rollback){failures.Add(rollback);}}
                if(failures.Count>1)throw new AggregateException("Startup change and rollback failed",failures);throw;
            }
        }
        public void SetEnabled(bool enabled){var before=OwnedTasks(true);var after=new Dictionary<string,string>();foreach(var entry in before){var doc=Parse(entry.Value);Set(doc,"/t:Task/t:Settings/t:Enabled","true");Set(doc,"/t:Task/t:Triggers/t:LogonTrigger/t:Enabled",enabled?"true":"false");after[entry.Key]=doc.OuterXml;}Commit(before,after);}
        public void Install(){
            string prefix=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).TrimEnd('\\')+"\\";
            if(!exe.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Startup registration requires a Program Files installation.");
            var before=OwnedTasks(false);var after=new Dictionary<string,string>();foreach(var entry in before)after[entry.Key]=NewXml(entry.Key,entry.Value==null||Enabled(entry.Value));Commit(before,after);
        }
        public void Remove(){var before=OwnedTasks(false);foreach(var entry in before)if(entry.Value!=null){store.Stop(entry.Key);store.Delete(entry.Key);}}
    }
}
