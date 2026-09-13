. "$PSScriptRoot\Paths.ps1"
$ErrorActionPreference='Stop'
trap { $_.Exception.ToString() | Set-Content "$script:stateRoot\glass-error.txt"; break }
'Loading WPF' | Set-Content "$script:stateRoot\glass-stage.txt"
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
. "$PSScriptRoot\Sensors.ps1"
. "$PSScriptRoot\Icons.ps1"
$mutex=New-Object Threading.Mutex($false,'Local\HardwarePulseGlass')
if(-not $mutex.WaitOne(0)){exit}
[xml]$xml=Get-Content "$PSScriptRoot\Panel.xaml" -Raw -Encoding UTF8
$reader=New-Object Xml.XmlNodeReader $xml
$window=[Windows.Markup.XamlReader]::Load($reader)
$window.FindName('BrandIcon').Content=New-PulseIcon 'live' 22 '#A5E7D5'
$window.Icon=[Windows.Media.Imaging.BitmapFrame]::Create([Uri]::new((Join-Path $PSScriptRoot 'assets/pulse.ico')))
$script:settingsPath=Join-Path $script:stateRoot 'widget-settings.json'
try {
    $saved=Get-Content $script:settingsPath -Raw -Encoding UTF8 -ErrorAction Stop | ConvertFrom-Json
    $area=[Windows.SystemParameters]::WorkArea
    $window.Width=[Math]::Max(240,[Math]::Min([double]$saved.width,$area.Width))
    $window.Height=[Math]::Max(340,[Math]::Min([double]$saved.height,$area.Height))
    $window.Left=[Math]::Max($area.Left,[Math]::Min([double]$saved.left,$area.Right-$window.Width))
    $window.Top=[Math]::Max($area.Top,[Math]::Min([double]$saved.top,$area.Bottom-$window.Height))
    $window.WindowStartupLocation='Manual'
    $window.Topmost=[bool]$saved.pin; $window.FindName('Pin').IsChecked=[bool]$saved.pin
    $window.FindName('Solid').IsChecked=[bool]$saved.solid
    $window.FindName('Large').IsChecked=[bool]$saved.large
    $window.FontSize=if($saved.large){14}else{12}
    if($null -ne $saved.opacity){$window.FindName('OpacitySlider').Value=[Math]::Max(15,[Math]::Min(100,[double]$saved.opacity))}
} catch {}
if((Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize' -Name EnableTransparency -ErrorAction SilentlyContinue).EnableTransparency -eq 0){$window.FindName('Solid').IsChecked=$true}
'Building cards' | Set-Content "$script:stateRoot\glass-stage.txt"
$script:cells=@{}; $script:peaks=@{}; $script:lastIdentity=''; $script:mode='live'
$script:labels=@{};$script:autoNames=@{};$script:nameOverrides=@{};$script:nameEditors=@{};$script:usageCells=@{}
if($saved.names){foreach($property in $saved.names.PSObject.Properties){$script:nameOverrides[$property.Name]=[string]$property.Value}}
elseif($saved){
    # Migrate the existing owner's explicitly configured labels; new installs start with discovery.
    try{
        $oldSnapshot=Get-Content "$runtime\snapshot.json" -Raw | ConvertFrom-Json
        if(@($oldSnapshot.sensors | Where-Object {$_.hardware -match 'Ryzen 7 9700X'}).Count -and @($oldSnapshot.sensors | Where-Object {$_.hardware -eq 'KIOXIA-EXCERIA PLUS G4 SSD'}).Count){
            $script:nameOverrides=@{CPU='Ryzen 7 9700X · Tctl/Tdie';GPU='GeForce RTX 5080';Airflow='LIAN LI A3 · System Temperature';cpuFan='PS120 EVO';bottom='Bottom Intake · SYS1';top='Top Exhaust · SYS3';NVMe='KIOXIA · Composite Temperature'}
        }
    }catch{}
}
# Repair only the exact legacy defaults affected by the former ANSI settings reader.
$legacyDefaults=@{CPU='Ryzen 7 9700X · Tctl/Tdie';Airflow='LIAN LI A3 · System Temperature';bottom='Bottom Intake · SYS1';top='Top Exhaust · SYS3';NVMe='KIOXIA · Composite Temperature'}
foreach($key in $legacyDefaults.Keys){
    if($script:nameOverrides[$key] -eq $legacyDefaults[$key].Replace([string][char]0x00B7,[string][char]0x8DEF)){$script:nameOverrides[$key]=$legacyDefaults[$key]}
}

$cards=$window.FindName('Cards'); $status=$window.FindName('Status')
function Add-Card($title,$subtitle,$accent,$hero,$rows) {
    $border=New-Object Windows.Controls.Border
    $border.CornerRadius=14; $border.Padding=10; $border.Margin='0,0,0,6'; $border.BorderThickness=1
    $border.BorderBrush=[Windows.Media.BrushConverter]::new().ConvertFromString('#426D8B9F')
    $brush=New-Object Windows.Media.LinearGradientBrush
    $brush.StartPoint='0,0'; $brush.EndPoint='1,1'
    $brush.GradientStops.Add([Windows.Media.GradientStop]::new([Windows.Media.ColorConverter]::ConvertFromString('#7031485B'),0))
    $brush.GradientStops.Add([Windows.Media.GradientStop]::new([Windows.Media.ColorConverter]::ConvertFromString('#40213048'),1))
    $border.Background=$brush
    $stack=New-Object Windows.Controls.StackPanel; $border.Child=$stack
    $header=New-Object Windows.Controls.Grid
    $header.ColumnDefinitions.Add([Windows.Controls.ColumnDefinition]::new())
    $col=[Windows.Controls.ColumnDefinition]::new();$col.Width='Auto';$header.ColumnDefinitions.Add($col)
    $label=New-Object Windows.Controls.TextBlock; $label.Text=$title; $label.FontSize=13;$label.FontWeight='SemiBold';$label.VerticalAlignment='Center'
    $nameRow=[Windows.Controls.StackPanel]::new();$nameRow.Orientation='Horizontal';$nameRow.VerticalAlignment='Center';$icon=New-PulseIcon ($title.ToLower()) 18 $accent;$icon.Margin='0,0,8,0';$null=$nameRow.Children.Add($icon);$null=$nameRow.Children.Add($label);$null=$header.Children.Add($nameRow)
    $value=New-Object Windows.Controls.TextBlock;$value.Text='—';$value.FontSize=23;$value.FontWeight='Normal';$value.Foreground=[Windows.Media.BrushConverter]::new().ConvertFromString($accent)
    [Windows.Controls.Grid]::SetColumn($value,1);$null=$header.Children.Add($value);$script:cells[$hero]=@($value,'°C')
    $null=$stack.Children.Add($header)
    $sub=New-Object Windows.Controls.TextBlock;$sub.Text=$subtitle;$sub.FontSize=10;$sub.Foreground=[Windows.Media.Brushes]::LightSteelBlue;$sub.Margin='0,3,0,6';$null=$stack.Children.Add($sub)
    $script:labels[$title]=$sub;$script:autoNames[$title]=$subtitle
    foreach($row in $rows) {
        $grid=New-Object Windows.Controls.Grid;$grid.Margin='0,2,0,2'
        $grid.ColumnDefinitions.Add([Windows.Controls.ColumnDefinition]::new())
        $col=[Windows.Controls.ColumnDefinition]::new();$col.Width='Auto';$grid.ColumnDefinitions.Add($col)
        $label=New-Object Windows.Controls.TextBlock;$label.Text=$row[0];$label.Foreground=[Windows.Media.BrushConverter]::new().ConvertFromString('#C0D0DD');$label.Margin='0,0,10,0';$null=$grid.Children.Add($label)
        if($row[1] -in @('cpuFan','bottom','top')){$script:labels[$row[1]]=$label;$script:autoNames[$row[1]]=$row[0]}
        if($row[1] -eq 'gpuFan'){$script:gpuFanLabel=$label}
        $value=New-Object Windows.Controls.TextBlock;$value.Text='—';$value.FontFamily='Segoe UI';$value.FontWeight='SemiBold';[Windows.Documents.Typography]::SetNumeralAlignment($value,'Tabular');$value.VerticalAlignment='Center';[Windows.Controls.Grid]::SetColumn($value,1);$null=$grid.Children.Add($value)
        $script:cells[$row[1]]=@($value,$row[2]);$null=$stack.Children.Add($grid)
    }
    $null=$cards.Children.Add($border)
}
Add-Card 'CPU' 'Processor' '#A5E7D5' 'cpu' @(@('Utilization','cpuLoad','%'),@('Vcore · Motherboard','vcore','V'),@('CPU Fan','cpuFan','RPM'))
Add-Card 'GPU' 'Graphics' '#A7CBFF' 'gpu' @(@('Utilization','gpuLoad','%'),@('VRAM Junction','vram','°C'),@('Core Voltage','gpuVolt','V'),@('Fan Speed','gpuFan','RPM'))
function Add-PairCard($title,$subtitle,$leftLabel,$leftKey,$rightLabel,$rightKey,$accent) {
    $border=New-Object Windows.Controls.Border;$border.CornerRadius=14;$border.Padding=10;$border.Margin='0,0,0,6';$border.BorderThickness=1
    $border.BorderBrush=[Windows.Media.BrushConverter]::new().ConvertFromString('#426D8B9F');$border.Background=[Windows.Media.BrushConverter]::new().ConvertFromString('#5031485B')
    $stack=New-Object Windows.Controls.StackPanel;$border.Child=$stack
    $heading=New-Object Windows.Controls.TextBlock;$heading.Text=$title;$heading.FontSize=13;$heading.FontWeight='SemiBold';$nameRow=[Windows.Controls.StackPanel]::new();$nameRow.Orientation='Horizontal';$icon=New-PulseIcon ($title.ToLower()) 18 $accent;$icon.Margin='0,0,8,0';$null=$nameRow.Children.Add($icon);$null=$nameRow.Children.Add($heading);$null=$stack.Children.Add($nameRow)
    $sub=New-Object Windows.Controls.TextBlock;$sub.Text=$subtitle;$sub.FontSize=10;$sub.Foreground=[Windows.Media.Brushes]::LightSteelBlue;$sub.Margin='0,3,0,7';$null=$stack.Children.Add($sub)
    $script:labels[$title]=$sub;$script:autoNames[$title]=$subtitle
    $grid=New-Object Windows.Controls.Grid;$grid.ColumnDefinitions.Add([Windows.Controls.ColumnDefinition]::new());$grid.ColumnDefinitions.Add([Windows.Controls.ColumnDefinition]::new())
    $pairs=@(@($leftLabel,$leftKey),@($rightLabel,$rightKey))
    for($i=0;$i -lt 2;$i++){
        $column=New-Object Windows.Controls.StackPanel;[Windows.Controls.Grid]::SetColumn($column,$i)
        $label=New-Object Windows.Controls.TextBlock;$label.Text=$pairs[$i][0];$label.FontSize=12;$label.Foreground=[Windows.Media.Brushes]::LightSteelBlue;$null=$column.Children.Add($label)
        $label.Margin='0,0,6,0';$label.FontSize=10
        $script:labels[$pairs[$i][1]]=$label;$script:autoNames[$pairs[$i][1]]=$pairs[$i][0]
        $value=New-Object Windows.Controls.TextBlock;$value.Text='—';$value.FontSize=21;$value.FontWeight='Normal';$value.Margin='0,6,0,0';$value.Foreground=[Windows.Media.BrushConverter]::new().ConvertFromString($accent);$null=$column.Children.Add($value)
        $script:cells[$pairs[$i][1]]=@($value,'°C');$null=$grid.Children.Add($column)
    }
    $null=$stack.Children.Add($grid);$null=$cards.Children.Add($border)
}
Add-PairCard 'Memory' 'Memory' 'Module 1' 'ramA' 'Module 2' 'ramB' '#E7C5A4'
Add-PairCard 'NVMe' 'NVMe · Composite Temperature' 'Drive 1' 'diskC' 'Drive 2' 'diskD' '#BBC8F4'
Add-Card 'Airflow' 'Motherboard' '#B9DCD9' 'system' @(@('System Fan 1','bottom','RPM'),@('System Fan 2','top','RPM'))
function Add-UsageRow($card,[string]$key,[string]$label,[string]$accent){
    $stack=[Windows.Controls.StackPanel]::new();$stack.Margin='0,8,0,0'
    $text=[Windows.Controls.TextBlock]::new();$text.FontSize=11;$text.Text=$label+' —';$text.Margin='0,0,0,5'
    [Windows.Documents.Typography]::SetNumeralAlignment($text,'Tabular')
    $track=[Windows.Controls.Border]::new();$track.Height=3;$track.CornerRadius=1.5;$track.Background=[Windows.Media.BrushConverter]::new().ConvertFromString('#304A6678')
    $bar=[Windows.Controls.Border]::new();$bar.Height=3;$bar.Width=0;$bar.HorizontalAlignment='Left';$bar.CornerRadius=1.5;$bar.Background=[Windows.Media.BrushConverter]::new().ConvertFromString($accent);$track.Child=$bar
    $null=$stack.Children.Add($text);$null=$stack.Children.Add($track);$null=$card.Child.Children.Add($stack)
    $script:usageCells[$key]=@($text,$track,$bar,$label)
}
Add-UsageRow $cards.Children[1] 'vram' 'VRAM' '#A7CBFF'
Add-UsageRow $cards.Children[2] 'ram' 'RAM' '#E7C5A4'
function Update-DeviceNames {
    foreach($key in $script:labels.Keys){
        $text=if($script:nameOverrides.ContainsKey($key) -and $script:nameOverrides[$key]){$script:nameOverrides[$key]}else{$script:autoNames[$key]}
        $script:labels[$key].Text=$text;$script:labels[$key].ToolTip=$text
        if($script:nameEditors.ContainsKey($key)){$script:nameEditors[$key].ToolTip='Automatic: '+$script:autoNames[$key]}
    }
}
Update-DeviceNames
function Save-WidgetSettings {
    $bounds=$window.RestoreBounds
    if($bounds.IsEmpty){return}
    $settings=@{width=$bounds.Width;height=$bounds.Height;left=$bounds.Left;top=$bounds.Top;pin=$window.Topmost;solid=[bool]$window.FindName('Solid').IsChecked;large=[bool]$window.FindName('Large').IsChecked;opacity=$window.FindName('OpacitySlider').Value;cardOrder=@($cards.Children | ForEach-Object {$_.Tag});names=$script:nameOverrides} | ConvertTo-Json -Depth 4
    $temp=$script:settingsPath+'.tmp'
    [IO.File]::WriteAllText($temp,$settings)
    if([IO.File]::Exists($script:settingsPath)){
        [IO.File]::Replace($temp,$script:settingsPath,[System.Management.Automation.Language.NullString]::Value)
    }else{[IO.File]::Move($temp,$script:settingsPath)}
}
function Move-Card($card,[int]$delta) {
    $index=$cards.Children.IndexOf($card);$target=$index+$delta
    if($index -lt 0 -or $target -lt 0 -or $target -ge $cards.Children.Count){return}
    $cards.Children.RemoveAt($index);$cards.Children.Insert($target,$card)
    Update-OrderButtons
    $card.BringIntoView()
    Save-WidgetSettings
}
function Update-OrderButtons {
    for($i=0;$i -lt $cards.Children.Count;$i++){
        $entry=$script:orderButtons[[string]$cards.Children[$i].Tag]
        $entry[0].IsEnabled=$i -gt 0
        $entry[1].IsEnabled=$i -lt ($cards.Children.Count-1)
    }
}
Add-Type -Path "$PSScriptRoot\CardDrag.cs" -ReferencedAssemblies @('PresentationFramework','PresentationCore','WindowsBase','System.Xaml')
$script:orderButtons=@{}
$titles=@('CPU','GPU','Memory','NVMe','Airflow')
for($i=0;$i -lt $cards.Children.Count;$i++){
    $card=$cards.Children[$i];$card.Tag=$titles[$i]
        $drag=[Windows.Controls.Primitives.Thumb]::new();$drag.Width=16;$drag.Height=20;$drag.Margin='0,0,6,0';$drag.Cursor='SizeAll';$drag.Focusable=$true
    $drag.ToolTip='Drag To Reorder (Esc To Cancel)'
    [Windows.Automation.AutomationProperties]::SetName($drag,'Drag '+$card.Tag)
    [xml]$thumbXaml='<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Thumb"><Border x:Name="GripPlate" CornerRadius="8" Background="Transparent" Padding="3"><Path Data="M8 5H9 M15 5H16 M8 12H9 M15 12H16 M8 19H9 M15 19H16" Stroke="#C2D8E5" StrokeThickness="2" StrokeStartLineCap="Round" StrokeEndLineCap="Round" Stretch="Uniform"/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="GripPlate" Property="Background" Value="#405E829D"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="GripPlate" Property="Background" Value="#605E829D"/></Trigger></ControlTemplate.Triggers></ControlTemplate>'
    $drag.Template=[Windows.Markup.XamlReader]::Load([Xml.XmlNodeReader]::new($thumbXaml))
    [CardDrag]::Attach($cards,$card,$drag,$window.FindName('CardScroll'),[Action]{Update-OrderButtons;Save-WidgetSettings})
        $first=$card.Child.Children[0]
    $headingRow=if($first -is [Windows.Controls.Grid]){$first.Children[0]}else{$first}
    $headingRow.Children.Insert(0,$drag)
    $menu=[Windows.Controls.ContextMenu]::new()
    $menu.Background=[Windows.Media.BrushConverter]::new().ConvertFromString('#182B3B');$menu.Foreground=[Windows.Media.Brushes]::White
    $card.ContextMenu=$menu;$drag.ContextMenu=$menu
    $buttons=@()
    foreach($delta in @(-1,1)){
        $button=[Windows.Controls.MenuItem]::new()
        $button.Icon=New-PulseIcon $(if($delta -lt 0){'up'}else{'down'}) 14
        $direction=if($delta -lt 0){'up'}else{'down'}
        $button.ToolTip='Move '+$card.Tag+' '+$direction;$button.Header='Move '+(Get-Culture).TextInfo.ToTitleCase($direction)
        [Windows.Automation.AutomationProperties]::SetName($button,$button.ToolTip)
        $button.Tag=@($card,$delta)
        $button.Add_Click({param($sender,$eventArgs) Move-Card $sender.Tag[0] $sender.Tag[1]})
        $null=$menu.Items.Add($button);$buttons+=,$button
    }
    $script:orderButtons[[string]$card.Tag]=$buttons

}
# Ignore unknown/duplicate saved IDs and append newly added cards in default order.
$byTitle=@{};foreach($card in $cards.Children){$byTitle[[string]$card.Tag]=$card}
$ordered=@();foreach($title in @($saved.cardOrder)+$titles){
    if($null -ne $title -and $byTitle.ContainsKey([string]$title)){$ordered+=,$byTitle[[string]$title];$byTitle.Remove([string]$title)}
}
$cards.Children.Clear();foreach($card in $ordered){$null=$cards.Children.Add($card)}
Update-OrderButtons
'Preparing backdrop' | Set-Content "$script:stateRoot\glass-stage.txt"
function Update-Panel {
    $data=Get-PulseSnapshot "$script:runtime\snapshot.json"
    if($data.names){foreach($key in $data.names.Keys){if($data.names[$key]){$script:autoNames[$key]=$data.names[$key]}};Update-DeviceNames}
    foreach($key in $script:usageCells.Keys){
        $cell=$script:usageCells[$key];$usage=if($data.state -eq 'LIVE'){$data.usage[$key]}else{$null}
        if($usage){
            $cell[0].Text=('{0}  {1:F1} / {2:F1} GB · {3:F0}%' -f $cell[3],$usage.used,$usage.total,$usage.percent)
            $cell[2].Width=[Math]::Max(0,$cell[1].ActualWidth*$usage.percent/100)
        }else{$cell[0].Text=$cell[3]+' —';$cell[2].Width=0}
        $cell[0].ToolTip='Current used / usable capacity (GB, binary units). Usage stays live in Session Max.'
    }
    if($data.state -eq 'LIVE' -and $data.identity -ne $script:lastIdentity){
        foreach($key in $data.values.Keys){if(-not $script:peaks.ContainsKey($key) -or $data.values[$key] -gt $script:peaks[$key]){$script:peaks[$key]=$data.values[$key]}}
        $script:lastIdentity=$data.identity
    }
    $status.Text=if($data.state -eq 'LIVE'){'● Live · '+$data.time.ToLocalTime().ToString('HH:mm:ss')+' · '+$data.values.Count+'/'+$script:SensorMap.Count+' sensors'}else{'● '+$data.state+' · Waiting for collector'}
    if($script:mode -eq 'max'){$status.Text+=' · Session peaks'}
    $status.Foreground=if($data.state -eq 'LIVE'){[Windows.Media.Brushes]::Aquamarine}else{[Windows.Media.Brushes]::PeachPuff}
    $values=if($script:mode -eq 'max'){$script:peaks}else{$data.values}
    foreach($key in $script:cells.Keys) {
        $cell=$script:cells[$key];$cell[0].Text='—'
        if($values.ContainsKey($key)){$format=if($cell[1] -eq 'V'){'{0:F3}'}elseif($cell[1] -eq 'RPM'){'{0:F0}'}else{'{0:F1}'};$cell[0].Text=($format -f $values[$key])+' '+$cell[1]}
    }
    $window.FindName('Live').Background=if($script:mode -eq 'live'){[Windows.Media.BrushConverter]::new().ConvertFromString('#607898A8')}else{[Windows.Media.Brushes]::Transparent}
    if($data.state -eq 'LIVE'){$script:gpuFanCount=$data.gpuFanCount}
    $script:gpuFanLabel.Text='Fan Speed'
    if($script:gpuFanCount -gt 1){
        $first=if($values.ContainsKey('gpuFan')){'{0:F0}' -f $values.gpuFan}else{'—'}
        $second=if($values.ContainsKey('gpuFan2')){'{0:F0}' -f $values.gpuFan2}else{'—'}
        $script:cells.gpuFan[0].Text=$first+' / '+$second+' RPM'
        $script:cells.gpuFan[0].ToolTip='GPU Fan 1 / GPU Fan 2 telemetry channels. These do not count physical fans.'
    }
    $window.FindName('Max').Background=if($script:mode -eq 'max'){[Windows.Media.BrushConverter]::new().ConvertFromString('#607898A8')}else{[Windows.Media.Brushes]::Transparent}
    @{updated=[DateTimeOffset]::Now.ToString('o');state=$data.state;mode=$script:mode;sensors=$data.values.Count} | ConvertTo-Json | Set-Content "$script:stateRoot\view-status.json"
}
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class PulseBackdrop {
 [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd,int attr,ref int value,int size);
 [StructLayout(LayoutKind.Sequential)] public struct Margins { public int Left,Right,Top,Bottom; }
 [DllImport("dwmapi.dll")] public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd,ref Margins margins);
}
'@
function Set-Material {
    $solid=$window.FindName('Solid').IsChecked -or [Windows.SystemParameters]::HighContrast
    $hwnd=[Windows.Interop.WindowInteropHelper]::new($window).Handle
    if($hwnd -eq [IntPtr]::Zero){return}
    $kind=if($solid){1}else{3};$dark=1;$round=2
    $result=[PulseBackdrop]::DwmSetWindowAttribute($hwnd,38,[ref]$kind,4)
    $null=[PulseBackdrop]::DwmSetWindowAttribute($hwnd,20,[ref]$dark,4)
    $null=[PulseBackdrop]::DwmSetWindowAttribute($hwnd,33,[ref]$round,4)
    $margins=New-Object PulseBackdrop+Margins
    $margins.Left=-1;$margins.Right=-1;$margins.Top=-1;$margins.Bottom=-1
    $null=[PulseBackdrop]::DwmExtendFrameIntoClientArea($hwnd,[ref]$margins)
    [Windows.Interop.HwndSource]::FromHwnd($hwnd).CompositionTarget.BackgroundColor=[Windows.Media.Colors]::Transparent
    $opacity=$window.FindName('OpacitySlider').Value
    $alpha=[byte][Math]::Round($opacity*255/100)
    $window.FindName('OpacitySlider').IsEnabled=(-not $solid -and $result -eq 0)
    $window.FindName('OpacitySlider').ToolTip=if($result -ne 0){'System glass background is unavailable on this Windows version.'}else{'Background opacity'}
    $window.FindName('OpacityValue').Text=if($solid -or $result -ne 0){'100%'}else{([Math]::Round($opacity)).ToString()+'%'}
    $window.Background=if($solid -or $result -ne 0){[Windows.Media.BrushConverter]::new().ConvertFromString('#182332')}else{[Windows.Media.SolidColorBrush]::new([Windows.Media.Color]::FromArgb($alpha,24,35,50))}
}
foreach($pair in @(@('Close','close'),@('Minimize','minimize'))){
    $button=$window.FindName($pair[0]);$button.Content=New-PulseIcon $pair[1] 14
    [Windows.Automation.AutomationProperties]::SetName($button,$pair[0])
}
$window.FindName('Settings').Content=New-PulseIcon 'settings' 20
$window.FindName('Back').Content=New-PulseIcon 'back' 16
function Show-Settings([bool]$show){
    $window.FindName('SettingsPage').Visibility=if($show){'Visible'}else{'Collapsed'}
    foreach($name in @('CardScroll','MonitorControls','MonitorFooter','Status')){
        $window.FindName($name).Visibility=if($show){'Collapsed'}else{'Visible'}
    }
    if($show){$null=$window.FindName('Back').Focus()}else{$null=$window.FindName('Settings').Focus()}
}
$window.FindName('Settings').Add_Click({Show-Settings $true})
$window.FindName('Back').Add_Click({Show-Settings $false})
$window.FindName('GitHub').Add_Click({Start-Process 'https://github.com/medking82/hardware-pulse'})
$window.Add_PreviewKeyDown({if($_.Key -eq 'Escape' -and $window.FindName('SettingsPage').IsVisible){Show-Settings $false;$_.Handled=$true}})
$window.FindName('Live').Add_Click({$script:mode='live';Update-Panel})
$window.FindName('Max').Add_Click({$script:mode='max';Update-Panel})
$window.FindName('Pin').Add_Click({$window.Topmost=[bool]$window.FindName('Pin').IsChecked})
$window.FindName('Solid').Add_Click({Set-Material})
$window.FindName('OpacitySlider').Add_ValueChanged({Set-Material})
$window.FindName('Large').Add_Click({$window.FontSize=if($window.FindName('Large').IsChecked){14}else{12}})
$window.Add_SizeChanged({
    $scale=[Math]::Max(0.85,[Math]::Min(1.0,$window.ActualWidth/280.0))
    $window.FindName('Viewport').LayoutTransform=[Windows.Media.ScaleTransform]::new($scale,$scale)
})
$window.FindName('Minimize').Add_Click({$window.WindowState='Minimized'})
$window.FindName('Close').Add_Click({$window.Close()})
$window.FindName('DragHandle').Add_MouseLeftButtonDown({if($_.ButtonState -eq 'Pressed'){$window.DragMove()}})
Add-Type -Path "$PSScriptRoot\WindowSnap.cs" -ReferencedAssemblies @('PresentationFramework','PresentationCore','WindowsBase','System.Xaml')
$window.Add_SourceInitialized({Set-Material;[WindowSnap]::Attach($window)})
$timer=New-Object Windows.Threading.DispatcherTimer;$timer.Interval=[TimeSpan]::FromSeconds(2);$timer.Add_Tick({Update-Panel});$timer.Start()
# Persist settled changes while the app is running, without relying on Windows shutdown callbacks.
$settingsTimer=New-Object Windows.Threading.DispatcherTimer
$settingsTimer.Interval=[TimeSpan]::FromMilliseconds(750)
$settingsTimer.Add_Tick({$settingsTimer.Stop();Save-WidgetSettings})
$queueSettings={if($window.IsLoaded){$settingsTimer.Stop();$settingsTimer.Start()}}
$window.Add_LocationChanged($queueSettings)
$window.Add_SizeChanged($queueSettings)
$window.FindName('OpacitySlider').Add_ValueChanged($queueSettings)
foreach($name in @('Pin','Solid','Large')){
    $window.FindName($name).Add_Checked($queueSettings)
    $window.FindName($name).Add_Unchecked($queueSettings)
}
foreach($pair in @(@('CPU','CPU Name'),@('GPU','GPU Name'),@('Memory','Memory Details'),@('ramA','Module 1 Label'),@('ramB','Module 2 Label'),@('NVMe','NVMe Details'),@('diskC','Drive 1 Name'),@('diskD','Drive 2 Name'),@('Airflow','Case / Motherboard'),@('cpuFan','CPU Fan Name'),@('bottom','System Fan 1'),@('top','System Fan 2'))){
    $label=[Windows.Controls.TextBlock]::new();$label.Text=$pair[1];$label.FontSize=11;$label.Margin='0,0,0,4'
    $editor=[Windows.Controls.TextBox]::new();$editor.Tag=$pair[0];$editor.MaxLength=160;$editor.Padding=6;$editor.Margin='0,0,0,12'
    $editor.Background=[Windows.Media.BrushConverter]::new().ConvertFromString('#5031485B');$editor.Foreground=[Windows.Media.Brushes]::White;$editor.BorderBrush=[Windows.Media.BrushConverter]::new().ConvertFromString('#60748B9F')
    $editor.Text=if($script:nameOverrides[$pair[0]]){$script:nameOverrides[$pair[0]]}else{''}
    [Windows.Automation.AutomationProperties]::SetName($editor,$pair[1])
    $editor.Add_TextChanged({param($sender,$eventArgs) $script:nameOverrides[[string]$sender.Tag]=$sender.Text.Trim();Update-DeviceNames;if($window.IsLoaded){$settingsTimer.Stop();$settingsTimer.Start()}})
    $script:nameEditors[$pair[0]]=$editor
    $null=$window.FindName('NameFields').Children.Add($label);$null=$window.FindName('NameFields').Children.Add($editor)
}
Update-DeviceNames
$window.Add_Closing({Save-WidgetSettings})
$window.Add_Closed({
    $settingsTimer.Stop()
    $timer.Stop()
    Save-WidgetSettings
})
try{Update-Panel;'Showing window' | Set-Content "$script:stateRoot\glass-stage.txt";$null=$window.ShowDialog()}finally{$mutex.ReleaseMutex();$mutex.Dispose()}
