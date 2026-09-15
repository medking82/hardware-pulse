param([string]$AppPath='build/native/app',[ValidateRange(1,36000)][int]$Frames=120)
$ErrorActionPreference='Stop'
$AppPath=[IO.Path]::GetFullPath($AppPath)
function FileHash([string]$Path){
 $algorithm=[Security.Cryptography.SHA256]::Create();$stream=$null
 try{$stream=[IO.File]::OpenRead($Path);return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-','')}
 finally{if($stream){$stream.Dispose()};$algorithm.Dispose()}
}
$appHash=FileHash (Join-Path $AppPath 'HardwarePulse.exe')
$coreHash=FileHash (Join-Path $AppPath 'Pulse.Core.dll')
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml
[void][Reflection.Assembly]::LoadFrom((Join-Path ([IO.Path]::GetFullPath($AppPath)) 'HardwarePulse.exe'))
[AppDomain]::MonitoringIsEnabled=$true
function Settle { [Windows.Threading.Dispatcher]::CurrentDispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::Render) }
$behind=[Windows.Window]::new();$front=[Windows.Window]::new();$capture=$null
try {
 foreach($window in @($behind,$front)){$window.WindowStyle='None';$window.ResizeMode='NoResize';$window.ShowInTaskbar=$false;$window.WindowStartupLocation='Manual';$window.Left=100;$window.Top=30;$window.Width=320;$window.Height=850;$window.Topmost=$true}
 $gradient=[Windows.Media.LinearGradientBrush]::new();$gradient.StartPoint=[Windows.Point]::new(0,0);$gradient.EndPoint=[Windows.Point]::new(1,1)
 foreach($stop in @(@('Black',0),@('White',.4),@('Gray',.7),@('Black',1))){$gradient.GradientStops.Add([Windows.Media.GradientStop]::new([Windows.Media.Colors]::($stop[0]),$stop[1]))}
 $behind.Background=$gradient;$behind.Show();$front.AllowsTransparency=$true;$front.Background=[Windows.Media.Brushes]::Transparent;$front.Show();Settle
 $capture=[HardwarePulse.LocalContrast]::new($front);if(-not $capture.Enable()){throw 'Capture exclusion unavailable'}
 Start-Sleep -Milliseconds 300;Settle;$bounds=$capture.Bounds()
 foreach($i in 1..8){$null=$capture.Capture($bounds,[Windows.Media.Colors]::Transparent,10)}
 $process=[Diagnostics.Process]::GetCurrentProcess();$cpu=$process.TotalProcessorTime.TotalSeconds
 $allocated=[AppDomain]::CurrentDomain.MonitoringTotalAllocatedMemorySize
 $watch=[Diagnostics.Stopwatch]::StartNew();$active=0.0;$ws=0.0;$private=0.0;$mask=$null
 foreach($i in 1..$Frames){
  $start=$watch.Elapsed.TotalMilliseconds;$mask=$capture.Capture($bounds,[Windows.Media.Colors]::Transparent,10)
  if($null -eq $mask){throw 'Missing capture'}
  $active+=$watch.Elapsed.TotalMilliseconds-$start;$process.Refresh();$ws+=$process.WorkingSet64;$private+=$process.PrivateMemorySize64
  $remaining=100-($watch.Elapsed.TotalMilliseconds-$start);if($remaining -gt 0){Start-Sleep -Milliseconds ([int]$remaining)}
 }
 $elapsed=$watch.Elapsed.TotalSeconds;$process.Refresh();$used=$process.TotalProcessorTime.TotalSeconds-$cpu
 [pscustomobject]@{
  Scope='Capture/analysis harness; includes PowerShell/WPF host, not complete Desktop'
  AppSha256=$appHash;CoreSha256=$coreHash;LogicalProcessors=[Environment]::ProcessorCount;WarmupFrames=8;TargetIntervalMs=100
  Version=[HardwarePulse.LocalContrast].Assembly.GetName().Version.ToString();Frames=$Frames;Seconds=[Math]::Round($elapsed,3)
  SourceWidth=$bounds.Width;SourceHeight=$bounds.Height;MaskWidth=$mask.PixelWidth;MaskHeight=$mask.PixelHeight
  CaptureMeanMs=[Math]::Round($active/$Frames,3);CpuMsPerFrame=[Math]::Round($used*1000/$Frames,3)
  CpuWholeMachinePercent=[Math]::Round($used/$elapsed/[Environment]::ProcessorCount*100,4)
  AllocatedBytesPerFrame=[Math]::Round(([AppDomain]::CurrentDomain.MonitoringTotalAllocatedMemorySize-$allocated)/$Frames)
  WorkingSetMeanMiB=[Math]::Round($ws/$Frames/1MB,2);PrivateMeanMiB=[Math]::Round($private/$Frames/1MB,2)
 }|ConvertTo-Json
}finally{if($capture){$capture.Dispose()};$front.Close();$behind.Close()}
