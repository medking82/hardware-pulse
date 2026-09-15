using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Threading;

namespace HardwarePulse {
    // Carries only a show-home notification, never commands or caller-supplied data.
    public sealed class AppActivation : IDisposable {
        readonly EventWaitHandle signal;
        RegisteredWaitHandle listener;
        volatile bool disposed;
        [DllImport("user32.dll")]static extern bool AllowSetForegroundWindow(int processId);
        public AppActivation(string name){signal=new EventWaitHandle(false,EventResetMode.AutoReset,name);}
        public void Listen(Dispatcher dispatcher,Action showHome){
            listener=ThreadPool.RegisterWaitForSingleObject(signal,delegate(object state,bool timedOut){
                if(disposed||dispatcher.HasShutdownStarted)return;
                try{dispatcher.BeginInvoke(new Action(delegate{if(!disposed)showHome();}));}
                catch(InvalidOperationException){}
            },null,Timeout.Infinite,false);
        }
        public void Notify(string executable){
            // Transfer foreground permission from this user-launched process to its existing peer.
            using(var self=Process.GetCurrentProcess())foreach(var peer in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable))){
                using(peer)try{
                    if(peer.Id!=self.Id&&peer.SessionId==self.SessionId&&string.Equals(peer.MainModule.FileName,executable,StringComparison.OrdinalIgnoreCase))AllowSetForegroundWindow(peer.Id);
                }catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}
            }
            signal.Set();
        }
        public void Dispose(){disposed=true;if(listener!=null)listener.Unregister(null);signal.Dispose();}
    }
}
