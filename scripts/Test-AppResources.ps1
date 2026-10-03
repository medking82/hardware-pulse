param([string]$AppPath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml,System.Windows.Forms
$app=[IO.Path]::GetFullPath($AppPath)
[void][Reflection.Assembly]::LoadFrom((Join-Path $app 'HardwarePulse.exe'))
$compiler=[CodeDom.Compiler.CompilerParameters]::new()
foreach($assembly in @('HardwarePulse.exe','Pulse.Adapters.Windows.dll','Pulse.Core.dll')){
    [void]$compiler.ReferencedAssemblies.Add((Join-Path $app $assembly))
}
Add-Type -CompilerParameters $compiler -TypeDefinition @'
using System;
using System.Threading;
using HardwarePulse;
public static class AppResourceViewFixture {
    public static int Calls;
    public static readonly ManualResetEventSlim Gate=new ManualResetEventSlim(true);
    public static AppResourceSnapshot Read(){
        Interlocked.Increment(ref Calls);
        if(!Gate.Wait(3000))throw new Exception("Fixture snapshot gate timed out");
        return new AppResourceSnapshot {
            Ram=new RamUsage {totalGb=16,usedGb=8},GpuAvailable=true,
            Processes=new[]{
                new AppResourceProcess {Name="Background fixture",Pid=101,RamBytes=2147483648L,GpuBytes=0,CanClose=false},
                new AppResourceProcess {Name="Window fixture",Pid=102,RamBytes=1073741824L,GpuBytes=536870912,CanClose=true},
                new AppResourceProcess {Name="Unavailable fixture",Pid=103,RamBytes=0,GpuBytes=null,CanClose=false}
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
function Capture($window,$name){
    $window.UpdateLayout();Pump
    $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new([int]$window.ActualWidth,[int]$window.ActualHeight,96,96,[Windows.Media.PixelFormats]::Pbgra32);$bitmap.Render($window)
    $png=[Windows.Media.Imaging.PngBitmapEncoder]::new();$png.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream=[IO.File]::Create((Join-Path $state $name));try{$png.Save($stream)}finally{$stream.Dispose()}
}
try {
    $reader=[Delegate]::CreateDelegate([Func[HardwarePulse.AppResourceSnapshot]],[AppResourceViewFixture].GetMethod('Read'))
    $shell.GetType().GetField('readAppResources',$flags).SetValue($shell,$reader)
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
    WaitUntil {$refresh.IsEnabled -and $list.Items.Count -eq 3}
    Assert ($list.Items[0].Process.Pid -eq 101) 'RAM entry is not sorted by RAM'
    Assert (-not $close.IsEnabled) 'No selection must not enable close'
    $list.SelectedIndex=0;Pump;Assert (-not $close.IsEnabled) 'Background process must not enable close'
    $list.SelectedIndex=1;Pump;Assert ($close.IsEnabled) 'Closeable fixture selection not enabled'
    $same=OpenResources $true
    Assert ([object]::ReferenceEquals($same,$window)) 'Both cards must reuse one resource window'
    Assert ($sort.SelectedIndex -eq 1 -and $list.Items[0].Process.Pid -eq 102) 'GPU entry did not sort existing snapshot'
    Assert ([AppResourceViewFixture]::Calls -eq 1) 'Sorting must not sample processes again'
    Assert ($list.Items[2].Gpu -eq '—') 'Unavailable GPU estimate must not become zero'
    foreach($languageCode in @('en','zh-CN','zh-TW')){
        $window.Close();Pump
        $picker=$shell.Window.FindName('LanguagePicker');foreach($item in $picker.Items){if($item.Tag -eq $languageCode){$picker.SelectedItem=$item}}
        $window=OpenResources $false
        $list=$window.FindName('ResourceList');$refresh=$window.FindName('ResourceRefresh')
        WaitUntil {$refresh.IsEnabled -and $list.Items.Count -eq 3}
        foreach($width in @(440,720)){
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
    WaitUntil {$refresh.IsEnabled -and $list.Items.Count -eq 3}
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
    WaitUntil {$refresh.IsEnabled -and $list.Items.Count -eq 3}
    Assert ([AppResourceViewFixture]::Calls -eq $before+1) 'Shared read result started another snapshot'
    $window.Close();Pump
    WaitUntil {$null -eq $shell.GetType().GetField('resourceReadTask',$flags).GetValue($shell)}
    'PASS resource view: synthetic reads only; no close request sent; RAM/GPU entry, selection, no sorting resample, unavailable GPU, RAM validation, localized narrow layout and teardown.'
    'Visual artifacts: '+$state
} finally {
    [AppResourceViewFixture]::Gate.Set();$shell.Dispose()
}
