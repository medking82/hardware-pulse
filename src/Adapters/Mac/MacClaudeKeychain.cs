using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace HardwarePulse {
    public interface IMacClaudeKeychainApi {
        int GetInteraction(out byte allowed);
        int SetInteraction(byte allowed);
        int Find(byte[] service,byte[] account,out uint length,out IntPtr data);
        void Free(IntPtr data);
    }
    // Serializes this process's temporary legacy-Keychain UI guard; never changes stored ACLs.
    public sealed class MacClaudeKeychain {
        static readonly object gate=new();
        readonly IMacClaudeKeychainApi api;
        public MacClaudeKeychain():this(new NativeApi()) {
            if(!OperatingSystem.IsMacOS())throw new PlatformNotSupportedException("macOS Keychain required");
        }
        public MacClaudeKeychain(IMacClaudeKeychainApi api){this.api=api??throw new ArgumentNullException(nameof(api));}
        public byte[] Read(string service,string account,CancellationToken cancel) {
            if(service!="Claude Code-credentials"&&!(service!=null&&service.StartsWith("Claude Code-credentials-",StringComparison.Ordinal)&&service.Length==32&&IsHex(service.Substring(24))))
                throw new ArgumentException("Invalid Claude service",nameof(service));
            if(string.IsNullOrWhiteSpace(account)||account.Length>256||Array.Exists(account.ToCharArray(),char.IsControl))throw new QuotaFailure("Login unavailable");
            cancel.ThrowIfCancellationRequested();
            lock(gate) {
                cancel.ThrowIfCancellationRequested();
                if(api.GetInteraction(out byte original)!=0)throw new QuotaFailure("Login unavailable");
                if(api.SetInteraction(0)!=0)throw new QuotaFailure("Login unavailable");
                IntPtr data=IntPtr.Zero;
                byte[] bytes=null;
                bool restored=false;
                try {
                  try {
                    int result=api.Find(Encoding.UTF8.GetBytes(service),Encoding.UTF8.GetBytes(account),out uint length,out data);
                    cancel.ThrowIfCancellationRequested();
                    if(result!=-25300) { // Only item-not-found permits the owning host's file fallback.
                    if(result!=0)throw new QuotaFailure("Login unavailable");
                    if(length==0||length>1048576||data==IntPtr.Zero)throw new QuotaFailure("Login unavailable");
                    bytes=new byte[length];Marshal.Copy(data,bytes,0,bytes.Length);
                    }
                  }finally {
                    try{if(data!=IntPtr.Zero)api.Free(data);}
                    finally{restored=api.SetInteraction(original)==0;}
                  }
                  cancel.ThrowIfCancellationRequested();
                  if(!restored)throw new QuotaFailure("Login unavailable");
                  return bytes;
                }catch {
                  // Until return, this method owns the copy, including restore-failure paths.
                  if(bytes!=null)CryptographicOperations.ZeroMemory(bytes);
                  throw;
                }
            }
        }
        static bool IsHex(string value){foreach(char c in value)if(!((c>='0'&&c<='9')||(c>='a'&&c<='f')))return false;return true;}
        sealed class NativeApi:IMacClaudeKeychainApi {
            const string Security="/System/Library/Frameworks/Security.framework/Security";
            [DllImport(Security)]static extern int SecKeychainGetUserInteractionAllowed(out byte allowed);
            [DllImport(Security)]static extern int SecKeychainSetUserInteractionAllowed(byte allowed);
            [DllImport(Security)]static extern int SecKeychainFindGenericPassword(IntPtr keychain,uint serviceLength,byte[] service,uint accountLength,byte[] account,out uint passwordLength,out IntPtr password,IntPtr item);
            [DllImport(Security)]static extern int SecKeychainItemFreeContent(IntPtr attributes,IntPtr data);
            public int GetInteraction(out byte allowed)=>SecKeychainGetUserInteractionAllowed(out allowed);
            public int SetInteraction(byte allowed)=>SecKeychainSetUserInteractionAllowed(allowed);
            public int Find(byte[] service,byte[] account,out uint length,out IntPtr data)=>SecKeychainFindGenericPassword(IntPtr.Zero,(uint)service.Length,service,(uint)account.Length,account,out length,out data,IntPtr.Zero);
            public void Free(IntPtr data){SecKeychainItemFreeContent(IntPtr.Zero,data);}
        }
    }
}
