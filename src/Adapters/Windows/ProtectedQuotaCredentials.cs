#if !NET
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace HardwarePulse {
    // OS/user-bound encryption, separate from ordinary preferences and diagnostics.
    public sealed class ProtectedQuotaCredentials {
        readonly string root;
        public ProtectedQuotaCredentials(string stateDirectory){root=Path.Combine(stateDirectory,"credentials");}
        string FileFor(string id){var provider=QuotaCatalog.Find(id);if(provider==null||provider.Authentication!=QuotaAuthentication.ApiKey)throw new ArgumentException("Unsupported credential");return Path.Combine(root,id+".dpapi");}
        public static bool Valid(string value){return !string.IsNullOrWhiteSpace(value)&&value.Length<=16384&&Array.TrueForAll(value.ToCharArray(),c=>c>32&&c<127);}
        public bool Exists(string id){return File.Exists(FileFor(id));}
        public string Read(string id){
            string path=FileFor(id);if(!File.Exists(path))return "";
            byte[] plain=null;
            try{var info=new FileInfo(path);if(info.Length>65536)throw new QuotaFailure("Credential unavailable");plain=ProtectedData.Unprotect(File.ReadAllBytes(path),Encoding.UTF8.GetBytes("HardwarePulse/"+id),DataProtectionScope.CurrentUser);string value=Encoding.UTF8.GetString(plain);if(!Valid(value))throw new QuotaFailure("Credential unavailable");return value;}
            catch(QuotaFailure){throw;}catch{throw new QuotaFailure("Credential unavailable");}
            finally{if(plain!=null)Array.Clear(plain,0,plain.Length);}
        }
        public void Save(string id,string value){
            string path=FileFor(id);if(!Valid(value))throw new ArgumentException("Invalid credential");
            byte[] plain=Encoding.UTF8.GetBytes(value);string temp=null;
            try{byte[] encrypted=ProtectedData.Protect(plain,Encoding.UTF8.GetBytes("HardwarePulse/"+id),DataProtectionScope.CurrentUser);Directory.CreateDirectory(root);temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(encrypted,0,encrypted.Length);file.Flush(true);}if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
            finally{Array.Clear(plain,0,plain.Length);if(temp!=null&&File.Exists(temp))File.Delete(temp);}
        }
        public void Clear(string id){File.Delete(FileFor(id));}
    }
}
#endif
