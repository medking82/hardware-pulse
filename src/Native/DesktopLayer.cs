using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace HardwarePulse {
    // Only changes our own top-level window. No parenting into Explorer, injection,
    // wallpaper replacement, or changes to another process's windows.
    public sealed class DesktopLayer : IDisposable {
        delegate bool Enumerate(IntPtr window,IntPtr state);
        delegate void WinEvent(IntPtr hook,uint kind,IntPtr window,int obj,int child,uint thread,uint time);
        [DllImport("user32.dll")] static extern bool EnumWindows(Enumerate callback,IntPtr state);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string title);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder name,int count);
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window,uint command);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int cx,int cy,uint flags);
        [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
        [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr window,int index,IntPtr value);
        [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min,uint max,IntPtr module,WinEvent callback,uint process,uint thread,uint flags);
        [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hook);
        readonly Window window;readonly IntPtr hwnd;readonly WinEvent callback;
        IntPtr hook;bool closed,queued,locked,styleApplied,alwaysOnTop;
        public bool Attached {get;private set;}
        public DesktopLayer(Window window,bool initiallyLocked=true) {
            this.window=window;hwnd=new WindowInteropHelper(window).Handle;
            callback=delegate {if(closed||queued)return;queued=true;window.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(delegate{queued=false;if(!closed)Refresh();}));};
            hook=SetWinEventHook(3,3,IntPtr.Zero,callback,0,0,0); // foreground changed
            SetLocked(initiallyLocked);
        }
        public void SetLocked(bool value) {
            if(styleApplied&&locked==value)return;
            locked=value;
            long style=GetWindowLongPtr(hwnd,-20).ToInt64();
            style|=0x80; // tool window
            // Only the locked readout is non-activating and click-through.
            // The editor must receive normal activation and mouse input.
            style=value?style|0x08000020L:style&~0x08000020L;
            SetWindowLongPtr(hwnd,-20,new IntPtr(style));
            styleApplied=true;
        }
        public void SetAlwaysOnTop(bool value){
            if(alwaysOnTop==value)return;
            alwaysOnTop=value;
            window.Topmost=value;
            SetWindowPos(hwnd,value?new IntPtr(-1):new IntPtr(-2),0,0,0,0,0x1|0x2|0x10);
            Refresh();
        }
        static IntPtr DesktopHost() {
            IntPtr found=IntPtr.Zero;
            EnumWindows(delegate(IntPtr candidate,IntPtr unused){
                var cls=new StringBuilder(64);GetClassName(candidate,cls,cls.Capacity);
                if((cls.ToString()=="Progman"||cls.ToString()=="WorkerW")&&IsWindowVisible(candidate)&&FindWindowEx(candidate,IntPtr.Zero,"SHELLDLL_DefView",null)!=IntPtr.Zero){found=candidate;return false;}
                return true;
            },IntPtr.Zero);
            return found;
        }
        public void Refresh() {
            if(closed||!IsWindow(hwnd))return;
            if(alwaysOnTop){
                if(!window.IsVisible)window.Show();
                Attached=SetWindowPos(hwnd,new IntPtr(-1),0,0,0,0,0x1|0x2|0x10|0x40);
                return;
            }
            IntPtr host=DesktopHost();Attached=host!=IntPtr.Zero;
            if(!Attached){window.Hide();return;} // retry via the existing UI poll after Explorer returns
            if(!window.IsVisible)window.Show();
            // Insert immediately above the desktop icon host, below ordinary windows.
            // Re-discovery also handles Show Desktop, wallpaper switches and Explorer restart.
            IntPtr preceding=GetWindow(host,3); // GW_HWNDPREV
            if(preceding!=hwnd)Attached=SetWindowPos(hwnd,preceding,0,0,0,0,0x1|0x2|0x10|0x40|0x200);
            else if(!IsWindowVisible(hwnd))Attached=SetWindowPos(hwnd,IntPtr.Zero,0,0,0,0,0x1|0x2|0x4|0x10|0x40|0x200);
        }
        public bool ClickThrough {get{return locked;}}
        public bool CanSampleBackground {get{
            if(!locked||!Attached)return false;
            var cls=new StringBuilder(64);GetClassName(GetForegroundWindow(),cls,cls.Capacity);
            return cls.ToString()=="Progman"||cls.ToString()=="WorkerW";
        }}
        public void Dispose(){if(closed)return;closed=true;if(hook!=IntPtr.Zero)UnhookWinEvent(hook);hook=IntPtr.Zero;}
    }
}
