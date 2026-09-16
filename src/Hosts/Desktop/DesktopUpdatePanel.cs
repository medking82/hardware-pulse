using System.Reflection;
using Avalonia.Controls;
using Avalonia.Threading;

namespace HardwarePulse.Desktop;

public sealed class DesktopUpdatePanel : StackPanel,IDisposable {
    readonly UiLanguage language;
    readonly PreviewSettings settings;
    readonly Action changed;
    readonly UpdateCoordinator coordinator;
    readonly bool supported;
    readonly CheckBox automatic=new(){Name="AutoUpdates"},downloadAutomatically=new(){Name="AutoDownload"};
    readonly Button check=new(){Name="CheckUpdates"},download=new(){Name="GetUpdate"},install=new(){Name="InstallUpdate"};
    readonly TextBlock status=new(){Name="UpdateStatus",TextWrapping=Avalonia.Media.TextWrapping.Wrap};
    readonly ProgressBar progress=new(){Name="DownloadProgress",Minimum=0,Maximum=100};
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(500)};
    bool disposed,rendering;
    public Task Operation {get;private set;}=Task.CompletedTask;
    public string StatusKey=>supported?coordinator.Installing?"Installing update…":coordinator.StatusKey??"Check for updates":"Updates require the installed stable Windows version.";
    public DesktopUpdatePanel(UiLanguage language,PreviewSettings settings,Action changed,bool demo=false,IUpdateClient? client=null,Version? version=null) {
        this.language=language;this.settings=settings;this.changed=changed;
        var assembly=typeof(DesktopUpdatePanel).Assembly;
        string informational=assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion??"";
        supported=client!=null||!demo&&WindowsStartupManagement.IsInstalled&&informational.Length>0&&!informational.Contains('-');
        coordinator=new UpdateCoordinator(client??new UpdateClient(),version??assembly.GetName().Version??new Version(0,0,0));
        Name="DesktopUpdatePanel";Spacing=12;Margin=new Avalonia.Thickness(20);
        Children.Add(language.Set(automatic,"Automatically check for updates"));Children.Add(language.Set(downloadAutomatically,"Automatically download verified updates"));
        Children.Add(language.Set(check,"Check for updates"));Children.Add(language.Set(download,"Download update"));Children.Add(language.Set(install,"Install and restart"));Children.Add(progress);Children.Add(status);
        automatic.IsCheckedChanged+=(_,_)=>{if(rendering||disposed)return;settings.AutoUpdates=automatic.IsChecked==true;changed();Poll(DateTime.UtcNow);};
        downloadAutomatically.IsCheckedChanged+=(_,_)=>{if(rendering||disposed)return;settings.AutoDownload=downloadAutomatically.IsChecked==true;if(settings.AutoDownload)settings.AutoUpdates=true;changed();Render();Poll(DateTime.UtcNow);};
        check.Click+=(_,_)=>Operation=CheckAsync();download.Click+=(_,_)=>Operation=DownloadAsync();install.Click+=(_,_)=>Install();
        timer.Tick+=(_,_)=>{Render();if(!coordinator.Busy)timer.Stop();};
        language.Changed+=Render;Render();
    }
    public Task CheckAsync()=>!disposed&&supported?Run(coordinator.CheckAsync(DateTime.UtcNow,settings.AutoDownload)):Task.CompletedTask;
    public Task DownloadAsync()=>!disposed&&supported?Run(coordinator.DownloadAsync()):Task.CompletedTask;
    public void Install(){if(disposed||!supported)return;coordinator.Install();Render();if(coordinator.Installing)timer.Start();}
    public void Poll(DateTime now){if(!disposed&&supported&&settings.AutoUpdates&&coordinator.ShouldCheck(now))Operation=Run(coordinator.CheckAsync(now,settings.AutoDownload));}
    async Task Run(Task operation) {Render();if(coordinator.Busy)timer.Start();await operation;if(disposed)return;Render();if(!coordinator.Busy)timer.Stop();}
    void Render() {
        if(disposed)return;rendering=true;
        try {
            automatic.IsChecked=settings.AutoUpdates;downloadAutomatically.IsChecked=settings.AutoDownload;
            automatic.IsEnabled=downloadAutomatically.IsEnabled=supported;
            check.IsEnabled=supported&&!coordinator.Busy;
            download.IsVisible=supported&&(coordinator.CanDownload||coordinator.Downloading);download.IsEnabled=coordinator.CanDownload;
            install.IsVisible=supported&&coordinator.Ready;install.IsEnabled=coordinator.Ready&&!coordinator.Busy;
            progress.IsVisible=coordinator.Downloading;progress.Value=coordinator.Progress;
            status.Text=language.T(StatusKey)+(coordinator.Downloading?" · "+coordinator.Progress+"%":coordinator.VersionText!=null?" · "+coordinator.VersionText:"");
        }finally{rendering=false;}
    }
    public void Dispose(){if(disposed)return;disposed=true;timer.Stop();language.Changed-=Render;coordinator.Dispose();}
}
