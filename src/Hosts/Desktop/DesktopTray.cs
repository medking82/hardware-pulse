using Avalonia.Controls;
using Avalonia.Platform;
using System.Windows.Input;

namespace HardwarePulse.Desktop;

// The existing window owns sampling. Hide only when the host enables a reachable tray.
public sealed class DesktopTray : IDisposable {
    readonly Window window;
    readonly TrayIcon icon;
    readonly UiLanguage language;
    bool disposed;
    readonly bool closeToTray;
    readonly Func<bool> trayAvailable;
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
        if(window is MonitorWindow monitor)
            Menu.Items.Insert(1,new NativeMenuItem("Open floating monitor") {Command=new ActionCommand(this,monitor.OpenFloatingMonitor)});
        language.Changed+=Localize;Localize();
        icon=new TrayIcon {Icon=artwork,ToolTipText="Pulse",Menu=Menu,IsVisible=true};
        this.trayAvailable=trayAvailable??(()=>icon.NativeMenuExporter!=null);
        icon.Clicked+=OnClicked;
        window.Closed+=OnClosed;
        window.Closing+=OnClosing;
    }
    void OnClosing(object? sender,WindowClosingEventArgs e) {
        if(disposed||!closeToTray||!trayAvailable()||e.Cancel||e.IsProgrammatic||e.CloseReason!=WindowCloseReason.WindowClosing)return;
        // Do not intercept application/OS shutdown or programmatic test/measurement close.
        e.Cancel=true;window.Hide();
    }
    void OnClicked(object? sender,EventArgs e)=>Restore();
    void Localize(){
        ((NativeMenuItem)Menu.Items[0]).Header=language.T("Open Pulse");
        if(window is MonitorWindow)((NativeMenuItem)Menu.Items[1]).Header=language.T("Open floating monitor");
        ((NativeMenuItem)Menu.Items[Menu.Items.Count-1]).Header=language.T("Quit Pulse");
    }
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
        window.Closed-=OnClosed;
        window.Closing-=OnClosing;
        icon.Clicked-=OnClicked;
        icon.IsVisible=false;
        icon.Dispose();
    }
    sealed class ActionCommand(DesktopTray owner,Action action) : ICommand {
        public event EventHandler? CanExecuteChanged {add{} remove{}}
        public bool CanExecute(object? parameter)=>!owner.disposed;
        public void Execute(object? parameter){if(CanExecute(parameter))action();}
    }
}
