using Avalonia.Controls;
using Avalonia.Layout;

namespace HardwarePulse.Desktop;

public sealed class DesktopStartupPanel : StackPanel,IDisposable {
    readonly UiLanguage language;
    readonly IDesktopStartupManagement source;
    readonly CheckBox enabled=new(){Name="StartWithWindows"};
    readonly Button refresh=new(){Name="RefreshStartup"};
    readonly Button collector=new(){Name="StartCollector"};
    readonly TextBlock status=new(){Name="StartupStatus",TextWrapping=Avalonia.Media.TextWrapping.Wrap};
    readonly bool supported;
    bool busy,disposed,rendering,loaded;
    public DesktopStartupState State {get;private set;}
    public string StatusKey {get;private set;}="Startup management requires the installed Windows version.";
    public Task Operation {get;private set;}=Task.CompletedTask;
    public DesktopStartupPanel(UiLanguage language,bool demo=false,IDesktopStartupManagement? source=null) {
        this.language=language;this.source=source??new WindowsStartupManagement();supported=source!=null||OperatingSystem.IsWindows()&&!demo;
        Name="DesktopStartupPanel";Spacing=10;
        Children.Add(language.Set(enabled,"Start with Windows"));
        var buttons=new StackPanel{HorizontalAlignment=HorizontalAlignment.Left,Spacing=8};
        buttons.Children.Add(language.Set(refresh,"Refresh startup status"));buttons.Children.Add(language.Set(collector,"Start hardware collector"));Children.Add(buttons);Children.Add(status);
        enabled.IsCheckedChanged+=(_,_)=>{if(!rendering)Operation=ChangeAsync(enabled.IsChecked==true);};
        refresh.Click+=(_,_)=>Operation=RefreshAsync();collector.Click+=(_,_)=>Operation=StartCollectorAsync();
        Loaded+=(_,_)=>{if(loaded)return;loaded=true;Operation=RefreshAsync();};
        Render();
    }
    void Render() {
        if(disposed)return;rendering=true;
        try{enabled.IsChecked=State==DesktopStartupState.Enabled;enabled.IsEnabled=collector.IsEnabled=supported&&!busy&&State!=DesktopStartupState.Unavailable;refresh.IsEnabled=supported&&!busy;language.Set(status,StatusKey);}
        finally{rendering=false;}
    }
    public Task RefreshAsync()=>Run(null);
    public Task ChangeAsync(bool value)=>Run(()=>source.SetEnabledAsync(value),value?DesktopStartupState.Enabled:DesktopStartupState.Disabled);
    public Task StartCollectorAsync()=>Run(source.StartCollectorAsync,success:"Hardware collector start requested.");
    async Task Run(Func<Task<DesktopStartupResult>>? action,DesktopStartupState? expected=null,string? success=null) {
        if(disposed||busy||!supported){Render();return;}
        if(action!=null&&State==DesktopStartupState.Unavailable){Render();return;}
        busy=true;StatusKey="Checking startup status…";Render();
        try {
            var result=action==null?DesktopStartupResult.Success:await action();
            if(disposed)return;
            var current=await source.ReadAsync();if(disposed)return;State=current;
            StatusKey=result==DesktopStartupResult.Canceled?"Startup change canceled.":result==DesktopStartupResult.Failed?"Startup action failed; refresh status or reinstall to repair.":
                expected!=null&&current!=expected?"Startup change could not be verified.":current==DesktopStartupState.Unavailable?"Startup management requires the installed Windows version.":
                success??(current==DesktopStartupState.Enabled?"Startup is enabled.":"Startup is disabled.");
        }catch(Exception e) when(e is InvalidOperationException or System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException){if(!disposed){State=DesktopStartupState.Unavailable;StatusKey="Startup action failed; refresh status or reinstall to repair.";}}
        finally{busy=false;Render();}
    }
    public void Dispose(){disposed=true;}
}
