param([string]$AppPath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml,System.Windows.Forms
$app=[IO.Path]::GetFullPath($AppPath)
[void][Reflection.Assembly]::LoadFrom((Join-Path $app 'HardwarePulse.exe'))
$compiler=[CodeDom.Compiler.CompilerParameters]::new()
[void]$compiler.ReferencedAssemblies.Add((Join-Path $app 'HardwarePulse.exe'))
Add-Type -CompilerParameters $compiler -TypeDefinition @'
using System;
using System.Threading.Tasks;
using HardwarePulse;
public sealed class HomeUpdateFixture : IUpdateClient {
    public TaskCompletionSource<string> Check=new TaskCompletionSource<string>(), Download=new TaskCompletionSource<string>();
    public int Checks,Downloads,Installs;
    public bool Ready {get;set;} public bool Installing {get;set;} public int Progress {get;set;}
    public Task<string> CheckAsync(){Checks++;return Check.Task;}
    public Task<string> DownloadAsync(string url,string tag,string digest,long size){Downloads++;return Download.Task;}
    public void Install(){Installs++;Installing=true;}
    public void CancelDownload(){}
}
'@
$state=Join-Path (Split-Path $PSScriptRoot) ('vendor/home-update-'+[Guid]::NewGuid().ToString('N'))
$paths=[HardwarePulse.PulsePaths]::new($app,$state,(Join-Path $state 'runtime'))
$shell=[HardwarePulse.Shell]::new($paths,$true)
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
function Assert($condition,$message){if(-not $condition){throw $message}}
function Pump {$shell.Window.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::Background)}
function Render {$shell.GetType().GetMethod('RenderUpdate',$flags).Invoke($shell,@());Pump}
function ClickHome {$updateButton.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent));Pump}
try {
    $field=$shell.GetType().GetField('updater',$flags);$field.GetValue($shell).Dispose()
    $fake=[HomeUpdateFixture]::new();$updater=[HardwarePulse.UpdateCoordinator]::new($fake,[Version]'0.6.0')
    $field.SetValue($shell,$updater)
    $updateButton=$shell.Window.FindName('HomeUpdate');$gear=$shell.Window.FindName('Settings')
    $language=$shell.Window.FindName('LanguagePicker');foreach($item in $language.Items){if($item.Tag -eq 'en'){$language.SelectedItem=$item}}
    Render;Assert ($updateButton.Visibility -eq 'Collapsed') 'Idle update button is visible'
    $pending=$updater.CheckAsync([DateTime]::Now,$false);Render
    Assert (-not $updateButton.IsEnabled -and $updateButton.Visibility -eq 'Visible') 'Checking status missing or permits duplicate action'
    $fake.Check.SetException([Exception]::new('Fixture offline'));Pump;Render
    Assert ($updateButton.Content -eq 'Retry update' -and $updateButton.IsEnabled) 'Failure has no retry action'
    $fake.Check=[Threading.Tasks.TaskCompletionSource[string]]::new();ClickHome
    Assert ($fake.Checks -eq 2) 'Home retry did not use existing updater'
    $fake.Check.SetResult('{"tag_name":"v0.6.1","assets":[{"name":"HardwarePulse-Setup.exe","browser_download_url":"https://github.com/medking82/hardware-pulse/releases/download/v0.6.1/HardwarePulse-Setup.exe","digest":"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","size":100}]}');Pump;Render
    Assert ($updateButton.Content -eq 'Update to 0.6.1') 'Home does not identify available version'
    ClickHome;Assert ($fake.Downloads -eq 1) 'Home did not start download'
    $fake.Progress=45;Render;Assert ($updateButton.Content -like '*45%' -and -not $updateButton.IsEnabled) 'Download progress/duplicate guard missing'
    $fake.Ready=$true;$fake.Download.SetResult('fixture');Pump;Render
    Assert ($updateButton.Content -eq 'Install and restart' -and $updateButton.IsEnabled) 'Ready installer action missing'
    $shell.Show();$shell.Window.Width=240;$shell.Window.UpdateLayout();Pump
    Assert ($updateButton.ActualWidth -gt 0 -and $updateButton.TranslatePoint([Windows.Point]::new($updateButton.ActualWidth,0),$gear.Parent).X -le $gear.TranslatePoint([Windows.Point]::new(0,0),$gear.Parent).X) 'Home update overlaps Settings at minimum width'
    foreach($item in $language.Items){if($item.Tag -eq 'zh-CN'){$language.SelectedItem=$item}};Pump
    Assert ($updateButton.Content -eq '安装并重启') 'Home update ignores language changes'
    ClickHome;Assert ($fake.Installs -eq 1) 'Home did not delegate install'
    ClickHome;Assert ($fake.Installs -eq 1) 'Home started duplicate installer'
    'PASS Home update: hidden idle, retry, version, progress, install, localization and narrow footer; no network or installer launched'
} finally {$shell.Exit()}
