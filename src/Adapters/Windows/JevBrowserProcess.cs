using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
namespace HardwarePulse {
    // Starts suspended, restricts inherited handles, assigns the job, then resumes.
    // No descendant can race job assignment; close/timeout affects only this owned tree.
    internal sealed class JevBrowserProcess:IDisposable {
        [StructLayout(LayoutKind.Sequential)]struct BasicLimits {public long ProcessTime,JobTime;public uint Flags;public UIntPtr MinWorkingSet,MaxWorkingSet;public uint ActiveLimit;public UIntPtr Affinity;public uint Priority,Scheduling;}
        [StructLayout(LayoutKind.Sequential)]struct IoCounters {public ulong ReadCount,WriteCount,OtherCount,ReadBytes,WriteBytes,OtherBytes;}
        [StructLayout(LayoutKind.Sequential)]struct ExtendedLimits {public BasicLimits Basic;public IoCounters Io;public UIntPtr ProcessMemory,JobMemory,PeakProcessMemory,PeakJobMemory;}
        [StructLayout(LayoutKind.Sequential)]struct Accounting {public long User,Kernel,PeriodUser,PeriodKernel;public uint Faults,Processes,Active,Terminated;}
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct StartupInfo {
            public int Size;public string Reserved,Desktop,Title;public uint X,Y,Width,Height,XChars,YChars,Fill,Flags;public ushort Show,ReservedSize;public IntPtr ReservedBytes,Input,Output,Error;
        }
        [StructLayout(LayoutKind.Sequential)]struct StartupInfoEx {public StartupInfo Startup;public IntPtr Attributes;}
        [StructLayout(LayoutKind.Sequential)]struct ProcessInfo {public IntPtr Process,Thread;public uint Id,ThreadId;}
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateJobObject(IntPtr security,string name);
        [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetInformationJobObject(IntPtr job,int information,ref ExtendedLimits limits,uint size);
        [DllImport("kernel32.dll",SetLastError=true)]static extern bool QueryInformationJobObject(IntPtr job,int information,out Accounting accounting,uint size,IntPtr length);
        [DllImport("kernel32.dll",SetLastError=true)]static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
        [DllImport("kernel32.dll",SetLastError=true)]static extern bool TerminateJobObject(IntPtr job,uint code);
        [DllImport("kernel32.dll",SetLastError=true)]static extern bool InitializeProcThreadAttributeList(IntPtr list,int count,int flags,ref IntPtr bytes);
        [DllImport("kernel32.dll",SetLastError=true)]static extern bool UpdateProcThreadAttribute(IntPtr list,uint flags,IntPtr attribute,IntPtr value,IntPtr size,IntPtr previous,IntPtr returned);
        [DllImport("kernel32.dll")]static extern void DeleteProcThreadAttributeList(IntPtr list);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CreateProcess(string executable,StringBuilder command,IntPtr processSecurity,IntPtr threadSecurity,bool inherit,uint flags,IntPtr environment,string directory,ref StartupInfoEx startup,out ProcessInfo process);
        [DllImport("kernel32.dll",SetLastError=true)]static extern uint ResumeThread(IntPtr thread);
        [DllImport("kernel32.dll")]static extern bool TerminateProcess(IntPtr process,uint code);
        [DllImport("kernel32.dll")]static extern uint WaitForSingleObject(IntPtr handle,uint milliseconds);
        [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
        IntPtr job,process;
        JevBrowserProcess(IntPtr job,IntPtr process){this.job=job;this.process=process;}
        internal static JevBrowserProcess Start(string executable,string arguments,IntPtr input,IntPtr output){
            IntPtr job=IntPtr.Zero,attributes=IntPtr.Zero,handles=IntPtr.Zero;bool initialized=false;var info=new ProcessInfo();
            try{
                job=CreateJobObject(IntPtr.Zero,null);if(job==IntPtr.Zero)throw new Win32Exception();
                var limits=new ExtendedLimits();limits.Basic.Flags=0x2000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE; no breakaway.
                if(!SetInformationJobObject(job,9,ref limits,(uint)Marshal.SizeOf(limits)))throw new Win32Exception();
                IntPtr size=IntPtr.Zero;InitializeProcThreadAttributeList(IntPtr.Zero,1,0,ref size);
                if(size.ToInt64()<=0||size.ToInt64()>65536)throw new Win32Exception();
                attributes=Marshal.AllocHGlobal(size);if(!InitializeProcThreadAttributeList(attributes,1,0,ref size))throw new Win32Exception();initialized=true;
                handles=Marshal.AllocHGlobal(2*IntPtr.Size);Marshal.Copy(new[]{input,output},0,handles,2);
                if(!UpdateProcThreadAttribute(attributes,0,new IntPtr(0x20002),handles,new IntPtr(2*IntPtr.Size),IntPtr.Zero,IntPtr.Zero))throw new Win32Exception();
                var startup=new StartupInfoEx();startup.Startup.Size=Marshal.SizeOf(startup);startup.Attributes=attributes;
                // CREATE_SUSPENDED | EXTENDED_STARTUPINFO_PRESENT | CREATE_NO_WINDOW.
                if(!CreateProcess(executable,new StringBuilder("\""+executable+"\" "+arguments),IntPtr.Zero,IntPtr.Zero,true,0x08080004,IntPtr.Zero,Path.GetDirectoryName(executable),ref startup,out info))throw new Win32Exception();
                if(!AssignProcessToJobObject(job,info.Process))throw new Win32Exception();
                if(ResumeThread(info.Thread)==uint.MaxValue)throw new Win32Exception();
                var owned=new JevBrowserProcess(job,info.Process);job=IntPtr.Zero;info.Process=IntPtr.Zero;return owned;
            }finally{
                if(info.Thread!=IntPtr.Zero)CloseHandle(info.Thread);
                if(info.Process!=IntPtr.Zero){TerminateProcess(info.Process,1);WaitForSingleObject(info.Process,2000);CloseHandle(info.Process);}
                if(job!=IntPtr.Zero)CloseHandle(job);
                if(initialized)DeleteProcThreadAttributeList(attributes);
                if(attributes!=IntPtr.Zero)Marshal.FreeHGlobal(attributes);
                if(handles!=IntPtr.Zero)Marshal.FreeHGlobal(handles);
            }
        }
        internal void Stop(){
            IntPtr owned=Interlocked.Exchange(ref job,IntPtr.Zero);if(owned==IntPtr.Zero)return;
            try{
                TerminateJobObject(owned,1);
                for(int attempt=0;attempt<40;attempt++){
                    Accounting state;if(!QueryInformationJobObject(owned,1,out state,(uint)Marshal.SizeOf(typeof(Accounting)),IntPtr.Zero)||state.Active==0)break;
                    Thread.Sleep(25);
                }
            }finally{CloseHandle(owned);}
        }
        public void Dispose(){
            IntPtr owned=Interlocked.Exchange(ref process,IntPtr.Zero);
            if(owned!=IntPtr.Zero)WaitForSingleObject(owned,200);
            Stop();if(owned!=IntPtr.Zero){WaitForSingleObject(owned,2000);CloseHandle(owned);}
        }
    }
}
