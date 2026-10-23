using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HardwarePulse;
using Microsoft.Win32.SafeHandles;

// Only disposable state and synthetic readings; never starts a collector or reads a login.
static class NativeReliabilityTests {
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly List<string> failures=new List<string>();
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CreateHardLink(string link,string target,IntPtr security);
    [DllImport("kernel32.dll",EntryPoint="CreateFileW",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle OpenFile(string path,uint access,uint share,IntPtr security,uint mode,uint flags,IntPtr template);
    static void Check(bool value,string name){if(!value)failures.Add(name);}
    static void Exercise(string name,Action action){try{action();}catch(Exception e){failures.Add(name+": "+e.GetType().Name+": "+e.Message);}}
    static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
    static void Invoke(Shell shell,string method,params object[] args){typeof(Shell).GetMethod(method,Hidden).Invoke(shell,args);}
    static void Capture(Window window,string path){window.UpdateLayout();var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(path))png.Save(stream);}
    static void Persistence(string root){
        string path=Path.Combine(root,"settings.json");
        foreach(string invalid in new[]{"{broken","null","[]"}){
            File.WriteAllText(path,invalid);var settings=new Settings(path);settings.Data["opacity"]=42;
            try{settings.Save();}catch(IOException){}catch(InvalidDataException){}
            Check(File.ReadAllText(path)==invalid,"Unreadable settings must survive Save: "+invalid);
        }
        File.WriteAllText(path,"{\"future\":{\"preserve\":7}}");Settings denied;
        using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None))denied=new Settings(path);
        denied.Data["opacity"]=40;try{denied.Save();}catch(IOException){}catch(InvalidDataException){}
        Check(File.ReadAllText(path)=="{\"future\":{\"preserve\":7}}","Transient read denial must not replace the original after the lock clears");
        File.WriteAllText(path,"{\"future\":{\"preserve\":7}}");
        var valid=new Settings(path);valid.Data["opacity"]=50;valid.Save();
        Check(new Settings(path).Map("future").ContainsKey("preserve"),"Valid settings preserve unknown fields");
        string victim=Path.Combine(root,"unrelated.txt"),snapshot=Path.Combine(root,"snapshot.json");
        File.WriteAllText(victim,"Keep this unrelated file");
        if(!CreateHardLink(snapshot+".tmp",victim,IntPtr.Zero))throw new IOException("Could not establish isolated hard-link fixture");
        Json.WriteAtomic(snapshot,new{value=1});
        Check(File.ReadAllText(victim)=="Keep this unrelated file","Atomic write must not truncate a pre-existing hard-linked temporary file");
        Check(Json.Read(snapshot).Contains("\"value\":1"),"Atomic write publishes complete JSON");
        string linked=Path.Combine(root,"linked.json");
        if(!CreateHardLink(linked,victim,IntPtr.Zero))throw new IOException("Could not establish destination hard-link fixture");
        Json.WriteAtomic(linked,new{value=2});
        Check(File.ReadAllText(victim)=="Keep this unrelated file","Replacing a hard-linked destination must preserve the other file");
        using(var locked=new FileStream(snapshot,FileMode.Open,FileAccess.Read,FileShare.Read)){
            bool failed=false;try{Json.WriteAtomic(snapshot,new{value=3});}catch(IOException){failed=true;}
            Check(failed,"Blocked replacement must report failure");
        }
        Check(Json.Read(snapshot).Contains("\"value\":1"),"Failed replacement preserves the last complete document");
        Check(Directory.GetFiles(root,"*.tmp").Length==1,"Failed replacement cleans up only its own temporary file");
        using(var reader=new FileStream(snapshot,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){
            Json.WriteAtomic(snapshot,new{value=4});
            using(var text=new StreamReader(reader))Check(text.ReadToEnd().Contains("\"value\":1"),"An existing shared reader retains the old complete snapshot");
        }
        Check(Json.Read(snapshot).Contains("\"value\":4"),"A new reader sees the replacement snapshot");
        string blockedLog=Path.Combine(root,"blocked-log");Directory.CreateDirectory(blockedLog);
        Exercise("Optional diagnostics ignore storage failure",()=>AtomicFile.TryWriteAllText(blockedLog,"diagnostic"));
        string oversized=Path.Combine(root,"oversized.json");using(var file=File.Create(oversized))file.SetLength(8*1024*1024+1);
        bool rejected=false;try{Json.Read(oversized);}catch(InvalidDataException){rejected=true;}
        Check(rejected,"JSON reads must reject files larger than the serialization limit");
        Check(SensorProfile.Read(oversized,DateTimeOffset.Now).state=="OFFLINE","An oversized snapshot becomes unavailable without escaping the reader");
        string fresh=Path.Combine(root,"fresh","settings.json");
        var newProfile=new Settings(fresh);newProfile.Data["opacity"]=35;newProfile.Save();
        Check(new Settings(fresh).Number("opacity",0,0,100)==35,"A new profile remains writable");
    }
    static void Directories(string root){
        string linked=Path.Combine(root,"directory-link"),target=Path.Combine(root,"directory-target");
        Check((File.GetAttributes(linked)&FileAttributes.ReparsePoint)!=0,"Directory fixture must be a junction");
        bool rejected=false;try{AtomicFile.WriteAllText(Path.Combine(linked,"nested","data.txt"),"data");}catch(IOException){rejected=true;}
        Check(rejected&&!Directory.Exists(Path.Combine(target,"nested")),"A linked ancestor is rejected before creating descendants");
        string pinned=Path.Combine(root,"pinned");Directory.CreateDirectory(pinned);
        using(AtomicFile.PinDirectory(pinned)){
            bool moved=false;try{Directory.Move(pinned,pinned+"-moved");moved=true;}catch(IOException){}
            Check(!moved,"A pinned directory cannot be renamed");
            using(var writer=OpenFile(pinned,0x40000000,7,IntPtr.Zero,3,0x02200000,IntPtr.Zero))Check(writer.IsInvalid,"A pinned directory rejects a competing write handle");
        }
        using(var writer=OpenFile(pinned,0x40000000,7,IntPtr.Zero,3,0x02200000,IntPtr.Zero)){
            Check(!writer.IsInvalid,"The fixture directory permits writes once the pin is released");
            rejected=false;try{AtomicFile.WriteAllText(Path.Combine(pinned,"blocked.txt"),"data");}catch(IOException){rejected=true;}
            Check(rejected&&!File.Exists(Path.Combine(pinned,"blocked.txt")),"An already writable directory handle prevents publication");
        }
        AtomicFile.WriteAllText(Path.Combine(pinned,"ready.txt"),"ready");
        Check(File.ReadAllText(Path.Combine(pinned,"ready.txt"))=="ready","Releasing a conflicting directory handle permits publication");
    }
    static void RuntimePermissions(string root){
        string ancestorAcl=Directory.GetAccessControl(root).GetSecurityDescriptorSddlForm(AccessControlSections.Access);
        var user=WindowsIdentity.GetCurrent().User;
        var users=new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid,null);
        var admins=new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null);
        var system=new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null);
        string app=Path.Combine(root,"runtime-policy"),account=Path.Combine(app,user.Value),runtime=Path.Combine(account,"runtime");
        Directory.CreateDirectory(app);
        var permissive=new DirectorySecurity();permissive.SetAccessRuleProtection(true,false);
        foreach(var sid in new[]{user,admins,system,users})permissive.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
        Directory.SetAccessControl(app,permissive);Directory.CreateDirectory(runtime);
        string snapshot=Path.Combine(runtime,"snapshot.json"),unrelated=Path.Combine(runtime,"unrelated.txt");
        File.WriteAllText(snapshot,"{}");File.WriteAllText(unrelated,"keep");
        string untouched=File.GetAccessControl(unrelated).GetSecurityDescriptorSddlForm(AccessControlSections.Access);
        RuntimeDirectory.Prepare(app,user.Value);
        foreach(string path in new[]{app,account,runtime,snapshot}){
            FileSystemSecurity acl=path==snapshot?(FileSystemSecurity)File.GetAccessControl(path):Directory.GetAccessControl(path);
            Check(acl.AreAccessRulesProtected,"Runtime ACL inheritance is protected: "+Path.GetFileName(path));
            foreach(FileSystemAccessRule rule in acl.GetAccessRules(true,true,typeof(SecurityIdentifier))){
                var sid=(SecurityIdentifier)rule.IdentityReference;
                if(sid==admins||sid==system||sid==user)continue;
                Check((rule.FileSystemRights&(FileSystemRights.Write|FileSystemRights.Delete|FileSystemRights.DeleteSubdirectoriesAndFiles|FileSystemRights.ChangePermissions|FileSystemRights.TakeOwnership))==0,"Other users have no runtime write or ownership grant: "+Path.GetFileName(path));
            }
        }
        Check(File.GetAccessControl(unrelated).GetSecurityDescriptorSddlForm(AccessControlSections.Access)==untouched,"ACL migration does not propagate into unowned children");
        Check(Directory.GetAccessControl(root).GetSecurityDescriptorSddlForm(AccessControlSections.Access)==ancestorAcl,"Runtime preparation leaves fixture ancestors unchanged");
        AtomicFile.WriteAllText(snapshot,"new snapshot");
        string stop=Path.Combine(runtime,"STOP");AtomicFile.WriteAllText(stop,"request");AtomicFile.WriteAllText(stop,"updated request");AtomicFile.Delete(stop);
        Check(!File.Exists(stop)&&File.ReadAllText(snapshot)=="new snapshot","Current user can publish, replace and clear the STOP signal under the runtime ACL");
        RuntimeDirectory.Prepare(app,user.Value);
        Check(File.ReadAllText(unrelated)=="keep","Repeated runtime preparation preserves unrelated contents");
    }
    static void Ui(string app,string root){
        string state=Path.Combine(root,"ui");Directory.CreateDirectory(state);
        Json.WriteAtomic(Path.Combine(state,"widget-settings.json"),new Dictionary<string,object>{{"language","en"},{"solid",true},{"width",360},{"height",720}});
        using(var shell=new Shell(new PulsePaths(app,state,Path.Combine(state,"runtime")),true)){
            try{
                shell.Window.ShowActivated=false;shell.Show();shell.ShowSettings(true);Pump();
                string statusPath=Path.Combine(state,"view-status.json");
                using(var locked=new FileStream(statusPath,FileMode.Open,FileAccess.Read,FileShare.Read))Exercise("Diagnostic write failure must not escape UpdatePanel",shell.UpdatePanel);
                string settingsPath=Path.Combine(state,"widget-settings.json");
                Directory.CreateDirectory(Path.Combine(state,"settings-error.txt"));
                using(var locked=new FileStream(settingsPath,FileMode.Open,FileAccess.Read,FileShare.Read))Exercise("Save failure and unavailable error log must not escape the UI",shell.Save);
                var saveStatus=shell.Control<TextBlock>("SettingsSaveStatus");
                Check(saveStatus!=null&&saveStatus.Text=="Could Not Save Changes","Failed save must be visible in Settings");
                shell.Save();Check(saveStatus!=null&&saveStatus.Text=="Changes Saved","A successful retry clears the save error");
                var tabs=(List<Button>)typeof(Shell).GetField("settingsTabs",Hidden).GetValue(shell);
                tabs[0].Focus();tabs[0].RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(tabs[0]),0,Key.Right){RoutedEvent=Keyboard.KeyDownEvent});Pump();
                Check(tabs[1].IsKeyboardFocused&&(string)typeof(Shell).GetField("settingsCategory",Hidden).GetValue(shell)=="App Appearance","Settings tabs support arrow-key selection and focus");
                var settings=(Settings)typeof(Shell).GetField("settings",Hidden).GetValue(shell);
                foreach(string locale in new[]{"en","zh-CN"})foreach(bool light in new[]{false,true}){
                    foreach(ComboBoxItem item in shell.Control<ComboBox>("LanguagePicker").Items)if((string)item.Tag==locale)shell.Control<ComboBox>("LanguagePicker").SelectedItem=item;
                    settings.Data["background"]=light?"#F4F6F8":"#202831";Invoke(shell,"ApplyMaterial");
                    shell.Window.Width=280;shell.Window.Height=720;Invoke(shell,"SelectSettingsCategory","App Appearance");Pump();
                    var slider=shell.Control<Slider>("FontSizeSlider");slider.Focus();Pump();
                    var focus=slider.Template.FindName("SliderFocus",slider) as Border;
                    Check(focus!=null&&focus.BorderBrush is SolidColorBrush&&((SolidColorBrush)focus.BorderBrush).Color.A>0,"Slider exposes a visible keyboard focus ring: "+locale+"/"+light);
                    Capture(shell.Window,Path.Combine(root,"appearance-"+locale+(light?"-light":"-dark")+".png"));
                }
            }finally{shell.Exit();}
        }
        string damaged=Path.Combine(root,"damaged-ui");Directory.CreateDirectory(damaged);string original="{broken";
        File.WriteAllText(Path.Combine(damaged,"widget-settings.json"),original);
        using(var shell=new Shell(new PulsePaths(app,damaged,Path.Combine(damaged,"runtime")),true)){
            try{shell.Window.ShowActivated=false;shell.Show();shell.ShowSettings(true);Pump();
                foreach(ComboBoxItem item in shell.Control<ComboBox>("LanguagePicker").Items)if((string)item.Tag=="en")shell.Control<ComboBox>("LanguagePicker").SelectedItem=item;
                var message=shell.Control<TextBlock>("SettingsSaveStatus");Check(message!=null&&message.Text=="Settings Unreadable · Not Saving","Unreadable settings warning is visible before any edit");shell.Save();
                shell.Window.Width=280;shell.Window.Height=720;Pump();Capture(shell.Window,Path.Combine(root,"settings-preserved.png"));
            }finally{shell.Exit();}
        }
        Check(File.ReadAllText(Path.Combine(damaged,"widget-settings.json"))==original,"Opening and closing a damaged profile preserves its bytes");
    }
    [STAThread]static int Main(string[] args){
        if(args.Length!=3)return 2;Directory.CreateDirectory(args[1]);
        if(args[2]!="ui"){
            Exercise("Persistence",()=>Persistence(args[1]));
            Exercise("Directory handles",()=>Directories(args[1]));
            Exercise("Runtime permissions",()=>RuntimePermissions(args[1]));
        }
        if(args[2]!="persistence")Exercise("UI",()=>Ui(args[0],args[1]));
        foreach(string failure in failures)Console.WriteLine("FAIL audit: "+failure);
        if(failures.Count!=0)return 1;
        Console.WriteLine("PASS audit: bounded JSON, atomic publication, pinned directories, runtime ACLs, preserved damaged settings, nonfatal diagnostics and keyboard/settings feedback");return 0;
    }
}
