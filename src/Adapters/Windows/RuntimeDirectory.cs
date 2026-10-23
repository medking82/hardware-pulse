using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace HardwarePulse {
    public static class RuntimeDirectory {
        static readonly SecurityIdentifier administrators=new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null);
        static readonly SecurityIdentifier system=new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null);
        static readonly SecurityIdentifier users=new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid,null);
        static readonly string[] outputs={"STOP","snapshot.json","collector-stage.txt","memory-warning.txt","disk-warning.txt","write-warning.txt","collector-error.txt","fps-warning.txt"};
        const InheritanceFlags Children=InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit;

        // Only the caller's SID tree is changed. Existing children are never recursively
        // re-ACL'd: a legacy directory may contain links to files outside the runtime.
        public static void Prepare(string applicationRoot,string userSid) {
            applicationRoot=Path.GetFullPath(applicationRoot).TrimEnd(Path.DirectorySeparatorChar);
            var user=new SecurityIdentifier(userSid);
            string account=Path.Combine(applicationRoot,user.Value),runtime=Path.Combine(account,"runtime");
            using(var identity=WindowsIdentity.GetCurrent()) {
                bool elevated=new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
                var owner=elevated?administrators:identity.User;
                using(var directories=PinnedDirectory.Open(runtime,true,applicationRoot)) {
                    foreach(string directory in new[]{applicationRoot,account,runtime})ValidateOwner(Directory.GetAccessControl(directory,AccessControlSections.Owner),user);
                    Apply(directories[applicationRoot],Policy(users,FileSystemRights.ReadAndExecute,true),owner,elevated);
                    Apply(directories[account],Policy(user,FileSystemRights.ReadAndExecute,true),owner,elevated);
                    var policy=Policy(user,FileSystemRights.ReadAndExecute|FileSystemRights.Write,true);
                    policy.AddAccessRule(new FileSystemAccessRule(user,FileSystemRights.Modify,Children,PropagationFlags.InheritOnly,AccessControlType.Allow));
                    Apply(directories[runtime],policy,owner,elevated);
                    foreach(string name in outputs) {
                        using(var file=FileHandles.Open(Path.Combine(runtime,name),FileHandles.MaximumAllowed,0,3,0x00200000)) {
                            if(file.IsInvalid) {if(Marshal.GetLastWin32Error()==2)continue;throw FileHandles.Error("Open runtime file");}
                            FileHandles.Information info;
                            if(!FileHandles.GetInformation(file,out info))throw FileHandles.Error("Inspect runtime file");
                            if((info.Attributes&0x410)!=0||info.Links!=1)throw new IOException("Runtime file must be a regular, single-link file: "+name);
                            ValidateOwner(File.GetAccessControl(Path.Combine(runtime,name),AccessControlSections.Owner),user);
                            Apply(file,Policy(user,FileSystemRights.Modify,false),owner,elevated);
                        }
                    }
                }
            }
        }

        static void ValidateOwner(FileSystemSecurity security,SecurityIdentifier user) {
            var owner=(SecurityIdentifier)security.GetOwner(typeof(SecurityIdentifier));
            // Do not take over another account's pre-created objects: it may retain
            // permission-changing handles even after their DACL/owner is replaced.
            if(owner!=administrators&&owner!=system&&owner!=user)throw new UnauthorizedAccessException("Runtime object is owned by another account.");
        }

        static DirectorySecurity Policy(SecurityIdentifier reader,FileSystemRights rights,bool directory) {
            var policy=new DirectorySecurity();policy.SetAccessRuleProtection(true,false);
            var inheritance=directory?Children:InheritanceFlags.None;
            policy.AddAccessRule(new FileSystemAccessRule(administrators,FileSystemRights.FullControl,inheritance,PropagationFlags.None,AccessControlType.Allow));
            policy.AddAccessRule(new FileSystemAccessRule(system,FileSystemRights.FullControl,inheritance,PropagationFlags.None,AccessControlType.Allow));
            policy.AddAccessRule(new FileSystemAccessRule(reader,rights,InheritanceFlags.None,PropagationFlags.None,AccessControlType.Allow));
            return policy;
        }

        [DllImport("advapi32.dll",EntryPoint="SetSecurityInfo")]
        static extern uint SetSecurity(SafeFileHandle handle,int kind,uint fields,byte[] owner,IntPtr group,byte[] dacl,IntPtr sacl);
        static void Apply(SafeFileHandle handle,DirectorySecurity policy,SecurityIdentifier owner,bool setOwner) {
            var descriptor=new RawSecurityDescriptor(policy.GetSecurityDescriptorBinaryForm(),0);
            byte[] acl=new byte[descriptor.DiscretionaryAcl.BinaryLength];descriptor.DiscretionaryAcl.GetBinaryForm(acl,0);
            byte[] sid=null;if(setOwner){sid=new byte[owner.BinaryLength];owner.GetBinaryForm(sid,0);}
            // MAXIMUM_ALLOWED handles suppress SetSecurityInfo's child propagation.
            uint error=SetSecurity(handle,1,0x80000004u|(setOwner?1u:0u),sid,IntPtr.Zero,acl,IntPtr.Zero);
            if(error!=0)throw new IOException("Protect runtime permissions: "+new System.ComponentModel.Win32Exception((int)error).Message);
        }
    }
}
