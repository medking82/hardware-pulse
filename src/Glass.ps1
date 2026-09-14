. "$PSScriptRoot\Paths.ps1"
$ErrorActionPreference='Stop'
trap { $_.Exception.ToString() | Set-Content "$script:stateRoot\glass-error.txt"; break }
'Loading WPF' | Set-Content "$script:stateRoot\glass-stage.txt"
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
. "$PSScriptRoot\Sensors.ps1"
. "$PSScriptRoot\Icons.ps1"
. "$PSScriptRoot\Localization.ps1"
$mutex=New-Object Threading.Mutex($false,'Local\HardwarePulseGlass')
if(-not $mutex.WaitOne(0)){exit}
$script:exitRequested=$false
$script:collectorStartFailed=$false
$script:stopBlocked=$false
$script:ignoredStopTime=0L
try{
    if(Test-Path "$runtime\STOP"){
        try{Remove-Item -LiteralPath "$runtime\STOP" -ErrorAction Stop}catch{$script:stopBlocked=$true;$script:ignoredStopTime=[IO.File]::GetLastWriteTimeUtc("$runtime\STOP").Ticks;throw}
    }
    if((Get-PulseSnapshot "$runtime\snapshot.json").state -ne 'LIVE'){
        $task=Get-ScheduledTask -TaskName 'Hardware Pulse Collector' -ErrorAction Stop
        $owner=if($task.Principal.UserId -like 'S-1-*'){$task.Principal.UserId}else{[Security.Principal.NTAccount]::new($task.Principal.UserId).Translate([Security.Principal.SecurityIdentifier]).Value}
        if(@($task.Actions).Count -ne 1 -or $task.Actions.Execute -ne (Join-Path $PSScriptRoot 'HardwarePulse.exe') -or $task.Actions.Arguments -ne '--collector' -or $owner -ne [Security.Principal.WindowsIdentity]::GetCurrent().User.Value){throw 'Collector ownership mismatch'}
        Start-ScheduledTask -InputObject $task -ErrorAction Stop
    }
}catch{$script:collectorStartFailed=$true}
[xml]$xml=Get-Content "$PSScriptRoot\Panel.xaml" -Raw -Encoding UTF8
$reader=New-Object Xml.XmlNodeReader $xml
$window=[Windows.Markup.XamlReader]::Load($reader)
function Get-PulseInitialSize([double]$workWidth,[double]$workHeight){
    # WPF and WorkArea use device-independent pixels, including Windows scaling.
    return @{width=280;height=[Math]::Max(340,[Math]::Min(650,[Math]::Floor($workHeight*.90)))}
}
$initialSize=Get-PulseInitialSize ([Windows.SystemParameters]::WorkArea.Width) ([Windows.SystemParameters]::WorkArea.Height)
$window.Width=$initialSize.width;$window.Height=$initialSize.height
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
    $window.FontSize=if($null -ne $saved.fontSize -and [double]$saved.fontSize -ge 10 -and [double]$saved.fontSize -le 16){[double]$saved.fontSize}elseif($saved.large){14}else{12}
    if($null -ne $saved.opacity){$window.FindName('OpacitySlider').Value=[Math]::Max(0,[Math]::Min(100,[double]$saved.opacity))}
} catch {}
$window.FindName('FontSizeSlider').Value=$window.FontSize
$window.FindName('FontSizeValue').Text=$window.FontSize.ToString()+' px'
$script:languagePreference=if($saved.language -in @('en','zh-CN','zh-TW','auto')){[string]$saved.language}else{'auto'}
$script:language=Resolve-PulseLanguage $script:languagePreference
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
$script:cells.vram[0].Parent.ToolTip='Memory-chip internal temperature (junction sensor when available); separate from GPU core temperature.'
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
    $text=[Windows.Controls.TextBlock]::new();$text.FontSize=11;$text.TextWrapping='Wrap';$text.Text=$label+' —';$text.Margin='0,0,0,5'
    [Windows.Documents.Typography]::SetNumeralAlignment($text,'Tabular')
    $track=[Windows.Controls.Border]::new();$track.Height=3;$track.CornerRadius=1.5;$track.Background=[Windows.Media.BrushConverter]::new().ConvertFromString('#304A6678')
    $bar=[Windows.Controls.Border]::new();$bar.Height=3;$bar.Width=0;$bar.HorizontalAlignment='Left';$bar.CornerRadius=1.5;$bar.Background=[Windows.Media.BrushConverter]::new().ConvertFromString($accent);$track.Child=$bar
    $null=$stack.Children.Add($text);$null=$stack.Children.Add($track);$null=$card.Child.Children.Add($stack)
    $script:usageCells[$key]=@($text,$track,$bar,$label)
}
Add-UsageRow $cards.Children[1] 'vram' 'VRAM' '#A7CBFF'
Add-UsageRow $cards.Children[2] 'ram' 'RAM' '#E7C5A4'
. "$PSScriptRoot\Density.ps1"
. "$PSScriptRoot\Typography.ps1"
function Update-DeviceNames {
    foreach($key in $script:labels.Keys){
        $text=if($script:nameOverrides.ContainsKey($key) -and $script:nameOverrides[$key]){
            if($legacyDefaults.ContainsKey($key) -and $script:nameOverrides[$key] -eq $legacyDefaults[$key]){Get-PulseDeviceText $script:nameOverrides[$key]}else{$script:nameOverrides[$key]}
        }else{Get-PulseDeviceText $script:autoNames[$key]}
        $script:labels[$key].Text=$text;$script:labels[$key].ToolTip=$text
        if($script:nameEditors.ContainsKey($key)){$script:nameEditors[$key].ToolTip=(Get-PulseText 'Automatic: ')+(Get-PulseDeviceText $script:autoNames[$key])}
    }
}
Update-DeviceNames
function Save-WidgetSettings {
    $bounds=$window.RestoreBounds
    if($bounds.IsEmpty){return}
    $settings=@{fontSize=$window.FontSize;positionLocked=$script:positionLocked;cardsVisible=$script:cardsVisible;details=$script:showDetails;background=$script:backgroundHex;autoUpdates=$script:autoUpdates;autoDownload=$script:autoDownload;overlay=$script:overlayState;language=$script:languagePreference;width=$bounds.Width;height=$bounds.Height;left=$bounds.Left;top=$bounds.Top;pin=$window.Topmost;solid=[bool]$window.FindName('Solid').IsChecked;large=[bool]$window.FindName('Large').IsChecked;opacity=$window.FindName('OpacitySlider').Value;cardOrder=@($cards.Children | ForEach-Object {$_.Tag});names=$script:nameOverrides} | ConvertTo-Json -Depth 4
    $temp=$script:settingsPath+'.tmp'
    [IO.File]::WriteAllText($temp,$settings)
    if([IO.File]::Exists($script:settingsPath)){
        [IO.File]::Replace($temp,$script:settingsPath,[System.Management.Automation.Language.NullString]::Value)
    }else{[IO.File]::Move($temp,$script:settingsPath)}
}
function Move-Card($card,[int]$delta) {
    if($script:positionLocked){return}
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
        $entry[0].IsEnabled=(-not $script:positionLocked -and $i -gt 0)
        $entry[1].IsEnabled=(-not $script:positionLocked -and $i -lt ($cards.Children.Count-1))
    }
}
Add-Type -Path "$PSScriptRoot\CardDrag.cs" -ReferencedAssemblies @('PresentationFramework','PresentationCore','WindowsBase','System.Xaml')
$script:orderButtons=@{}
$script:cardGrips=@()
$titles=@('CPU','GPU','Memory','NVMe','Airflow')
for($i=0;$i -lt $cards.Children.Count;$i++){
    $card=$cards.Children[$i];$card.Tag=$titles[$i]
        $drag=[Windows.Controls.Primitives.Thumb]::new();$drag.Width=16;$drag.Height=20;$drag.Margin='0,0,6,0';$drag.Cursor='SizeAll';$drag.Focusable=$true
    $drag.ToolTip='Drag To Reorder (Esc To Cancel)'
    $script:cardGrips+=,$drag
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
. "$PSScriptRoot\CardVisibility.ps1"
Update-OrderButtons
'Preparing backdrop' | Set-Content "$script:stateRoot\glass-stage.txt"
function Update-Panel {
    if(Test-Path "$script:runtime\STOP"){
        if(-not $script:stopBlocked -or [IO.File]::GetLastWriteTimeUtc("$script:runtime\STOP").Ticks -ne $script:ignoredStopTime){$script:exitRequested=$true;$window.Close();return}
    }else{$script:stopBlocked=$false}
    $data=Get-PulseSnapshot "$script:runtime\snapshot.json"
    if($data.state -eq 'LIVE' -and $null -ne $data.available){
        $script:availableSensors=$data.available
        $script:availableUsage=$data.usage
    }
    if($data.names){foreach($key in $data.names.Keys){if($data.names[$key]){$script:autoNames[$key]=$data.names[$key]}};Update-DeviceNames}
    foreach($key in $script:usageCells.Keys){
        $cell=$script:usageCells[$key];$usage=if($data.state -eq 'LIVE'){$data.usage[$key]}else{$null}
        if($usage){
            $usageLabel=if($usage.label){$usage.label}else{$cell[3]}
            $cell[0].Text=('{0}  {1:F1} / {2:F1} GB · {3:F0}%' -f (Get-PulseText $usageLabel),$usage.used,$usage.total,$usage.percent)
            $cell[2].Width=[Math]::Max(0,$cell[1].ActualWidth*$usage.percent/100)
        }else{$cell[0].Text=(Get-PulseText $cell[3])+' —';$cell[2].Width=0}
        $cell[0].ToolTip=Get-PulseText 'Current used / usable capacity (GB, binary units). Usage stays live in Session Max.'
    }
    if($data.state -eq 'LIVE' -and $data.identity -ne $script:lastIdentity){
        foreach($key in $data.values.Keys){if(-not $script:peaks.ContainsKey($key) -or $data.values[$key] -gt $script:peaks[$key]){$script:peaks[$key]=$data.values[$key]}}
        $script:lastIdentity=$data.identity
    }
    $status.Text=if($data.state -eq 'LIVE'){'● '+(Get-PulseText 'Live')+' · '+$data.time.ToLocalTime().ToString('HH:mm:ss')+' · '+$data.values.Count+' '+(Get-PulseText 'sensors')}else{'● '+(Get-PulseText $data.state)+' · '+(Get-PulseText 'Waiting for collector')}
    if($script:mode -eq 'max'){$status.Text+=' · '+(Get-PulseText 'Session peaks')}
    if($script:collectorStartFailed -and $data.state -ne 'LIVE'){$status.Text=Get-PulseText 'Collector start failed; reinstall or check permissions'}
    $status.Foreground=if($script:lightTheme){[Windows.Media.BrushConverter]::new().ConvertFromString($(if($data.state -eq 'LIVE'){'#12644D'}else{'#804000'}))}else{if($data.state -eq 'LIVE'){[Windows.Media.Brushes]::Aquamarine}else{[Windows.Media.Brushes]::PeachPuff}}
    $values=if($script:mode -eq 'max'){$script:peaks}else{$data.values}
    foreach($key in $script:cells.Keys) {
        $cell=$script:cells[$key];$cell[0].Text='—'
        if($values.ContainsKey($key)){$format=if($cell[1] -eq 'V'){'{0:F3}'}elseif($cell[1] -eq 'RPM'){'{0:F0}'}else{'{0:F1}'};$cell[0].Text=($format -f $values[$key])+' '+$cell[1]}
    }
    $window.FindName('Live').Background=if($script:mode -eq 'live'){[Windows.Media.BrushConverter]::new().ConvertFromString('#607898A8')}else{[Windows.Media.Brushes]::Transparent}
    if($data.state -eq 'LIVE'){$script:gpuFanCount=$data.gpuFanCount}
    $script:gpuFanLabel.Text=Get-PulseText 'Fan Speed'
    if($script:gpuFanCount -gt 1){
        $first=if($values.ContainsKey('gpuFan')){'{0:F0}' -f $values.gpuFan}else{'—'}
        $second=if($values.ContainsKey('gpuFan2')){'{0:F0}' -f $values.gpuFan2}else{'—'}
        $script:cells.gpuFan[0].Text=$first+' / '+$second+' RPM'
        $script:cells.gpuFan[0].ToolTip=Get-PulseText 'GPU Fan 1 / GPU Fan 2 telemetry channels. These do not count physical fans.'
    }
    $window.FindName('Max').Background=if($script:mode -eq 'max'){[Windows.Media.BrushConverter]::new().ConvertFromString('#607898A8')}else{[Windows.Media.Brushes]::Transparent}
    foreach($key in @('cpuFan','bottom','top')){
        $script:cells[$key][0].ToolTip=if($values.ContainsKey($key) -and $values[$key] -eq 0){Get-PulseText 'This channel reports 0 RPM; other fans or pumps may use separate channels.'}else{$null}
    }
    Update-CardVisibility
    @{updated=[DateTimeOffset]::Now.ToString('o');state=$data.state;mode=$script:mode;sensors=$data.values.Count} | ConvertTo-Json | Set-Content "$script:stateRoot\view-status.json"
}
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class PulseBackdrop {
 [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd,int attr,ref int value,int size);
 [StructLayout(LayoutKind.Sequential)] public struct Margins { public int Left,Right,Top,Bottom; }
 [DllImport("dwmapi.dll")] public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd,ref Margins margins);
 [StructLayout(LayoutKind.Sequential)] struct Blur { public uint Flags; public int Enabled; public IntPtr Region; public int Transition; }
 [StructLayout(LayoutKind.Sequential)] struct Accent { public int State,Flags,Color,Animation; }
 [StructLayout(LayoutKind.Sequential)] struct Composition { public int Attribute; public IntPtr Data; public IntPtr Size; }
 [DllImport("dwmapi.dll")] static extern int DwmEnableBlurBehindWindow(IntPtr hwnd,ref Blur value);
 [DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int left,int top,int right,int bottom);
 [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr value);
 [DllImport("user32.dll")] static extern bool SetWindowCompositionAttribute(IntPtr hwnd,ref Composition value);
 public static bool ApplyStable(IntPtr hwnd,bool solid,bool clear) {
   // Disable focus-dependent system Acrylic. Accent blur is optional and probed at runtime.
   int none=1;DwmSetWindowAttribute(hwnd,38,ref none,4);
   IntPtr region=CreateRectRgn(0,0,-1,-1), memory=IntPtr.Zero;
   if(region==IntPtr.Zero)return false;
   try {
     var blur=new Blur { Flags=3,Enabled=solid?0:1,Region=region };
     if(DwmEnableBlurBehindWindow(hwnd,ref blur)<0)return false;
     var accent=new Accent { State=(solid||clear)?0:3 };
     int size=Marshal.SizeOf(typeof(Accent));memory=Marshal.AllocHGlobal(size);
     Marshal.StructureToPtr(accent,memory,false);
     var data=new Composition { Attribute=19,Data=memory,Size=new IntPtr(size) };
     return SetWindowCompositionAttribute(hwnd,ref data);
   } catch(EntryPointNotFoundException) { return false; }
     finally { if(memory!=IntPtr.Zero)Marshal.FreeHGlobal(memory);DeleteObject(region); }
 }
}
'@
function Update-SurfaceOpacity {
    $factor=if($window.FindName('Solid').IsChecked -or [Windows.SystemParameters]::HighContrast -or -not $window.FindName('OpacitySlider').IsEnabled){1.0}else{$window.FindName('OpacitySlider').Value/100.0}
    if($script:positionLocked -and $window.FindName('SettingsPage').Visibility -ne 'Visible' -and -not $window.FindName('Solid').IsChecked -and -not [Windows.SystemParameters]::HighContrast){$factor*=0.25}
    $viewport=$window.FindName('Viewport')
    if($viewport.Background.IsFrozen){$viewport.Background=$viewport.Background.Clone()}
    $viewport.Background.Opacity=$factor
    foreach($card in $cards.Children){
        if($card.Background.IsFrozen){$card.Background=$card.Background.Clone()}
        $card.Background.Opacity=$factor
    }
}
function Set-Material {
    $solid=$window.FindName('Solid').IsChecked -or [Windows.SystemParameters]::HighContrast
    $hwnd=[Windows.Interop.WindowInteropHelper]::new($window).Handle
    if($hwnd -eq [IntPtr]::Zero){return}
    $dark=1;$round=2
    $lockedMonitor=$script:positionLocked -and $window.FindName('SettingsPage').Visibility -ne 'Visible'
    $clear=$lockedMonitor -or $window.FindName('OpacitySlider').Value -eq 0
    $result=if([PulseBackdrop]::ApplyStable($hwnd,[bool]$solid,[bool]$clear)){0}else{-1}
    $null=[PulseBackdrop]::DwmSetWindowAttribute($hwnd,20,[ref]$dark,4)
    $null=[PulseBackdrop]::DwmSetWindowAttribute($hwnd,33,[ref]$round,4)
    $margins=New-Object PulseBackdrop+Margins
    $margins.Left=-1;$margins.Right=-1;$margins.Top=-1;$margins.Bottom=-1
    $null=[PulseBackdrop]::DwmExtendFrameIntoClientArea($hwnd,[ref]$margins)
    [Windows.Interop.HwndSource]::FromHwnd($hwnd).CompositionTarget.BackgroundColor=[Windows.Media.Colors]::Transparent
    $opacity=$window.FindName('OpacitySlider').Value
    if($lockedMonitor -and -not $solid){$opacity*=0.25}
    $alpha=[byte][Math]::Round($opacity*255/100)
    $window.FindName('OpacitySlider').IsEnabled=(-not $solid -and $result -eq 0)
    $window.FindName('OpacitySlider').ToolTip=if($result -ne 0){Get-PulseText 'System glass background is unavailable on this Windows version.'}else{Get-PulseText 'Background Opacity'}
    $window.FindName('OpacityValue').Text=if($solid -or $result -ne 0){'100%'}else{([Math]::Round($opacity)).ToString()+'%'}
    $base=if($script:backgroundHex){[Windows.Media.ColorConverter]::ConvertFromString($script:backgroundHex)}else{[Windows.Media.ColorConverter]::ConvertFromString('#35383B')}
    $window.Background=if($solid -or $result -ne 0){[Windows.Media.SolidColorBrush]::new($base)}else{[Windows.Media.SolidColorBrush]::new([Windows.Media.Color]::FromArgb($alpha,$base.R,$base.G,$base.B))}
    Update-SurfaceOpacity
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
    Update-CardVisibility
    Set-Material
}
$window.FindName('Settings').Add_Click({Show-Settings $true})
$window.FindName('Back').Add_Click({Show-Settings $false})
$window.FindName('GitHub').Add_Click({Start-Process 'https://github.com/medking82/hardware-pulse'})
$window.Add_PreviewKeyDown({if($_.Key -eq 'Escape' -and $window.FindName('SettingsPage').IsVisible){Show-Settings $false;$_.Handled=$true}})
$window.FindName('Live').Add_Click({$script:mode='live';Update-Panel})
$window.FindName('Max').Add_Click({$script:mode='max';Update-Panel})
$window.FindName('Details').Add_Click({$script:showDetails=-not $script:showDetails;Update-CardDensity -Animate;Save-WidgetSettings})
$window.FindName('CardScroll').Add_SizeChanged({Update-CardDensity})
$window.FindName('Pin').Add_Click({$window.Topmost=[bool]$window.FindName('Pin').IsChecked})
$window.FindName('Solid').Add_Click({Set-Material})
$window.FindName('OpacitySlider').Add_ValueChanged({Set-Material})
$window.FindName('FontSizeSlider').Add_ValueChanged({$window.FontSize=$window.FindName('FontSizeSlider').Value;$window.FindName('FontSizeValue').Text=$window.FontSize.ToString()+' px';Update-CardDensity;Save-WidgetSettings})
$window.FindName('Large').Add_Click({$window.FontSize=if($window.FindName('Large').IsChecked){14}else{12};Update-CardDensity})
$window.Add_SizeChanged({
    Update-CardDensity
})
$window.FindName('Minimize').Add_Click({$window.WindowState='Minimized'})
$window.FindName('Close').Add_Click({$window.Close()})
$window.FindName('DragHandle').Add_MouseLeftButtonDown({if(-not $script:positionLocked -and $_.ButtonState -eq 'Pressed'){$window.DragMove()}})
Add-Type -Path "$PSScriptRoot\WindowSnap.cs" -ReferencedAssemblies @('PresentationFramework','PresentationCore','WindowsBase','System.Xaml')
$script:positionLocked=[bool]$saved.positionLocked
function Update-PositionLock {
    $window.SetValue([WindowSnap]::PositionLockedProperty,$script:positionLocked)
    $window.ResizeMode=if($script:positionLocked){'NoResize'}else{'CanResizeWithGrip'}
    $window.FindName('DragHandle').Cursor=if($script:positionLocked){'Arrow'}else{'SizeAll'}
    $window.FindName('LockPosition').IsChecked=$script:positionLocked
    if($script:trayLock){$script:trayLock.Checked=$script:positionLocked}
    foreach($grip in $script:cardGrips){$grip.IsEnabled=(-not $script:positionLocked);$grip.Opacity=if($script:positionLocked){0.0}else{1.0}}
    Update-OrderButtons
    Set-Material
}
$window.FindName('LockPosition').Add_Click({$script:positionLocked=[bool]$window.FindName('LockPosition').IsChecked;Update-PositionLock;Save-WidgetSettings})
Update-PositionLock
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
$script:nameEditorTitles=@{}
foreach($pair in @(@('CPU','CPU Name'),@('GPU','GPU Name'),@('Memory','Memory Details'),@('ramA','Module 1 Label'),@('ramB','Module 2 Label'),@('NVMe','NVMe Details'),@('diskC','Drive 1 Name'),@('diskD','Drive 2 Name'),@('Airflow','Case / Motherboard'),@('cpuFan','CPU Fan Name'),@('bottom','System Fan 1'),@('top','System Fan 2'))){
    $script:nameEditorTitles[$pair[0]]=$pair[1]
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
$window.Add_Closing({param($sender,$eventArgs) Save-WidgetSettings;if(-not $script:exitRequested){$eventArgs.Cancel=$true;$window.Hide()}})
$window.Add_Closed({
    if($script:runningLoop){[Windows.Threading.Dispatcher]::CurrentDispatcher.BeginInvokeShutdown([Windows.Threading.DispatcherPriority]::Background)}
    if($updateTimer){$updateTimer.Stop()};if($script:updater){$script:updater.CancelDownload()}
    if($script:tray){$script:tray.Visible=$false;$script:tray.Dispose()}
    if($overlayTimer){$overlayTimer.Stop();$script:frameCapture.Dispose();$script:gameOverlay.Close()}
    try{if(Test-Path "$script:runtime\snapshot.json"){[IO.File]::WriteAllText("$script:runtime\STOP",'Pulse Exit')}}catch{[IO.File]::WriteAllText("$script:stateRoot\shutdown-error.txt",'Collector shutdown request failed. Check runtime permissions.')}
    $settingsTimer.Stop()
    $timer.Stop()
    Save-WidgetSettings
})
$script:localizedControls=[Collections.Generic.List[object]]::new()
. "$PSScriptRoot\Overlay.ps1"
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$script:tray=[Windows.Forms.NotifyIcon]::new();$script:tray.Icon=[Drawing.Icon]::new((Join-Path $PSScriptRoot 'assets\pulse.ico'));$script:tray.Text='Hardware Pulse';$script:tray.Visible=$true
$trayMenu=[Windows.Forms.ContextMenuStrip]::new()
$script:trayShow=$trayMenu.Items.Add('Show Pulse')
$script:traySettings=$trayMenu.Items.Add('Settings')
$null=$trayMenu.Items.Add([Windows.Forms.ToolStripSeparator]::new())
$script:trayPin=$trayMenu.Items.Add('Always on Top');$script:trayPin.CheckOnClick=$true
$script:trayLock=$trayMenu.Items.Add('Lock Position and Size');$script:trayLock.CheckOnClick=$true
$null=$trayMenu.Items.Add([Windows.Forms.ToolStripSeparator]::new())
$script:trayExit=$trayMenu.Items.Add('Exit')
$trayMenu.Add_Opening({$script:trayPin.Checked=$window.Topmost;$script:trayLock.Checked=$script:positionLocked})
$script:traySettings.Add_Click({$window.Show();$window.WindowState='Normal';$null=$window.Activate();Show-Settings $true})
$script:trayPin.Add_Click({$window.Topmost=$script:trayPin.Checked;$window.FindName('Pin').IsChecked=$window.Topmost;Save-WidgetSettings})
$script:trayLock.Add_Click({$script:positionLocked=$script:trayLock.Checked;Update-PositionLock;Save-WidgetSettings})
$restorePulse={$window.Show();$window.WindowState='Normal';$null=$window.Activate()}
$script:trayShow.Add_Click($restorePulse);$script:tray.Add_DoubleClick($restorePulse)
$script:trayExit.Add_Click({$script:exitRequested=$true;$window.Close()})
$script:tray.ContextMenuStrip=$trayMenu
. "$PSScriptRoot\Preferences.ps1"
Register-PulseText $window
$languagePicker=$window.FindName('LanguagePicker')
foreach($item in $languagePicker.Items){if($item.Tag -eq $script:languagePreference){$languagePicker.SelectedItem=$item}}
$languagePicker.Add_SelectionChanged({
    if($languagePicker.SelectedItem){
        $script:languagePreference=[string]$languagePicker.SelectedItem.Tag
        $script:language=Resolve-PulseLanguage $script:languagePreference
        Update-PulseLanguage
        if($window.IsLoaded){$settingsTimer.Stop();$settingsTimer.Start()}
    }
})
function Start-PulseLoop {
    $script:runningLoop=$true
    $window.Show()
    [Windows.Threading.Dispatcher]::Run()
}
try{Update-PulseLanguage;'Showing window' | Set-Content "$script:stateRoot\glass-stage.txt";$null=Start-PulseLoop}finally{$mutex.ReleaseMutex();$mutex.Dispose()}
