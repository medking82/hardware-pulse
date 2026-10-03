param([string]$AppPath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml,System.Windows.Forms
$app=[IO.Path]::GetFullPath($AppPath)
[void][Reflection.Assembly]::LoadFrom((Join-Path $app 'HardwarePulse.exe'))
$compiler=[CodeDom.Compiler.CompilerParameters]::new()
[void]$compiler.ReferencedAssemblies.Add('System.dll')
[void]$compiler.ReferencedAssemblies.Add('System.Xaml.dll')
foreach($assembly in @('HardwarePulse.exe','Pulse.Adapters.Windows.dll','Pulse.Core.dll')){
    [void]$compiler.ReferencedAssemblies.Add((Join-Path $app $assembly))
}
[void]$compiler.ReferencedAssemblies.Add([Windows.Window].Assembly.Location)
[void]$compiler.ReferencedAssemblies.Add([Windows.UIElement].Assembly.Location)
[void]$compiler.ReferencedAssemblies.Add([Windows.DependencyObject].Assembly.Location)
Add-Type -CompilerParameters $compiler -TypeDefinition @'
using System;
using System.Threading;
using System.Collections.Generic;
using System.Windows;
using HardwarePulse;
public static class AppResourceViewFixture {
    public static int Calls;
    public static readonly ManualResetEventSlim Gate=new ManualResetEventSlim(true);
    public static readonly ManualResetEventSlim CloseGate=new ManualResetEventSlim(true);
    public static bool Confirm=true;
    public static int Confirmations,Closes;
    public static string Prompt;
    public static readonly List<int> ClosedPids=new List<int>();
    public static bool ConfirmClose(Window owner,string prompt,string title){Confirmations++;Prompt=prompt;return Confirm;}
    public static AppCloseResult Close(AppResourceProcess process){
        Interlocked.Increment(ref Closes);if(!CloseGate.Wait(3000))throw new Exception("Fixture close gate timed out");
        ClosedPids.Add(process.Pid);return process.Pid==102?AppCloseResult.Requested:AppCloseResult.IdentityChanged;
    }
    public static AppResourceSnapshot Read(){
        Interlocked.Increment(ref Calls);
        if(!Gate.Wait(3000))throw new Exception("Fixture snapshot gate timed out");
        return new AppResourceSnapshot {
            Ram=new RamUsage {totalGb=16,usedGb=8},GpuAvailable=true,
            Processes=new[]{
                new AppResourceProcess {Name="Background fixture",Pid=101,StartedUtcTicks=101,RamBytes=2147483648L,GpuBytes=0,CanClose=false},
                new AppResourceProcess {Name="Window fixture",Pid=102,StartedUtcTicks=102,RamBytes=1073741824L,GpuBytes=536870912,CanClose=true},
                new AppResourceProcess {Name="Unavailable fixture",Pid=103,StartedUtcTicks=103,RamBytes=0,GpuBytes=null,CanClose=false},
                new AppResourceProcess {Name="Second window fixture",Pid=104,StartedUtcTicks=104,RamBytes=805306368L,GpuBytes=134217728,CanClose=true}
            }
        };
    }
}
'@
$root=Split-Path $PSScriptRoot
$state=Join-Path $root ('vendor/app-resources-'+[Guid]::NewGuid().ToString('N'))
$paths=[HardwarePulse.PulsePaths]::new($app,$state,(Join-Path $state 'runtime'))
$shell=[HardwarePulse.Shell]::new($paths,$true)
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
function Assert($condition,$message){if(-not $condition){throw $message}}
function Pump {[void]$shell.Window.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::Background)}
function WaitUntil([scriptblock]$condition){
    $wait=[Diagnostics.Stopwatch]::StartNew()
    while($wait.ElapsedMilliseconds -lt 4000){Pump;if(& $condition){return};Start-Sleep -Milliseconds 10}
    throw 'Timed out waiting for resource fixture'
}
function OpenResources($gpu){
    [void]$shell.GetType().GetMethod('ShowAppResources',$flags).Invoke($shell,@([bool]$gpu));Pump
    $resourceView=$shell.GetType().GetField('resourcesWindow',$flags).GetValue($shell)
    Assert ($null -ne $resourceView) 'Resource window did not remain open'
    return ,$resourceView
}
function Click($control){$control.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Primitives.ButtonBase]::ClickEvent));Pump}
function FindCheckbox($node){
    if($node -is [Windows.Controls.CheckBox]){return ,$node}
    for($i=0;$i -lt [Windows.Media.VisualTreeHelper]::GetChildrenCount($node);$i++){
        $found=FindCheckbox ([Windows.Media.VisualTreeHelper]::GetChild($node,$i));if($null -ne $found){return ,$found}
    }
}
function Capture($window,$name){
    $window.UpdateLayout();Pump
    $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new([int]$window.ActualWidth,[int]$window.ActualHeight,96,96,[Windows.Media.PixelFormats]::Pbgra32);$bitmap.Render($window)
    $png=[Windows.Media.Imaging.PngBitmapEncoder]::new();$png.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream=[IO.File]::Create((Join-Path $state $name));try{$png.Save($stream)}finally{$stream.Dispose()}
}
try {
    $reader=[Delegate]::CreateDelegate([Func[HardwarePulse.AppResourceSnapshot]],[AppResourceViewFixture].GetMethod('Read'))
    $shell.GetType().GetField('readAppResources',$flags).SetValue($shell,$reader)
    $closer=[Delegate]::CreateDelegate([Func[HardwarePulse.AppResourceProcess,HardwarePulse.AppCloseResult]],[AppResourceViewFixture].GetMethod('Close'))
    $confirmer=[Delegate]::CreateDelegate([Func[Windows.Window,string,string,bool]],[AppResourceViewFixture].GetMethod('ConfirmClose'))
    $shell.GetType().GetField('closeResourceApp',$flags).SetValue($shell,$closer)
    $shell.GetType().GetField('confirmResourceApps',$flags).SetValue($shell,$confirmer)
    $headroom=$shell.GetType().GetMethod('TryResourceHeadroom',[Reflection.BindingFlags]'Static,NonPublic')
    foreach($reading in @($null,[HardwarePulse.RamUsage]::new(),[HardwarePulse.RamUsage]@{totalGb=0;usedGb=0},[HardwarePulse.RamUsage]@{totalGb=16;usedGb=17},[HardwarePulse.RamUsage]@{totalGb=[double]::NaN;usedGb=1},[HardwarePulse.RamUsage]@{totalGb=16;usedGb=[double]::PositiveInfinity})){
        $arguments=[object[]]@($reading,[double]0)
        Assert (-not $headroom.Invoke($null,$arguments)) 'Invalid RAM must not produce headroom advice'
    }
    $arguments=[object[]]@([HardwarePulse.RamUsage]@{totalGb=16;usedGb=8},[double]0)
    Assert ($headroom.Invoke($null,$arguments) -and $arguments[1] -eq 8) 'Valid RAM headroom calculation'
    $shell.Show();Pump
    $window=OpenResources $false
    $list=$window.FindName('ResourceList');$sort=$window.FindName('ResourceSort');$refresh=$window.FindName('ResourceRefresh');$close=$window.FindName('ResourceClose')
    WaitUntil {$refresh.IsEnabled -and $list.Items.Count -eq 4}
    Assert ($list.Items[0].Process.Pid -eq 101) 'RAM entry is not sorted by RAM'
    Assert (-not $close.IsEnabled) 'No selection must not enable close'
    $list.UpdateLayout();Pump
    $firstBox=FindCheckbox ($list.ItemContainerGenerator.ContainerFromIndex(0))
    Assert ($null -ne $firstBox -and -not $firstBox.IsEnabled -and $firstBox.ActualWidth -le 28) 'Unavailable process checkbox must be disabled and fit its column'
    $windowBox=FindCheckbox ($list.ItemContainerGenerator.ContainerFromIndex(1));$windowBox.IsChecked=$true;Pump
    Assert ($list.SelectedItems.Count -eq 1 -and $list.SelectedItem.Process.Pid -eq 102) 'Row checkbox did not select the intended process'
    $list.SelectedItems.Clear();Pump
    $list.SelectedIndex=0;Pump;Assert (-not $close.IsEnabled) 'Background process must not enable close'
    $list.SelectedIndex=1;Pump;Assert ($close.IsEnabled) 'Closeable fixture selection not enabled'
    $same=OpenResources $true
    Assert ([object]::ReferenceEquals($same,$window)) 'Both cards must reuse one resource window'
    Assert ($sort.SelectedIndex -eq 1 -and $list.Items[0].Process.Pid -eq 102) 'GPU entry did not sort existing snapshot'
    Assert ([AppResourceViewFixture]::Calls -eq 1) 'Sorting must not sample processes again'
    Assert ($list.Items[3].Gpu -eq '—') 'Unavailable GPU estimate must not become zero'
    $direction=$window.FindName('ResourceDirection');Click $direction
    Assert ($list.Items[0].Process.Pid -eq 101 -and $list.Items[3].Process.Pid -eq 103) 'GPU ascending must be numeric with unavailable last'
    $header=$list.View.Columns[3].Header;Click $header
    Assert ($list.Items[0].Process.Pid -eq 102) 'Column header must toggle direction'
    $header=$list.View.Columns[1].Header;Click $header
    Assert ($sort.SelectedIndex -eq 2 -and $list.Items[0].Process.Pid -eq 101) 'App header must sort names alphabetically'
    Assert ([AppResourceViewFixture]::Calls -eq 1) 'Headers must not resample process usage'
    $sort.SelectedIndex=0;Pump
    $suggestions=$window.FindName('ResourceSuggestions');Assert ($suggestions.IsEnabled) 'High-usage close suggestions missing'
    Assert ($list.SelectedItems.Count -eq 1) 'Sorting must preserve explicit eligible selection'
    $list.SelectedItems.Clear();Pump
    Assert (-not $close.IsEnabled) 'Suggestions must not select apps automatically'
    [AppResourceViewFixture]::Confirm=$false;Click $suggestions
    Assert ([AppResourceViewFixture]::Closes -eq 0 -and $refresh.IsEnabled -and $list.IsEnabled) 'Canceled confirmation must dispatch nothing and restore controls'
    Assert ($list.SelectedItems.Count -eq 0 -and [AppResourceViewFixture]::Prompt -match 'Window fixture' -and [AppResourceViewFixture]::Prompt -match 'Second window fixture') 'Suggested batch must confirm candidate identities without changing manual selection'
    [AppResourceViewFixture]::Confirm=$true;[AppResourceViewFixture]::CloseGate.Reset();Click $suggestions
    WaitUntil {[AppResourceViewFixture]::Closes -eq 1}
    Assert (-not $close.IsEnabled -and -not $refresh.IsEnabled -and -not $list.IsEnabled) 'Batch must disable duplicate and selection actions'
    Click $suggestions;Assert ([AppResourceViewFixture]::Closes -eq 1) 'Busy batch permits duplicate close'
    [AppResourceViewFixture]::CloseGate.Set();WaitUntil {$refresh.IsEnabled}
    Assert ([AppResourceViewFixture]::ClosedPids.Count -eq 2 -and [AppResourceViewFixture]::ClosedPids[0] -eq 102 -and [AppResourceViewFixture]::ClosedPids[1] -eq 104) 'Batch closed unselected process or changed deterministic order'
    Assert ([AppResourceViewFixture]::Prompt -match 'Window fixture' -and [AppResourceViewFixture]::Prompt -match 'Second window fixture') 'Batch confirmation must identify every selected app'
    Assert ($list.SelectedItems.Count -eq 0 -and -not $close.IsEnabled) 'Finished batch retained stale close eligibility'
    $follow=$window.FindName('ResourceFollow');$opacity=$window.FindName('ResourceOpacity')
    Assert ($follow.IsChecked -and -not $opacity.IsEnabled) 'Default resource appearance must follow App'
    $follow.IsChecked=$false;Click $follow;$opacity.Value=35;Pump
    Assert ($opacity.IsEnabled -and $window.FindName('ResourceOpacityValue').Text -eq '35%') 'Independent opacity adjustment missing'
    $alpha=([Windows.Media.SolidColorBrush]$window.Background).Color.A
    Assert ($alpha -eq [Math]::Round(255*0.35) -and $window.Opacity -eq 1) 'Opacity must affect background only'
    $follow.IsChecked=$true;Click $follow;$shell.Window.FindName('OpacitySlider').Value=65;Pump
    Assert ($window.FindName('ResourceOpacityValue').Text -like '65%*') 'App opacity changes must update open usage window'
    Click $refresh;WaitUntil {$refresh.IsEnabled}
    foreach($row in $list.Items){if($row.Process.Pid -in @(102,104)){$list.SelectedItems.Add($row)}};Pump
    [AppResourceViewFixture]::Confirm=$false;Click $close
    Assert ([AppResourceViewFixture]::Closes -eq 2) 'Manual selected batch cancellation dispatched a request'
    [AppResourceViewFixture]::Confirm=$true;[AppResourceViewFixture]::CloseGate.Reset();Click $close
    WaitUntil {[AppResourceViewFixture]::Closes -eq 3}
    $window.Close();Pump;[AppResourceViewFixture]::CloseGate.Set()
    WaitUntil {[AppResourceViewFixture]::ClosedPids.Count -eq 3};Pump
    Assert ([AppResourceViewFixture]::Closes -eq 3) 'Closing the resource window must stop undispatched batch requests'
    foreach($languageCode in @('en','zh-CN','zh-TW')){
        if($window.IsVisible){$window.Close();Pump}
        $picker=$shell.Window.FindName('LanguagePicker');foreach($item in $picker.Items){if($item.Tag -eq $languageCode){$picker.SelectedItem=$item}}
        $window=OpenResources $false
        $list=$window.FindName('ResourceList');$refresh=$window.FindName('ResourceRefresh')
        WaitUntil {$refresh.IsEnabled -and $list.Items.Count -eq 4}
        foreach($width in @(480,760)){
            $window.Width=$width;$window.Height=$window.MinHeight;$window.UpdateLayout();Pump
            function FindScrollViewer($node){
                if($node -is [Windows.Controls.ScrollViewer]){return $node}
                for($i=0;$i -lt [Windows.Media.VisualTreeHelper]::GetChildrenCount($node);$i++){
                    $found=FindScrollViewer ([Windows.Media.VisualTreeHelper]::GetChild($node,$i));if($found){return $found}
                }
            }
            $viewer=FindScrollViewer $list
            Assert ($viewer -and $viewer.ScrollableWidth -le 1) 'Usage columns overflow the viewport'
            Assert ($list.ActualHeight -ge 80) 'Usage list has no complete row at minimum height'
            $columnWidth=($list.View.Columns | Measure-Object -Property Width -Sum).Sum
            Assert ($columnWidth -le $list.ActualWidth-20) 'Usage columns are clipped at narrow width'
            Capture $window ('resources-'+$languageCode+'-'+$width+'.png')
        }
    }
    $window.Close();Pump
    $settings=$shell.GetType().GetField('settings',$flags).GetValue($shell);$settings.Data['background']='#FFFFFF'
    [void]$shell.GetType().GetMethod('ApplyMaterial',$flags).Invoke($shell,@());Pump
    $window=OpenResources $false;$refresh=$window.FindName('ResourceRefresh');$list=$window.FindName('ResourceList')
    WaitUntil {$refresh.IsEnabled -and $list.Items.Count -eq 4}
    Capture $window 'resources-light.png'
    [AppResourceViewFixture]::Gate.Reset();$before=[AppResourceViewFixture]::Calls
    Click $refresh
    WaitUntil {[AppResourceViewFixture]::Calls -gt $before}
    Assert (-not $refresh.IsEnabled) 'Refresh must be disabled during a snapshot'
    $window.Close();Pump
    Assert ($null -eq $shell.GetType().GetField('resourcesWindow',$flags).GetValue($shell)) 'Closed resource window retained'
    Assert ($null -eq $shell.GetType().GetField('resourcesSort',$flags).GetValue($shell)) 'Closed sort selector retained'
    $window=OpenResources $true
    Assert ([AppResourceViewFixture]::Calls -eq $before+1) 'Reopening started a parallel process snapshot'
    $refresh=$window.FindName('ResourceRefresh');$list=$window.FindName('ResourceList')
    Assert (-not $refresh.IsEnabled) 'Reopened window must wait for the shared read'
    [AppResourceViewFixture]::Gate.Set()
    WaitUntil {$refresh.IsEnabled -and $list.Items.Count -eq 4}
    Assert ([AppResourceViewFixture]::Calls -eq $before+1) 'Shared read result started another snapshot'
    $window.Close();Pump
    WaitUntil {$null -eq $shell.GetType().GetField('resourceReadTask',$flags).GetValue($shell)}
    'PASS resource view: synthetic reads/close callbacks only; header numeric/name sorting, no resampling, explicit suggestions, batch cancel/duplicate/stale results, background opacity/follow App, three languages, narrow layout and pending read reuse.'
    'Visual artifacts: '+$state
} finally {
    [AppResourceViewFixture]::Gate.Set();[AppResourceViewFixture]::CloseGate.Set();$shell.Dispose()
}
