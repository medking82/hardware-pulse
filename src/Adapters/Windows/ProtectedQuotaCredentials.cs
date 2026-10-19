#if NET
#pragma warning disable CA1416 // Windows-only adapter; hosts check the platform before calling.
#endif
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace HardwarePulse {
    // API keys for quota providers are protected with DPAPI for the current Windows user and are
    // written to one file per provider. Keys never enter widget-settings.json, diagnostics, logs
    // or sync, and are only ever sent to that provider's fixed HTTPS endpoint.
    public static class ProtectedQuotaCredentials {
        [StructLayout(LayoutKind.Sequential)]struct DataBlob {public int Size;public IntPtr Data;}
        [DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)]static extern bool CryptProtectData(ref DataBlob input,string description,ref DataBlob entropy,IntPtr reserved,IntPtr prompt,uint flags,out DataBlob output);
        [DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)]static extern bool CryptUnprotectData(ref DataBlob input,IntPtr description,ref DataBlob entropy,IntPtr reserved,IntPtr prompt,uint flags,out DataBlob output);
        [DllImport("kernel32.dll")]static extern IntPtr LocalFree(IntPtr handle);
        const uint UiForbidden=0x1;
        const int MaxLength=16384;
        const int MaxFile=65536;
        static int MaximumLength(string provider){return QuotaCatalog.NeedsSessionHeader(provider)?65536:MaxLength;}
        public static string Directory(){return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"HardwarePulse","credentials");}
        public static string KeyFile(string provider){return System.IO.Path.Combine(Directory(),Provider(provider)+".dpapi");}
        public static bool Exists(string provider){try{return File.Exists(KeyFile(provider));}catch{return false;}}
        // Only printable ASCII is accepted: a pasted key with stray whitespace or control
        // characters is a typo, not a credential.
        public static bool Valid(string key){
            if(string.IsNullOrEmpty(key)||key.Length>MaxLength)return false;
            foreach(char character in key)if(character<0x21||character>0x7E)return false;
            return true;
        }
        public static bool Valid(string provider,string key){return QuotaCatalog.NeedsSessionHeader(provider)?JevSession.Decode(key)!=null:Valid(key);}
        public static string Read(string provider){
            string path=KeyFile(provider);
            if(!File.Exists(path))throw new QuotaFailure(QuotaCatalog.NeedsSessionHeader(provider)?"Session required":"API key required");
            byte[] protectedBytes=null;
            try{
                var info=new FileInfo(path);if(info.Length<=0||info.Length>MaxFile)throw new QuotaFailure("Credential unavailable");
                protectedBytes=File.ReadAllBytes(path);
                return Unprotect(provider,protectedBytes);
            }catch(QuotaFailure){throw;}
            catch{throw new QuotaFailure("Credential unavailable");}
            finally{if(protectedBytes!=null)Array.Clear(protectedBytes,0,protectedBytes.Length);}
        }
        public static void Save(string provider,string key){
            if(!Valid(provider,key))throw new QuotaFailure("Credential unavailable");
            string path=KeyFile(provider);string temporary=path+".tmp";
            System.IO.Directory.CreateDirectory(Directory());
            byte[] protectedBytes=null;
            try{
                protectedBytes=Protect(provider,key);
                using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None)){stream.Write(protectedBytes,0,protectedBytes.Length);stream.Flush(true);}
                if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
            }finally{
                if(protectedBytes!=null)Array.Clear(protectedBytes,0,protectedBytes.Length);
                try{if(File.Exists(temporary))File.Delete(temporary);}catch{}
            }
        }
        public static void Remove(string provider){
            try{string path=KeyFile(provider);if(File.Exists(path))File.Delete(path);}
            catch{throw new QuotaFailure("Credential unavailable");}
        }
        static byte[] Protect(string provider,string key){
            byte[] plain=null;byte[] entropy=null;IntPtr plainPointer=IntPtr.Zero,entropyPointer=IntPtr.Zero,outputPointer=IntPtr.Zero;
            try{
                plain=Encoding.UTF8.GetBytes(key);entropy=Entropy(provider);
                plainPointer=Marshal.AllocHGlobal(plain.Length);
                Marshal.Copy(plain,0,plainPointer,plain.Length);
                entropyPointer=Marshal.AllocHGlobal(entropy.Length);
                Marshal.Copy(entropy,0,entropyPointer,entropy.Length);
                var input=new DataBlob{Size=plain.Length,Data=plainPointer};
                var salt=new DataBlob{Size=entropy.Length,Data=entropyPointer};
                DataBlob output;
                if(!CryptProtectData(ref input,null,ref salt,IntPtr.Zero,IntPtr.Zero,UiForbidden,out output))throw new QuotaFailure("Credential unavailable");
                try{
                    var result=new byte[output.Size];Marshal.Copy(output.Data,result,0,result.Length);return result;
                }finally{outputPointer=output.Data;LocalFree(outputPointer);}
            }finally{
                if(plain!=null)Array.Clear(plain,0,plain.Length);
                if(plainPointer!=IntPtr.Zero){for(int index=0;index<plain.Length;index++)Marshal.WriteByte(plainPointer,index,0);Marshal.FreeHGlobal(plainPointer);}
                if(entropyPointer!=IntPtr.Zero)Marshal.FreeHGlobal(entropyPointer);
            }
        }
        static string Unprotect(string provider,byte[] protectedBytes){
            byte[] entropy=null;IntPtr protectedPointer=IntPtr.Zero,entropyPointer=IntPtr.Zero,outputPointer=IntPtr.Zero;
            try{
                entropy=Entropy(provider);
                protectedPointer=Marshal.AllocHGlobal(protectedBytes.Length);
                Marshal.Copy(protectedBytes,0,protectedPointer,protectedBytes.Length);
                entropyPointer=Marshal.AllocHGlobal(entropy.Length);
                Marshal.Copy(entropy,0,entropyPointer,entropy.Length);
                var input=new DataBlob{Size=protectedBytes.Length,Data=protectedPointer};
                var salt=new DataBlob{Size=entropy.Length,Data=entropyPointer};
                DataBlob output;
                if(!CryptUnprotectData(ref input,IntPtr.Zero,ref salt,IntPtr.Zero,IntPtr.Zero,UiForbidden,out output))throw new QuotaFailure("Credential unavailable");
                try{
                    if(output.Size<=0||output.Size>MaximumLength(provider))throw new QuotaFailure("Credential unavailable");
                    var plain=new byte[output.Size];Marshal.Copy(output.Data,plain,0,plain.Length);
                    try{
                        string key=Encoding.UTF8.GetString(plain);
                        // A protected file that no longer holds a usable key is not a key.
                        if(!Valid(provider,key))throw new QuotaFailure("Credential unavailable");
                        return key;
                    }finally{Array.Clear(plain,0,plain.Length);}
                }finally{outputPointer=output.Data;LocalFree(outputPointer);}
            }finally{
                if(protectedPointer!=IntPtr.Zero)Marshal.FreeHGlobal(protectedPointer);
                if(entropyPointer!=IntPtr.Zero)Marshal.FreeHGlobal(entropyPointer);
            }
        }
        // App-specific entropy binds every blob to this application's credential purpose.
        static byte[] Entropy(string provider){return Encoding.UTF8.GetBytes("HardwarePulse.QuotaCredential."+Provider(provider));}
        static string Provider(string provider){
            if(string.IsNullOrEmpty(provider))throw new QuotaFailure("Credential unavailable");
            foreach(char character in provider)if(!char.IsLetterOrDigit(character))throw new QuotaFailure("Credential unavailable");
            return provider;
        }
    }
}
