using Avalonia.Controls;
using Avalonia.Platform;
using System.Windows.Input;

namespace HardwarePulse.Desktop;

// The existing window owns sampling. Hiding requires a reachable native tray.
public sealed class DesktopTray : IDisposable {
    readonly Window window;
    readonly TrayIcon icon;
    readonly UiLanguage language;
    bool disposed;
    readonly bool closeToTray;
    readonly Func<bool> trayAvailable;
    public bool IsAvailable=>!disposed&&trayAvailable();
    public NativeMenu Menu { get; }=new();
    public DesktopTray(Window window,bool closeToTray=false,Func<bool>? trayAvailable=null) {
        this.window=window;
        this.closeToTray=closeToTray;
        language=(window as MonitorWindow)?.Language??new UiLanguage();
        using var stream=AssetLoader.Open(new Uri("avares://Pulse.Desktop/Assets/pulse.ico"));
        var artwork=new WindowIcon(stream);
        window.Icon=artwork;
        Menu.Items.Add(new NativeMenuItem("Open Pulse") {Command=new ActionCommand(this,Restore)});
        Menu.Items.Add(new NativeMenuItem("Quit Pulse") {Command=new ActionCommand(this,window.Close)});
        if(window is MonitorWindow monitor) {
            Menu.Items.Insert(1,new NativeMenuItem("Open floating monitor") {Command=new ActionCommand(this,monitor.OpenFloatingMonitor)});
            Menu.Items.Insert(2,new NativeMenuItem("Done") {Command=new ActionCommand(this,()=>monitor.FloatingMonitor?.SetLocked(true),()=>monitor.FloatingMonitor is {IsVisible:true,IsLocked:false,CanLock:true})});
            Menu.Items.Insert(3,new NativeMenuItem("Return to App") {Command=new ActionCommand(this,()=>monitor.FloatingMonitor?.Close(),()=>monitor.FloatingMonitor!=null)});
            Menu.Items.Insert(4,new NativeMenuItem("Screenshot mode · 15 seconds") {Command=new ActionCommand(this,()=>monitor.FloatingMonitor?.BeginScreenshot(),()=>monitor.FloatingMonitor?.CanBeginScreenshot==true)});
        }
        Menu.NeedsUpdate+=UpdateMenu;
        language.Changed+=Localize;Localize();
        icon=new TrayIcon {Icon=artwork,ToolTipText="Pulse",Menu=Menu,IsVisible=true};
        this.trayAvailable=trayAvailable??(()=>icon.NativeMenuExporter!=null);
        icon.Clicked+=OnClicked;
        window.Closed+=OnClosed;
        window.Closing+=OnClosing;
        if(window is MonitorWindow owner)owner.UserCloseRequested+=UserClose;
    }
    void UserClose(){if(disposed)return;if(closeToTray&&IsAvailable)window.Hide();else window.Close();}
    void OnClosing(object? sender,WindowClosingEventArgs e){
        if(disposed||!closeToTray||!IsAvailable||e.Cancel||e.IsProgrammatic||e.CloseReason!=WindowCloseReason.WindowClosing)return;
        e.Cancel=true;window.Hide();
    }
    void OnClicked(object? sender,EventArgs e)=>Restore();
    void Localize(){
        ((NativeMenuItem)Menu.Items[0]).Header=language.T("Open Pulse");
        if(window is MonitorWindow) {
            ((NativeMenuItem)Menu.Items[1]).Header=language.T("Open floating monitor");
            ((NativeMenuItem)Menu.Items[2]).Header=language.T("Done");
            ((NativeMenuItem)Menu.Items[3]).Header=language.T("Return to App");
            ((NativeMenuItem)Menu.Items[4]).Header=language.T("Screenshot mode · 15 seconds");
        }
        ((NativeMenuItem)Menu.Items[Menu.Items.Count-1]).Header=language.T("Quit Pulse");
        UpdateMenu(null,EventArgs.Empty);
    }
    void UpdateMenu(object? sender,EventArgs e){foreach(var item in Menu.Items.OfType<NativeMenuItem>())if(item.Command!=null)item.IsEnabled=item.Command.CanExecute(null);}
    void OnClosed(object? sender,EventArgs e)=>Dispose();
    void Restore() {
        if(disposed)return;
        if(window is MonitorWindow monitor){monitor.RestoreMain();return;}
        window.Show();
        if(window.WindowState==WindowState.Minimized)window.WindowState=WindowState.Normal;
        window.Activate();
    }
    public void Dispose() {
        if(disposed)return;
        disposed=true;
        language.Changed-=Localize;
        Menu.NeedsUpdate-=UpdateMenu;
        window.Closed-=OnClosed;
        window.Closing-=OnClosing;
        if(window is MonitorWindow owner)owner.UserCloseRequested-=UserClose;
        icon.Clicked-=OnClicked;
        icon.IsVisible=false;
        icon.Dispose();
    }
    sealed class ActionCommand(DesktopTray owner,Action action,Func<bool>? enabled=null) : ICommand {
        public event EventHandler? CanExecuteChanged {add{} remove{}}
        public bool CanExecute(object? parameter)=>!owner.disposed&&(enabled?.Invoke()??true);
        public void Execute(object? parameter){if(CanExecute(parameter))action();}
    }
}
