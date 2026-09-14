using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using HardwarePulse;

// Opt-in interactive integration test: briefly toggles Show Desktop, then restores it.
// No collector, installed settings, wallpaper configuration, or Explorer changes.
static class NativeDesktopLayerTests {
    [DllImport("user32.dll")] static extern IntPtr GetTopWindow(IntPtr parent);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window,uint command);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,int message,IntPtr w,IntPtr l);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out WindowSnap.Rect rect);
    static void CheckEdgeSnap(DesktopView view){
        var hwnd=new WindowInteropHelper(view).Handle;var work=SystemParameters.WorkArea;
        var matrix=PresentationSource.FromVisual(view).CompositionTarget.TransformToDevice;
        var start=matrix.Transform(new Point(work.Left+DesktopView.EdgePadding,work.Top+DesktopView.EdgePadding));var end=matrix.Transform(new Point(work.Right-DesktopView.EdgePadding,work.Bottom-DesktopView.EdgePadding));
        var saved=new Point(view.Left,view.Top);var memory=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WindowSnap.Rect)));
        try{for(int edge=0;edge<4;edge++){
            view.Left=edge==0?work.Left+8:edge==1?work.Right-view.ActualWidth-8:work.Left+100;
            view.Top=edge==2?work.Top+8:edge==3?work.Bottom-view.ActualHeight-8:work.Top+100;
            WindowSnap.Rect rect;Assert(GetWindowRect(hwnd,out rect),"Desktop bounds missing");
            SendMessage(hwnd,0x0231,IntPtr.Zero,IntPtr.Zero);Marshal.StructureToPtr(rect,memory,false);
            SendMessage(hwnd,0x0216,IntPtr.Zero,memory);var snapped=(WindowSnap.Rect)Marshal.PtrToStructure(memory,typeof(WindowSnap.Rect));
            SendMessage(hwnd,0x0232,IntPtr.Zero,IntPtr.Zero);
            Assert(edge==0?snapped.Left==(int)start.X:edge==1?snapped.Right==(int)end.X:edge==2?snapped.Top==(int)start.Y:snapped.Bottom==(int)end.Y,"Desktop native edge snap failed: "+edge);
        }}finally{Marshal.FreeHGlobal(memory);view.Left=saved.X;view.Top=saved.Y;}
    }
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
    static bool Above(IntPtr a,IntPtr b){for(var h=GetTopWindow(IntPtr.Zero);h!=IntPtr.Zero;h=GetWindow(h,2)){if(h==a)return true;if(h==b)return false;}throw new Exception("Test window missing from z-order");}
    static void Assert(bool value,string message){if(!value)throw new Exception(message);}
    [STAThread] static int Main(string[] args){
        bool desktopShown=false;object shell=null;Type shellType=null;
        try{
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            var view=new DesktopView((name,size,color)=>new TextBlock{Text="◇",FontSize=size,Foreground=new SolidColorBrush((Color)ColorConverter.ConvertFromString(color))});
            view.Left=SystemParameters.WorkArea.Left+60;view.Top=SystemParameters.WorkArea.Top+120;
            view.Show();view.Render(new[]{new DesktopMetric("cpu","CPU","52.4 °C   18%","cpu"),new DesktopMetric("gpu","GPU","44.6 °C   12%","gpu"),new DesktopMetric("ram","Memory","10.4 / 32 GB","memory")},20,20,"#E4F3EF",true);view.RefreshLayer();
            Assert(view.LayerAvailable,"Desktop host not found");
            var hwnd=new WindowInteropHelper(view).Handle;
            Assert((GetWindowLongPtr(hwnd,-20).ToInt64()&0x20)!=0,"Locked desktop is not click-through");
            view.Render(new[]{new DesktopMetric("cpu","CPU","52.4 °C   18%","cpu")},20,20,"#E4F3EF",false);
            Assert((GetWindowLongPtr(hwnd,-20).ToInt64()&0x20)==0,"Unlocked desktop still click-through");
            CheckEdgeSnap(view);
            view.Render(new[]{new DesktopMetric("cpu","CPU","52.4 °C   18%","cpu"),new DesktopMetric("gpu","GPU","44.6 °C   12%","gpu"),new DesktopMetric("ram","Memory","10.4 / 32 GB","memory")},20,20,"#E4F3EF",true);
            var cover=new Window{Title="Pulse Desktop Layer Test",Width=450,Height=200,Left=view.Left,Top=view.Top,Content=new TextBlock{Text="Desktop readings must stay behind this ordinary window.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(20)},ShowInTaskbar=false};cover.Show();view.RefreshLayer();
            Assert(Above(new WindowInteropHelper(cover).Handle,hwnd),"Desktop covers ordinary app");
            Assert((GetWindowLongPtr(hwnd,-20).ToInt64()&8)==0,"Desktop unexpectedly topmost");
            int tick=0;var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};
            timer.Tick+=delegate{
                try{
                    tick++;
                    if(tick==2){shellType=Type.GetTypeFromProgID("Shell.Application");shell=Activator.CreateInstance(shellType);shellType.InvokeMember("ToggleDesktop",BindingFlags.InvokeMethod,null,shell,null);desktopShown=true;}
                    if(tick>=3)view.RefreshLayer();
                    if(tick==5){
                        Assert(IsWindowVisible(hwnd),"Show Desktop hides the readings");
                        Assert(DesktopContrast.Sample(view).HasValue,"Desktop background sampling unavailable");
                        var textColor=view.ResolveColor(true,"#FFFFFF");
                        view.Render(new[]{new DesktopMetric("cpu","CPU","52.4 °C   18%","cpu"),new DesktopMetric("gpu","GPU Fan 1","700 RPM","gpu"),new DesktopMetric("gpu2","GPU Fan 2","0 RPM","gpu")},20,20,textColor,true);view.SetTextOpacity(100);
                        File.WriteAllText(args[0],"PASS native four-edge snap, layer found, click-through/unlock, below ordinary app, non-topmost, visible after Show Desktop, local background sampling\n");
                        var point=view.PointToScreen(new Point());var scale=PresentationSource.FromVisual(view).CompositionTarget.TransformToDevice;
                        var size=scale.Transform(new Point(view.ActualWidth,view.ActualHeight));
                        using(var bitmap=new System.Drawing.Bitmap((int)size.X+20,(int)size.Y+20)){
                            using(var graphics=System.Drawing.Graphics.FromImage(bitmap))graphics.CopyFromScreen((int)point.X-10,(int)point.Y-10,0,0,bitmap.Size);
                            bitmap.Save(Path.Combine(Path.GetDirectoryName(args[0]),"desktop-live.png"),System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }
                    if(tick==15){shellType.InvokeMember("ToggleDesktop",BindingFlags.InvokeMethod,null,shell,null);desktopShown=false;}
                    if(tick==17){view.RefreshLayer();Assert(Above(new WindowInteropHelper(cover).Handle,hwnd),"Desktop did not return below ordinary app");timer.Stop();cover.Close();view.Close();app.Shutdown();}
                }catch(Exception e){File.AppendAllText(args[0],e.ToString());timer.Stop();app.Shutdown(1);}
            };timer.Start();int code=app.Run();return code;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
        finally{if(desktopShown&&shell!=null)shellType.InvokeMember("ToggleDesktop",BindingFlags.InvokeMethod,null,shell,null);if(shell!=null)Marshal.FinalReleaseComObject(shell);}
    }
}
