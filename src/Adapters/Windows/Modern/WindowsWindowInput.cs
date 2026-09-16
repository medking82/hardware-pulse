#nullable enable
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace HardwarePulse;

// Own-window input policy only. Does not hook input or modify another process.
public sealed class WindowsWindowInput {
    const int ExtendedStyle=-20;
    const long Layered=0x80000,Transparent=0x20,NoActivate=0x08000000;
    readonly nint handle;
    long originalBits;
    bool applied;
    public WindowsWindowInput(nint handle) {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        this.handle=handle;CheckOwner();
    }
    void CheckOwner() {
        if(handle==0||GetWindowThreadProcessId(handle,out uint process)==0||process!=(uint)Environment.ProcessId)
            throw new InvalidOperationException("An owned live window is required");
    }
    public void SetPassThrough(bool enabled) {
        CheckOwner();if(enabled==applied)return;
        long before=GetWindowLongPtrW(handle,ExtendedStyle).ToInt64();
        if(enabled&&(before&Layered)!=0)throw new InvalidOperationException("Existing layered rendering must retain its own input policy");
        const long mask=Layered|Transparent|NoActivate;
        long wanted=enabled?before|mask:(before&~mask)|originalBits;
        Marshal.SetLastPInvokeError(0);
        if(SetWindowLongPtrW(handle,ExtendedStyle,(nint)wanted)==0&&Marshal.GetLastPInvokeError()!=0)throw new Win32Exception(Marshal.GetLastPInvokeError());
        if(enabled&&!SetLayeredWindowAttributes(handle,0,255,2)) {
            int error=Marshal.GetLastPInvokeError();SetWindowLongPtrW(handle,ExtendedStyle,(nint)before);throw new Win32Exception(error);
        }
        if(enabled)originalBits=before&mask;
        applied=enabled;
    }
    [DllImport("user32.dll",SetLastError=true)] static extern nint GetWindowLongPtrW(nint window,int index);
    [DllImport("user32.dll",SetLastError=true)] static extern nint SetWindowLongPtrW(nint window,int index,nint value);
    [DllImport("user32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] static extern bool SetLayeredWindowAttributes(nint window,uint key,byte alpha,uint flags);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint window,out uint process);
}
