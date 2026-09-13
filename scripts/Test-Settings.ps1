$ErrorActionPreference='Stop'
# Exercise the real WPF bindings with isolated state, without closing the user's widget.
$root=Split-Path $PSScriptRoot
$testRoot=Join-Path $root ('vendor/settings-test-'+[Guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path $testRoot
Copy-Item "$root/src/*" $testRoot
Copy-Item "$root/assets" $testRoot -Recurse
@'
$script:stateRoot=$PSScriptRoot
$script:runtime=Join-Path $PSScriptRoot 'runtime'
'@ | Set-Content "$testRoot/Paths.ps1"
$scriptText=[IO.File]::ReadAllText("$testRoot/Glass.ps1")
$scriptText=$scriptText.Replace('Local\HardwarePulseGlass','Local\HardwarePulseSettingsTest')
$exercise=@'
$window.ShowInTaskbar=$false
$window.ShowActivated=$false
$window.Show()
$gear=$window.FindName('Settings').Content.Child.Children[0]
$iconBounds=$gear.Data.GetRenderBounds([Windows.Media.Pen]::new([Windows.Media.Brushes]::White,$gear.StrokeThickness))
if($iconBounds.Left -lt 0 -or $iconBounds.Top -lt 0 -or $iconBounds.Right -gt 24 -or $iconBounds.Bottom -gt 24){throw 'Gear SVG strokes exceed the viewBox'}
Show-Settings $true
if($window.FindName('CardScroll').Visibility -ne 'Collapsed' -or $window.FindName('SettingsPage').Visibility -ne 'Visible'){throw 'Settings navigation failed'}
Show-Settings $false
if($window.FindName('SettingsPage').Visibility -ne 'Collapsed' -or $window.FindName('CardScroll').Visibility -ne 'Visible'){throw 'Monitor navigation failed'}
function Pump {
    $frame=New-Object Windows.Threading.DispatcherFrame
    $stop=New-Object Windows.Threading.DispatcherTimer
    $stop.Interval=[TimeSpan]::FromMilliseconds(1200)
    $stop.Add_Tick({$stop.Stop();$frame.Continue=$false}.GetNewClosure())
    $stop.Start()
    [Windows.Threading.Dispatcher]::PushFrame($frame)
}
$window.Width=310;$window.Height=690;$window.Left=90;$window.Top=70
$window.FindName('Large').IsChecked=$true
$window.FindName('Pin').IsChecked=$true;$window.Topmost=$true
$window.FindName('Solid').IsChecked=$true
$window.FindName('OpacitySlider').Value=37
$script:nameEditors['cpuFan'].Text='Custom '+[char]0x00B7+' Cooler'
$idsBefore=@($cards.Children | ForEach-Object {$_.Tag}) -join ','
function Capture-TestView([string]$name){
    $window.UpdateLayout()
    $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new([int]$window.ActualWidth,[int]$window.ActualHeight,96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($window)
    $encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new();$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream=[IO.File]::Create((Join-Path $PSScriptRoot ($name+'.png')))
    try{$encoder.Save($stream)}finally{$stream.Dispose()}
}
foreach($language in @('zh-CN','zh-TW','en','zh-TW')){
    $window.FindName('LanguagePicker').SelectedItem=@($window.FindName('LanguagePicker').Items | Where-Object {$_.Tag -eq $language})[0]
    if($window.FindName('Live').Content -ne (Get-PulseText 'Live')){throw 'Live label did not switch language'}
    if($window.FindName('Solid').Content -ne (Get-PulseText 'Solid Background')){throw 'Settings label did not switch language'}
    if($script:orderButtons.CPU[0].Header -ne (Get-PulseText 'Move Up')){throw 'Context menu did not switch language'}
    if($script:labels.cpuFan.Text -ne ('Custom '+[char]0x00B7+' Cooler')){throw 'Language switch changed a custom name'}
    if((@($cards.Children | ForEach-Object {$_.Tag}) -join ',') -ne $idsBefore){throw 'Language switch changed card IDs or order'}
    $window.Width=240;$window.UpdateLayout()
    Show-Settings $true
    Capture-TestView ($language+'-settings-240')
    Show-Settings $false
    Capture-TestView ($language+'-monitor-240')
    $window.Width=310
}
if($window.FindName('Live').Content -ne '即時'){throw 'Traditional Chinese text mismatch'}
if((Resolve-PulseLanguage 'unknown') -ne 'en' -or (Get-PulseText 'Unknown Device Model') -ne 'Unknown Device Model'){throw 'Language fallback failed'}
Pump
if(-not(Test-Path $script:settingsPath)){throw 'Settings were not saved while window remained open'}
$saved=Get-Content $script:settingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
if($saved.width -ne 310 -or $saved.height -ne 690 -or $saved.left -ne 90 -or $saved.top -ne 70){throw 'Geometry autosave failed'}
if(-not $saved.large -or -not $saved.pin -or -not $saved.solid -or $saved.opacity -ne 37){throw 'Preference autosave failed'}
if($saved.language -ne 'zh-TW'){throw 'Language autosave failed'}
if($saved.names.cpuFan -ne ('Custom '+[char]0x00B7+' Cooler') -or $script:labels.cpuFan.Text -ne $saved.names.cpuFan){throw 'Custom hardware name autosave failed'}
$window.Width=320
Pump
$saved=Get-Content $script:settingsPath -Raw | ConvertFrom-Json
if($saved.width -ne 320){throw 'Atomic replacement of existing settings failed'}
$window.Close()
'PASS: live WPF geometry and preference autosave before close, and atomic replacement'
'@
$scriptText=$scriptText.Replace('$null=$window.ShowDialog()',$exercise)
[IO.File]::WriteAllText("$testRoot/Glass.ps1",$scriptText,[Text.UTF8Encoding]::new($true))
& "$PSScriptRoot/Run-Hidden.ps1" "$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe" @('-NoProfile','-STA','-File',"$testRoot/Glass.ps1") $testRoot
if(Test-Path "$testRoot/glass-error.txt"){throw (Get-Content "$testRoot/glass-error.txt" -Raw)}
$restore=@'
if($script:language -ne 'zh-TW' -or $window.FindName('Live').Content -ne (Get-PulseText 'Live')){throw 'Language restore failed'}
if($window.Width -ne 320 -or $window.Height -ne 690 -or $window.Left -ne 90 -or $window.Top -ne 70){throw 'Geometry restore failed'}
if(-not $window.FindName('Large').IsChecked -or $window.FontSize -ne 14 -or -not $window.Topmost -or -not $window.FindName('Solid').IsChecked -or $window.FindName('OpacitySlider').Value -ne 37){throw 'Preference restore failed'}
if($script:labels.cpuFan.Text -ne ('Custom '+[char]0x00B7+' Cooler')){throw 'Unicode hardware name restore failed'}
$window.Close()
'PASS: geometry and preferences restored in a new process'
'@
$scriptText=$scriptText.Replace($exercise,$restore)
[IO.File]::WriteAllText("$testRoot/Glass.ps1",$scriptText,[Text.UTF8Encoding]::new($true))
& "$PSScriptRoot/Run-Hidden.ps1" "$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe" @('-NoProfile','-STA','-File',"$testRoot/Glass.ps1") $testRoot
if(Test-Path "$testRoot/glass-error.txt"){throw (Get-Content "$testRoot/glass-error.txt" -Raw)}
