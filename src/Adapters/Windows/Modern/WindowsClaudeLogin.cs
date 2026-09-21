using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace HardwarePulse;

public interface IWindowsCredentialReader {
    byte[]? Read(string target,CancellationToken cancel);
}

// Reads Claude Code's current-user credential without prompting or modifying its store.
public sealed class WindowsClaudeLogin {
    readonly Func<CancellationToken,string> fileToken;
    readonly IWindowsCredentialReader credentials;
    readonly string userName;
    public WindowsClaudeLogin(Func<CancellationToken,string> fileToken,IWindowsCredentialReader credentials,string userName) {
        this.fileToken=fileToken??throw new ArgumentNullException(nameof(fileToken));
        this.credentials=credentials??throw new ArgumentNullException(nameof(credentials));
        this.userName=userName??string.Empty;
    }
    public static WindowsClaudeLogin Default() {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Windows Credential Manager required");
        return new WindowsClaudeLogin(ClaudeFileLogin.Default().Read,new NativeWindowsCredentialReader(),Environment.UserName);
    }
    public string Read(CancellationToken cancel) {
        cancel.ThrowIfCancellationRequested();
        try{return fileToken(cancel);}
        catch(QuotaFailure failure)when(failure.Status=="Login required") { }
        foreach(string target in Targets(userName)) {
            cancel.ThrowIfCancellationRequested();
            byte[]? data=credentials.Read(target,cancel);
            if(data==null)continue;
            try {
                if(data.Length==0||data.Length>1048576)throw new QuotaFailure("Login unavailable");
                if(LooksUtf16(data)) {
                    byte[] utf8=Encoding.UTF8.GetBytes(Encoding.Unicode.GetString(data).TrimEnd('\0'));
                    try{return ClaudeFileLogin.ParseCredential(utf8,cancel);}
                    finally{CryptographicOperations.ZeroMemory(utf8);}
                }
                return ClaudeFileLogin.ParseCredential(data,cancel);
            }finally{CryptographicOperations.ZeroMemory(data);}
        }
        throw new QuotaFailure("Login required");
    }
    static IEnumerable<string> Targets(string user) {
        yield return "Claude Code-credentials";
        if(!string.IsNullOrWhiteSpace(user)&&user.Length<=256&&!user.Any(char.IsControl)) {
            yield return "Claude Code-credentials:"+user;
            yield return "Claude Code-credentials/"+user;
        }
    }
    static bool LooksUtf16(byte[] value)=>value.Length>=4&&value.Length%2==0&&(value[1]==0||value[3]==0);
}

sealed class NativeWindowsCredentialReader:IWindowsCredentialReader {
    const uint Generic=1;
    public byte[]? Read(string target,CancellationToken cancel) {
        cancel.ThrowIfCancellationRequested();
        if(!CredRead(target,Generic,0,out IntPtr pointer)) {
            int error=Marshal.GetLastWin32Error();
            if(error==1168)return null;
            throw new QuotaFailure("Login unavailable");
        }
        try {
            var credential=Marshal.PtrToStructure<Credential>(pointer);
            if(credential.CredentialBlobSize==0||credential.CredentialBlob==IntPtr.Zero)return null;
            if(credential.CredentialBlobSize>1048576)throw new QuotaFailure("Login unavailable");
            byte[] result=new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob,result,0,result.Length);
            try{cancel.ThrowIfCancellationRequested();return result;}
            catch{CryptographicOperations.ZeroMemory(result);throw;}
        }finally{CredFree(pointer);}
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    struct Credential {
        public uint Flags,Type;
        public IntPtr TargetName,Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist,AttributeCount;
        public IntPtr Attributes,TargetAlias,UserName;
    }
    [DllImport("advapi32.dll",EntryPoint="CredReadW",CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]static extern bool CredRead(string target,uint type,uint reservedFlag,out IntPtr credentialPtr);
    [DllImport("advapi32.dll")]static extern void CredFree(IntPtr buffer);
}
