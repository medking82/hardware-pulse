using System.Runtime.InteropServices;
using Avalonia.Controls;

// Fixture setup only. A real owned-window click establishes normal user intent;
// foreground/no-activation assertions still read the OS, never a substituted value.
static class NativeFixtureActivation {
    public static Func<bool> WithClick(Window fixture,Func<bool> ready,bool requireClick=false)=>new Activation(fixture,ready,requireClick).Step;
    sealed class Activation(Window fixture,Func<bool> ready,bool requireClick) {
        readonly bool topmost=fixture.Topmost;
        nint handle;
        bool sent,cleaned;
        string? rejected;
        Point previous;
        public bool Step() {
            if(!sent) {
                if(!requireClick&&ready())return true;
                TrySend();
                return false; // Deliver the input through the real native loop.
            }
            if((GetAsyncKeyState(1)&0x8000)!=0||!GetCursorPos(out var cursor)||Math.Abs(cursor.X-previous.X)>1||Math.Abs(cursor.Y-previous.Y)>1||!ready())return false;
            Cleanup();
            return ready(); // Windows may consume an activation click before routing managed pointer events.
        }
        void TrySend() {
            if(!OperatingSystem.IsWindows()||!fixture.IsVisible){Reject("not-visible");return;}
            handle=fixture.TryGetPlatformHandle()?.Handle??0;
            Check(handle!=0&&GetWindowThreadProcessId(handle,out uint owner)!=0&&owner==(uint)Environment.ProcessId,"Fixture input requires this process's live HWND");
            if(IsIconic(handle)||!GetClientRect(handle,out var rect)||rect.Right<100||rect.Bottom<100){Reject("not-ready-client");return;}
            if(!GetUserObjectInformationW(GetThreadDesktop(GetCurrentThreadId()),6,out int input,4,out _)||input==0){Reject("not-input-desktop");return;}
            foreach(int key in new[]{1,2,4,0x10,0x11,0x12,0x5B,0x5C})if((GetAsyncKeyState(key)&0x8000)!=0){Reject($"input-held vk={key:X}");return;}
            if(!GetCursorPos(out previous)){Reject($"cursor-unavailable error={Marshal.GetLastWin32Error()}");return;}
            try {
                fixture.Topmost=true;
                // A foreign surface can cover the center. Every candidate must still hit this owned client.
                var candidates=new[]{new Point{X=rect.Right/2,Y=rect.Bottom/2},new Point{X=16,Y=16},new Point{X=rect.Right-16,Y=16},new Point{X=16,Y=rect.Bottom-16},new Point{X=rect.Right-16,Y=rect.Bottom-16}};
                var point=new Point();var hits=new List<nint>();bool found=false;
                foreach(var candidate in candidates) {
                    point=candidate;
                    if(!ClientToScreen(handle,ref point)){Reject("client-to-screen");return;}
                    nint hit=GetAncestor(WindowFromPoint(point),2);hits.Add(hit);
                    if(hit==handle){found=true;break;}
                }
                if(!found) {
                    nint obstruct=hits[0];GetWindowRect(obstruct,out var blocked);
                    bool nativeTopmost=(GetWindowLongPtrW(handle,-20).ToInt64()&8)!=0;
                    Reject($"hit-obstructed roots={string.Join(',',hits)} nativeTopmost={nativeTopmost} blockerRect={blocked.Left},{blocked.Top},{blocked.Right},{blocked.Bottom}");return;
                }
                fixture.Closed+=OnClosed;
                // Keep movement/click/restoration in one ordered input batch.
                var events=new[]{Move(point),new Input{Mouse=new MouseInput{Flags=2}},new Input{Mouse=new MouseInput{Flags=4}},Move(previous)};
                uint count=SendInput((uint)events.Length,events,Marshal.SizeOf<Input>());int error=Marshal.GetLastWin32Error();
                if(count!=events.Length) {
                    if(count==2)SendInput(1,new[]{new Input{Mouse=new MouseInput{Flags=4}}},Marshal.SizeOf<Input>());
                    if(count>0)SendInput(1,new[]{Move(previous)},Marshal.SizeOf<Input>());
                    Cleanup();
                    throw new Exception($"Owned fixture input blocked: sent={count}; error={error}");
                }
                sent=true;
                Console.WriteLine($"NATIVE_FIXTURE_CLICK owned={handle} sent={count}");Console.Out.Flush();
            } finally {if(!sent)fixture.Topmost=topmost;}
        }
        void Reject(string reason) {
            if(rejected==reason)return;rejected=reason;
            Console.WriteLine($"NATIVE_FIXTURE_BLOCKED owned={handle} reason={reason}");Console.Out.Flush();
        }
        void OnClosed(object? sender,EventArgs args)=>Cleanup();
        void Cleanup() {
            if(cleaned)return;cleaned=true;
            fixture.Closed-=OnClosed;
            if(IsWindow(handle))fixture.Topmost=topmost;
        }
    }
    static Input Move(Point point) {
        int left=GetSystemMetrics(76),top=GetSystemMetrics(77),width=GetSystemMetrics(78),height=GetSystemMetrics(79);
        Check(width>1&&height>1,"Fixture input requires a valid virtual desktop");
        return new(){Mouse=new(){X=(int)Math.Clamp(Math.Round((point.X-left)*65535d/(width-1)),0,65535),Y=(int)Math.Clamp(Math.Round((point.Y-top)*65535d/(height-1)),0,65535),Flags=0xC001}};
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    [StructLayout(LayoutKind.Sequential)] struct Point {public int X,Y;}
    [StructLayout(LayoutKind.Sequential)] struct Rect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] struct MouseInput {public int X,Y;public uint Data,Flags,Time;public nuint Extra;}
    [StructLayout(LayoutKind.Sequential)] struct Input {public uint Type;public MouseInput Mouse;}
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint window,out uint process);
    [DllImport("user32.dll")] static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] static extern bool GetClientRect(nint window,out Rect rect);
    [DllImport("user32.dll")] static extern bool GetWindowRect(nint window,out Rect rect);
    [DllImport("user32.dll")] static extern nint GetWindowLongPtrW(nint window,int index);
    [DllImport("user32.dll")] static extern bool ClientToScreen(nint window,ref Point point);
    [DllImport("user32.dll")] static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] static extern nint GetAncestor(nint window,uint flags);
    [DllImport("user32.dll")] static extern nint GetThreadDesktop(uint thread);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern bool GetUserObjectInformationW(nint handle,int index,out int value,int length,out int needed);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll",SetLastError=true)] static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll",SetLastError=true)] static extern uint SendInput(uint count,Input[] events,int size);
}
