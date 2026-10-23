using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace HardwarePulse {
    // Keep the directory chain and the new file open through publication. Neither
    // a directory swap nor a temporary-name swap may redirect an elevated writer.
    public static class AtomicFile {
        public static void WriteAllBytes(string path,byte[] bytes,bool flushToDisk=false) {
            path=Path.GetFullPath(path);
            string parent=Path.GetDirectoryName(path);
            using(PinnedDirectory.Open(parent,true)) {
                string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
                using(var handle=FileHandles.Open(temporary,0x40010000,0,1,0x00200000)) { // WRITE | DELETE, CREATE_NEW
                    if(handle.IsInvalid)throw FileHandles.Error("Create temporary file");
                    bool published=false;
                    using(var stream=new FileStream(handle,FileAccess.Write)) {
                        try {
                            stream.Write(bytes,0,bytes.Length);
                            if(flushToDisk)stream.Flush(true);else stream.Flush();
                            // Same-directory FILE_RENAME_INFORMATION: no target path is
                            // reopened, and the exclusive source handle stays open.
                            byte[] name=Encoding.Unicode.GetBytes(Path.GetFileName(path));
                            int offset=2*IntPtr.Size+4;
                            byte[] rename=new byte[offset+name.Length+4];rename[0]=1;
                            Buffer.BlockCopy(BitConverter.GetBytes(name.Length),0,rename,2*IntPtr.Size,4);
                            Buffer.BlockCopy(name,0,rename,offset,name.Length);
                            FileHandles.Rename(handle,rename);
                            published=true;
                        } finally {
                            // Delete only the file still held by this call, never a name
                            // another process could have replaced after the handle closed.
                            if(!published)FileHandles.SetInformation(handle,4,new byte[]{1},1);
                        }
                    }
                }
            }
        }

        public static IDisposable PinDirectory(string path) {return PinnedDirectory.Open(path,true);}
        public static void EnsureDirectory(string path) {using(PinDirectory(path)){} }
        public static void Delete(string path) {
            path=Path.GetFullPath(path);
            using(PinnedDirectory.Open(Path.GetDirectoryName(path),false))File.Delete(path);
        }

        public static void WriteAllText(string path,string text,bool flushToDisk=false) {
            WriteAllBytes(path,new UTF8Encoding(false).GetBytes(text),flushToDisk);
        }

        public static void TryWriteAllText(string path,string text) {
            // Optional diagnostics must not replace the error they are reporting.
            try{WriteAllText(path,text);}catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }

    internal static class FileHandles {
        internal const uint MaximumAllowed=0x02000000;
        [StructLayout(LayoutKind.Sequential)]internal struct Information {
            internal uint Attributes;
            internal System.Runtime.InteropServices.ComTypes.FILETIME Created,Accessed,Written;
            internal uint Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;
        }
        [DllImport("kernel32.dll",EntryPoint="CreateFileW",CharSet=CharSet.Unicode,SetLastError=true)]
        internal static extern SafeFileHandle Open(string path,uint access,uint share,IntPtr security,uint disposition,uint flags,IntPtr template);
        internal static SafeFileHandle Open(string path,uint access,uint share,uint disposition,uint flags) {return Open(path,access,share,IntPtr.Zero,disposition,flags,IntPtr.Zero);}
        [DllImport("kernel32.dll",EntryPoint="GetFileInformationByHandle",SetLastError=true)]
        internal static extern bool GetInformation(SafeFileHandle handle,out Information information);
        [DllImport("kernel32.dll",EntryPoint="SetFileInformationByHandle",SetLastError=true)]
        internal static extern bool SetInformation(SafeFileHandle handle,int kind,byte[] information,int length);
        [StructLayout(LayoutKind.Sequential)]struct IoStatus {internal IntPtr Status,Information;}
        [DllImport("ntdll.dll")]static extern int NtSetInformationFile(SafeFileHandle handle,out IoStatus status,byte[] information,int length,int kind);
        [DllImport("ntdll.dll")]static extern int RtlNtStatusToDosError(int status);
        internal static void Rename(SafeFileHandle handle,byte[] information) {
            // Win32's wrapper resolves a destination path and reopens its directory.
            // The native same-directory form works while our directory pin is held.
            // Modern Windows can retain readers of the old snapshot during replacement.
            information[0]=3; // REPLACE_IF_EXISTS | POSIX_SEMANTICS
            IoStatus result;int status=NtSetInformationFile(handle,out result,information,information.Length,65);
            if(status==unchecked((int)0xC0000003)||status==unchecked((int)0xC00000BB)) { // Unsupported information class/filesystem, including Windows 7.
                information[0]=1;status=NtSetInformationFile(handle,out result,information,information.Length,10);
            }
            if(status!=0)throw Error("Publish file",RtlNtStatusToDosError(status));
        }
        internal static IOException Error(string operation,int error=0) {return new IOException(operation+": "+new Win32Exception(error==0?Marshal.GetLastWin32Error():error).Message);}
    }

    internal sealed class PinnedDirectory:IDisposable {
        readonly Dictionary<string,SafeFileHandle> handles=new Dictionary<string,SafeFileHandle>(StringComparer.OrdinalIgnoreCase);
        readonly List<SafeFileHandle> order=new List<SafeFileHandle>();
        [DllImport("kernel32.dll",EntryPoint="CreateDirectoryW",CharSet=CharSet.Unicode,SetLastError=true)]
        static extern bool CreateDirectory(string path,IntPtr security);
        internal SafeFileHandle this[string path] {get{return handles[Path.GetFullPath(path)];}}
        internal static PinnedDirectory Open(string path,bool create,string aclRoot=null) {
            path=Path.GetFullPath(path);
            var result=new PinnedDirectory();
            try {
                var parents=new Stack<string>();
                for(var directory=new DirectoryInfo(path);directory!=null;directory=directory.Parent)parents.Push(directory.FullName);
                while(parents.Count!=0) {
                    string current=parents.Pop();
                    bool changeAcl=aclRoot!=null&&(string.Equals(current,aclRoot,StringComparison.OrdinalIgnoreCase)||current.StartsWith(aclRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase));
                    uint access=changeAcl?FileHandles.MaximumAllowed:0x81; // LIST_DIRECTORY | READ_ATTRIBUTES
                    // No write/delete sharing: pin both directory identity and reparse state.
                    var handle=FileHandles.Open(current,access,1,3,0x02200000);
                    if(handle.IsInvalid) {
                        int error=Marshal.GetLastWin32Error();handle.Dispose();
                        if(!create||(error!=2&&error!=3))throw FileHandles.Error("Open directory "+current,error);
                        if(!CreateDirectory(current,IntPtr.Zero)&&Marshal.GetLastWin32Error()!=183)throw FileHandles.Error("Create directory");
                        handle=FileHandles.Open(current,access,1,3,0x02200000);
                    }
                    result.order.Add(handle);
                    if(handle.IsInvalid)throw FileHandles.Error("Open directory");
                    FileHandles.Information info;
                    if(!FileHandles.GetInformation(handle,out info))throw FileHandles.Error("Inspect directory");
                    if((info.Attributes&0x410)!=0x10)throw new IOException("Directory must not be a reparse point: "+current);
                    result.handles.Add(current,handle);
                }
                return result;
            } catch {result.Dispose();throw;}
        }
        public void Dispose() {for(int i=order.Count-1;i>=0;i--)order[i].Dispose();}
    }
}
