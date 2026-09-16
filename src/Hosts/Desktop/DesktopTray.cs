using Avalonia.Controls;
using Avalonia.Platform;
using System.Windows.Input;

namespace HardwarePulse.Desktop;

// The existing window owns the lifetime. Tray support is optional; never hide on close.
public sealed class DesktopTray : IDisposable {
    readonly Window window;
    readonly TrayIcon icon;
    readonly UiLanguage language;
    bool disposed;
    public NativeMenu Menu { get; }=new();
    public DesktopTray(Window window) {
        this.window=window;
        language=(window as MonitorWindow)?.Language??new UiLanguage();
        using var stream=AssetLoader.Open(new Uri("avares://Pulse.Desktop/Assets/pulse.ico"));
        var artwork=new WindowIcon(stream);
        window.Icon=artwork;
        Menu.Items.Add(new NativeMenuItem("Open Pulse") {Command=new ActionCommand(this,Restore)});
        Menu.Items.Add(new NativeMenuItem("Quit Pulse") {Command=new ActionCommand(this,window.Close)});
        language.Changed+=Localize;Localize();
        icon=new TrayIcon {Icon=artwork,ToolTipText="Pulse",Menu=Menu,IsVisible=true};
        icon.Clicked+=OnClicked;
        window.Closed+=OnClosed;
    }
    void OnClicked(object? sender,EventArgs e)=>Restore();
    void Localize(){((NativeMenuItem)Menu.Items[0]).Header=language.T("Open Pulse");((NativeMenuItem)Menu.Items[1]).Header=language.T("Quit Pulse");}
    void OnClosed(object? sender,EventArgs e)=>Dispose();
    void Restore() {
        if(disposed)return;
        window.Show();
        if(window.WindowState==WindowState.Minimized)window.WindowState=WindowState.Normal;
        window.Activate();
    }
    public void Dispose() {
        if(disposed)return;
        disposed=true;
        language.Changed-=Localize;
        window.Closed-=OnClosed;
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
