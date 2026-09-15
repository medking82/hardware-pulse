param([string]$AppPath,[string]$OutputPath)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
if(-not $AppPath){$AppPath=Join-Path $root 'build/native/app'}
if(-not $OutputPath){$OutputPath=Join-Path $root 'docs/showcase/pulse-hero.png'}
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml,System.Windows.Forms
[void][Reflection.Assembly]::LoadFrom((Join-Path $AppPath 'HardwarePulse.exe'))
$version=[HardwarePulse.Shell].Assembly.GetName().Version.ToString(3)
$state=Join-Path $root ('vendor/hero-'+[Guid]::NewGuid().ToString('N'))
$paths=[HardwarePulse.PulsePaths]::new($AppPath,$state,(Join-Path $state 'runtime'))
$demo=[HardwarePulse.RawSnapshot]::new();$demo.schema=2;$demo.pid=1;$demo.sequence=1;$demo.time=[DateTimeOffset]::Now.ToString('o');$demo.memoryName='32 GB DDR5 · Demo memory';$demo.boardName='Demo motherboard'
$demo.ramUsage=[HardwarePulse.RamUsage]::new();$demo.ramUsage.usedGb=12.4;$demo.ramUsage.totalGb=32
$values=@{cpu=52.4;cpuLoad=18;vcore=1.08;gpu=44.6;gpuLoad=12;vram=51;gpuVolt=0.85;cpuFan=820;gpuFan=0;gpuFan2=0;ramA=38.5;ramB=38.2;system=40;bottom=900;top=1050;diskC=36;diskD=37}
$sensors=New-Object 'Collections.Generic.List[HardwarePulse.Sensor]'
$names=@{cpu='CPU Package';cpuLoad='CPU Total';vcore='Vcore';gpu='GPU Core';gpuLoad='GPU Core';vram='GPU Memory Junction';gpuVolt='GPU Core Voltage';cpuFan='CPU Fan';gpuFan='GPU Fan 1';gpuFan2='GPU Fan 2';ramA='DIMM #1';ramB='DIMM #3';system='System';bottom='Intake';top='Exhaust';diskC='Composite Temperature';diskD='Composite Temperature'}
foreach($entry in $values.GetEnumerator()){
 $key=$entry.Key;$spec=[HardwarePulse.SensorProfile]::Specs[$key];$sensor=[HardwarePulse.Sensor]::new();$sensor.id=$spec.Id;$sensor.type=$spec.Type;$sensor.value=$entry.Value;$sensor.name=$names[$key];$sensor.hardware='Demo hardware';$sensor.hardwareId='/board';$sensor.hardwareType='SuperIO'
 if($key -in @('cpu','cpuLoad')){$sensor.hardwareId='/cpu';$sensor.hardwareType='Cpu'}
 if($key -in @('gpu','gpuLoad','vram','gpuVolt','gpuFan','gpuFan2')){$sensor.hardwareId='/gpu';$sensor.hardwareType='GpuNvidia'}
 if($key -in @('ramA','ramB')){$sensor.hardwareId=$spec.Id;$sensor.hardwareType='Memory'}
 if($key -in @('diskC','diskD')){$sensor.hardwareId=$spec.Id;$sensor.hardwareType='Storage'}
 $sensors.Add($sensor)
}
foreach($row in @(@('GPU Memory Used',3072),@('GPU Memory Total',12288))){$sensor=[HardwarePulse.Sensor]::new();$sensor.id='/gpu/'+$row[0];$sensor.hardwareId='/gpu';$sensor.hardwareType='GpuNvidia';$sensor.hardware='Demo graphics';$sensor.name=$row[0];$sensor.type='SmallData';$sensor.value=$row[1];$sensors.Add($sensor)}
foreach($row in @(@('Download Speed',12400000),@('Upload Speed',820000))){$s=[HardwarePulse.Sensor]::new();$s.id='/nic/demo/'+$row[0];$s.hardwareId='/nic/demo';$s.hardwareType='Network';$s.hardware='LAN';$s.name=$row[0];$s.type='Throughput';$s.value=$row[1];$sensors.Add($s)}
$demo.sensors=$sensors.ToArray();$links=@();foreach($row in @(@('Ethernet',2500000000),@('Wi-Fi',1201000000))){$l=[HardwarePulse.NetworkLink]::new();$l.hardwareId='/nic/'+$row[0];$l.connectionType=$row[0];$l.connected=$true;$l.physical=$true;$l.bitsPerSecond=$row[1];if($row[0] -eq 'Wi-Fi'){$l.signalPercent=88};$links+=$l};$demo.networkLinks=$links
[HardwarePulse.Json]::WriteAtomic($paths.Snapshot,$demo)
[HardwarePulse.Json]::WriteAtomic((Join-Path $state 'widget-settings.json'),@{width=1120;height=890;left=40;top=40;fontSize=14;language='en';details=$true;solid=$false;opacity=76;background='#15212B';quotaCodex=$true;quotaAntigravity=$true;quotaClaude=$true;desktopAppIconColors=$true;desktopWidth=510;desktopFontSize=16;desktopSpacing=10;desktopAutoContrast=$false;desktopTextOpacity=100;desktopColor='#EAF2F5';cardOrder=@('CPU','GPU','Memory','Airflow','NVMe','Network');names=@{CPU='Demo processor · 12 cores';GPU='Demo graphics';Memory='32 GB DDR5 · Demo memory';NVMe='Demo NVMe storage';Airflow='Demo chassis';cpuFan='CPU fan';bottom='Intake';top='Exhaust';diskC='C: System';diskD='D: Projects'};desktopVisible=@{gpuFan=$false;gpuFan2=$false;bottom=$false;top=$false;cpuFan=$false;ramA=$false;ramB=$false}})
$compiler=[CodeDom.Compiler.CompilerParameters]::new();[void]$compiler.ReferencedAssemblies.Add((Join-Path $AppPath 'HardwarePulse.exe'))
Add-Type -CompilerParameters $compiler -TypeDefinition @'
using System;using System.Threading;using HardwarePulse;
public static class HeroQuota {
 public static QuotaReading Read(string provider,CancellationToken cancel){
  var now=DateTimeOffset.UtcNow;var r=new QuotaReading{Provider=provider,Status="Live",Observed=now};
  if(provider!="Codex")r.Windows.Add(new QuotaWindow{Label="5-hour",Remaining=provider=="Claude"?74:100,Reset=now.AddHours(3)});
  r.Windows.Add(new QuotaWindow{Label="Weekly",Remaining=provider=="Codex"?64:provider=="Claude"?96:98,Reset=now.AddDays(4).AddHours(8)});return r;
 }
}
'@
$shell=[HardwarePulse.Shell]::new($paths,$true);$flags=[Reflection.BindingFlags]'Instance,NonPublic'
function Pump {$shell.Window.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::Background)}
function Settle {foreach($i in 1..30){Pump;Start-Sleep -Milliseconds 10}}
function Snapshot($visual){$visual.UpdateLayout();$b=[Windows.Media.Imaging.RenderTargetBitmap]::new([int]($visual.ActualWidth*2),[int]($visual.ActualHeight*2),192,192,[Windows.Media.PixelFormats]::Pbgra32);$b.Render($visual);$b.Freeze();return $b}
function Brush($hex){[Windows.Media.BrushConverter]::new().ConvertFromString($hex)}
function Text($context,$value,$x,$y,$size,$color,$bold=$false){$weight=if($bold){[Windows.FontWeights]::SemiBold}else{[Windows.FontWeights]::Normal};$type=[Windows.Media.Typeface]::new([Windows.Media.FontFamily]::new('Segoe UI'),[Windows.FontStyles]::Normal,$weight,[Windows.FontStretches]::Normal);$formatted=[Windows.Media.FormattedText]::new($value,[Globalization.CultureInfo]::InvariantCulture,[Windows.FlowDirection]::LeftToRight,$type,$size,(Brush $color));$context.DrawText($formatted,[Windows.Point]::new($x,$y))}
try {
 $field=$shell.GetType().GetField('quotas',$flags);$field.GetValue($shell).Dispose();$delegate=[Delegate]::CreateDelegate([Func[string,Threading.CancellationToken,HardwarePulse.QuotaReading]],[HeroQuota].GetMethod('Read'));$session=[HardwarePulse.QuotaSession]::new($delegate);$field.SetValue($shell,$session)
 foreach($provider in [HardwarePulse.QuotaSession]::Providers){$session.Enable($provider,$true)};$session.Tick([DateTimeOffset]::UtcNow)
 foreach($i in 1..100){$session.Tick([DateTimeOffset]::UtcNow);if(@($session.Readings|Where-Object Status -ne 'Live').Count -eq 0){break};Start-Sleep -Milliseconds 10}
 $shell.Window.ShowActivated=$false;$shell.Show();$shell.UpdatePanel();Settle
 # Hide only the clock's changing digits in fixture output through the existing status content.
 $shell.Window.FindName('Status').Text='● Live · Demo readings'
 $appImage=Snapshot $shell.Window
 $toggle=$shell.Window.FindName('DesktopEnabled');$toggle.IsChecked=$true;$toggle.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Primitives.ButtonBase]::ClickEvent))
 $lock=$shell.Window.FindName('DesktopLocked');$lock.IsChecked=$true;$lock.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Primitives.ButtonBase]::ClickEvent));Settle
 $desktop=$shell.GetType().GetField('desktop',$flags).GetValue($shell);$desktopImage=Snapshot $desktop
 $drawing=[Windows.Media.DrawingVisual]::new();$dc=$drawing.RenderOpen()
 $background=[Windows.Media.LinearGradientBrush]::new((Brush '#101A26').Color,(Brush '#172E36').Color,25);$dc.DrawRectangle($background,$null,[Windows.Rect]::new(0,0,1920,1280))
 foreach($spot in @(@(300,400,900,750,'#605C9BA0'),@(1350,300,850,900,'#60717CBD'),@(850,1000,1000,800,'#5035676D'))){
  $glow=[Windows.Media.RadialGradientBrush]::new();$glow.GradientStops.Add([Windows.Media.GradientStop]::new((Brush $spot[4]).Color,0));$glow.GradientStops.Add([Windows.Media.GradientStop]::new([Windows.Media.Colors]::Transparent,1));$dc.DrawEllipse($glow,$null,[Windows.Point]::new($spot[0],$spot[1]),$spot[2],$spot[3])
 }
 Text $dc 'HARDWARE PULSE' 72 46 18 '#98C7C6' $true
 Text $dc 'Your desktop. In focus.' 68 83  60 '#F0F6FA' $true
 Text $dc 'Hardware, connections and AI quota — one quiet view.' 72 169 25 '#AEC1CE'
 Text $dc ('WINDOWS  ·  '+$version) 1592 61 18 '#C1D1DD'
 Text $dc 'APP / ADAPTIVE CARDS' 74 234 17 '#8EDBCC' $true
 Text $dc 'DESKTOP / AT A GLANCE' 1280 234 17 '#8EDBCC' $true
 $dc.DrawRoundedRectangle((Brush '#70030B11'),$null,[Windows.Rect]::new(66,283,1140,914),22,22)
 $dc.DrawImage($appImage,[Windows.Rect]::new(76,272,1120,890))
 $desktopHeight=$desktopImage.Height;$dc.DrawImage($desktopImage,[Windows.Rect]::new(1270,272,510,$desktopHeight))
 Text $dc 'LAN + WI-FI' 1292 1060 17 '#8EDBCC' $true
 Text $dc 'Independent connection rates.' 1292 1089 21 '#D3E0E6'
 Text $dc 'Even when Wi-Fi is idle.' 1292 1122 21 '#AEC1CE'
 Text $dc 'Actual WPF UI + shipped SVG icons  /  Fictional demo data' 76 1216 18 '#93AAB9'
 $dc.Close();$result=[Windows.Media.Imaging.RenderTargetBitmap]::new(1920,1280,96,96,[Windows.Media.PixelFormats]::Pbgra32);$result.Render($drawing);$png=[Windows.Media.Imaging.PngBitmapEncoder]::new();$png.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($result));$stream=[IO.File]::Create($OutputPath);try{$png.Save($stream)}finally{$stream.Dispose()}
 Write-Output $OutputPath
}finally{$shell.Exit()}
