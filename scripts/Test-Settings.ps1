$ErrorActionPreference='Stop'
# Exercise the real WPF bindings with isolated state, without closing the user's widget.
$root=Split-Path $PSScriptRoot
$testRoot=Join-Path $root ('vendor/settings-test-'+[Guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path $testRoot
Copy-Item "$root/src/*" $testRoot
Copy-Item "$root/assets" $testRoot -Recurse
Copy-Item "$root/src/Core" $testRoot -Recurse -Force
@'
$script:stateRoot=$PSScriptRoot
$script:runtime=Join-Path $PSScriptRoot 'runtime'
'@ | Set-Content "$testRoot/Paths.ps1"
$scriptText=[IO.File]::ReadAllText("$testRoot/Glass.ps1")
$scriptText=$scriptText.Replace('Local\HardwarePulseGlass','Local\HardwarePulseSettingsTest')
$exercise=@'
$window.ShowInTaskbar=$false
$script:exitRequested=$true
$window.ShowActivated=$false
$window.Show()
$window.UpdateLayout()
if($overlayTimer.IsEnabled){throw 'Unused overlay timer wakes the app'}
$overlayTimer.Start();Stop-Overlay
if($overlayTimer.IsEnabled){throw 'Stopping overlay leaves timer running'}
$script:trayLock.PerformClick()
if(-not $script:positionLocked -or $window.ResizeMode -ne 'NoResize' -or -not $window.GetValue([WindowSnap]::PositionLockedProperty)){throw 'Tray lock did not disable movement and resizing'}
if(@($script:cardGrips | Where-Object IsEnabled).Count){throw 'Locked cards can still be dragged'}
$lockedOrder=@($cards.Children | ForEach-Object Tag) -join ','
Move-Card $cards.Children[0] 1
if((@($cards.Children | ForEach-Object Tag) -join ',') -ne $lockedOrder){throw 'Locked card order changed'}
$script:trayLock.PerformClick()
if($script:positionLocked -or $window.ResizeMode -ne 'CanResizeWithGrip'){throw 'Tray unlock failed'}
if(@($script:cardGrips | Where-Object {-not $_.IsEnabled}).Count){throw 'Unlocked cards cannot be dragged'}
foreach($pair in @(@('zh-HK','zh-TW'),@('zh-CN','zh-CN'),@('en-US','en'),@('de-DE','en'))){if((Get-PulseSystemLanguage $pair[0]) -ne $pair[1]){throw 'System language selection failed'}}
$settingsButton=$window.FindName('Settings')
$iconCenter=$settingsButton.Content.TranslatePoint([Windows.Point]::new(10,10),$settingsButton)
if([Math]::Abs($iconCenter.X-$settingsButton.ActualWidth/2) -gt 0.7 -or [Math]::Abs($iconCenter.Y-$settingsButton.ActualHeight/2) -gt 0.7){throw 'Settings icon is not centered'}
$window.FindName('Solid').IsChecked=$false
$window.FindName('OpacitySlider').Value=0;Set-Material
if($window.FindName('OpacitySlider').IsEnabled){
    if($window.FindName('Viewport').Background.Opacity -ne 0 -or @($cards.Children | Where-Object {$_.Background.Opacity -ne 0}).Count){throw 'Background layers ignore zero opacity'}
    if($window.Opacity -ne 1){throw 'Background opacity faded foreground content'}
}
$gear=$window.FindName('Settings').Content.Child.Children[0]
$iconBounds=$gear.Data.GetRenderBounds([Windows.Media.Pen]::new([Windows.Media.Brushes]::White,$gear.StrokeThickness))
if($iconBounds.Left -lt 0 -or $iconBounds.Top -lt 0 -or $iconBounds.Right -gt 24 -or $iconBounds.Bottom -gt 24){throw 'Gear SVG strokes exceed the viewBox'}
Show-Settings $true
if($window.FindName('CardScroll').Visibility -ne 'Collapsed' -or $window.FindName('SettingsPage').Visibility -ne 'Visible'){throw 'Settings navigation failed'}
Show-Settings $false
if($window.FindName('SettingsPage').Visibility -ne 'Collapsed' -or $window.FindName('CardScroll').Visibility -ne 'Visible'){throw 'Monitor navigation failed'}
$script:exitRequested=$false
$window.Close()
if($window.IsVisible -or -not $script:tray.Visible){throw 'Close did not hide to tray'}
$script:trayShow.PerformClick()
if(-not $window.IsVisible){throw 'Tray restore failed'}
$script:exitRequested=$true
$metrics=[FrameCapture]::new();$metrics.Reset(42)
for($i=0;$i -lt 99;$i++){$metrics.Add('main',10,10)}
$metrics.Add('main',100,10)
$result=$metrics.ReadAt(10)
if(-not $result.Ready -or $result.Minimum -ne 10 -or $result.Low -ne 10 -or [Math]::Abs($result.Average-(100000/1090)) -gt .001){throw 'FPS aggregate definitions failed'}
$metrics.Add('other',1,10)
if($metrics.ReadAt(10).Count -ne 100){throw 'Swapchains mixed'}
if($metrics.ReadAt(13).Ready){throw 'Stale FPS displayed'}
$metrics.Dispose()
$metrics.Reset(42);$metrics.Feed('Application,ProcessID,SwapChainAddress,MsBetweenPresents');$metrics.Feed('"Game, Demo.exe",42,0x1,16.0');$metrics.Feed('Other.exe,43,0x1,1.0');$metrics.Feed('Game.exe,42,0x1,NaN')
if($metrics.Read().Count -ne 1){throw 'CSV PID/invalid frame filtering failed'}
$metrics.Dispose()
$rect=[GameOverlay+Rect]::new();$rect.Left=-1920;$rect.Top=0;$rect.Right=0;$rect.Bottom=1080
foreach($position in @('top-left','top','top-right','bottom-left','bottom','bottom-right')){
    $point=[GameOverlay]::Anchor($rect,300,80,$position)
    if($point.X -lt -1920 -or $point.X+300 -gt 0 -or $point.Y -lt 0 -or $point.Y+80 -gt 1080){throw 'Overlay anchor outside target'}
}
'PASS: tray close/restore, FPS math/filtering/staleness and six overlay anchors'
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
foreach($screen in @(@('1080p-100',1920,1040,1),@('1080p-125',1920,1030,1.25),@('1080p-150',1920,1020,1.5),@('1440p-125',2560,1390,1.25),@('1440p-150',2560,1380,1.5),@('4k-150',3840,2100,1.5))){
    foreach($cell in $script:cells.Values){$cell[0].Text=switch($cell[1]){'RPM'{'1234 RPM'}'V'{'1.125 V'}'%'{'99.0 %'}default{'65.2 °C'}}}
    $workWidth=$screen[1]/$screen[3];$workHeight=$screen[2]/$screen[3]
    $size=Get-PulseInitialSize $workWidth $workHeight
    if($size.width -gt $workWidth -or $size.height -gt $workHeight -or $size.height -lt 340){throw 'Initial window does not fit logical work area'}
    $window.Width=$size.width;$window.Height=$size.height;$window.UpdateLayout()
    $scroll=$window.FindName('CardScroll')
    if($scroll.ExtentWidth -gt $scroll.ViewportWidth+1){throw 'Horizontal card overflow'}
    if($scroll.ScrollableHeight -gt 1){throw ('Overview requires scrolling for '+$screen[0]+': '+$scroll.ScrollableHeight)}
    $footer=$window.FindName('Settings').TranslatePoint([Windows.Point]::new(0,0),$window)
    if($footer.Y+$window.FindName('Settings').ActualHeight -gt $window.ActualHeight){throw 'Settings footer not reachable'}
    Capture-TestView $screen[0]
}
$window.Width=310;$window.Height=690
'PASS: initial logical work-area layouts at 1080p, 1440p and 4K with 100/125/150 percent scaling inputs'
$script:showDetails=$true;Update-CardDensity;$window.UpdateLayout()
if($script:labels.CPU.Visibility -ne 'Visible' -or $script:cells.vcore[0].Parent.Visibility -ne 'Visible'){throw 'Details did not restore full readings'}
$detailWidth=$cards.ActualWidth
if($window.FindName('Details').Background.Color.A -eq 0){throw 'Details lacks selected state'}
$window.Height=450;$window.UpdateLayout();Update-CardDensity;$window.UpdateLayout()
if($script:densityLevel -eq 0 -or $script:cells.vcore[0].Parent.Visibility -ne 'Visible'){throw 'Details must tighten spacing without hiding readings'}
$scroll=$window.FindName('CardScroll');$scroll.ScrollToBottom();$window.UpdateLayout()
if($scroll.VerticalOffset -le 0){throw 'Custom monitor scrollbar cannot scroll'}
$scroll.ScrollToTop();$window.Height=690
$script:showDetails=$false;Update-CardDensity
$window.UpdateLayout()
if([Math]::Abs($cards.ActualWidth-$detailWidth) -gt 1){throw 'Details changes card width'}
if($script:compactGroups[0].grid.Visibility -ne 'Visible' -or $window.FindName('Details').Background.Color.A -ne 0){throw 'Compact mode/state not restored'}
$window.Height=900;$window.UpdateLayout();Update-CardDensity;$window.UpdateLayout()
if($script:labels.CPU.Visibility -ne 'Visible'){throw ('Spacious auto layout: level='+$script:densityLevel+' card='+$cards.DesiredSize.Height+' viewport='+$window.FindName('CardScroll').ActualHeight+' win='+$window.ActualHeight)}
$fullHeight=$cards.DesiredSize.Height
$window.Height=650;$window.UpdateLayout();Update-CardDensity;$window.UpdateLayout()
if($cards.DesiredSize.Height -gt $window.FindName('CardScroll').ActualHeight+1){throw 'Measured auto layout does not fit'}
Capture-TestView 'density-tight'
$window.Height=900;$window.UpdateLayout();Update-CardDensity;$window.UpdateLayout()
if($script:labels.CPU.Visibility -ne 'Visible' -or $script:densityLevel -ne 0){throw 'Growing window did not restore full information'}
'PASS: density measures available space and restores hardware details as the window grows'
$script:language='zh-CN'
if((Get-PulseDeviceText 'Pump Fan') -ne '水泵转速' -or (Get-PulseDeviceText 'System Fan #1') -ne '系统风扇 #1'){throw 'Cooling channel translation failed'}
if((Get-PulseDeviceText 'Demo · System Temperature') -ne 'Demo · 系统温度'){throw 'Generated temperature descriptor not translated'}
if((Get-PulseDeviceText '32 GB DDR5-6000 configured · Slots A2 / B2') -ne '32 GB DDR5-6000 已配置 · 槽位 A2 / B2'){throw 'Memory metadata translation failed'}
if((Get-PulseText 'VRAM Junction') -ne '显存温度'){throw 'VRAM wording regression'}
$script:language='zh-TW'
if((Get-PulseDeviceText 'Bottom Intake · SYS1') -ne '底部進風 · SYS1'){throw 'Traditional fan descriptor not translated'}
$script:language='en'
if((Get-PulseDeviceText 'Demo · Composite Temperature') -ne 'Demo · Composite Temperature'){throw 'English descriptor changed'}
'PASS: generated hardware descriptors and VRAM terminology in three languages'
Capture-TestView 'density-spacious'
$window.Height=690;$window.UpdateLayout()
foreach($language in @('zh-CN','zh-TW','en','zh-TW')){
    $window.FindName('LanguagePicker').SelectedItem=@($window.FindName('LanguagePicker').Items | Where-Object {$_.Tag -eq $language})[0]
    if($window.FindName('Live').Content -ne (Get-PulseText 'Live')){throw 'Live label did not switch language'}
    if($window.FindName('Solid').Content -ne (Get-PulseText 'Solid Background')){throw 'Settings label did not switch language'}
    if($script:orderButtons.CPU[0].Header -ne (Get-PulseText 'Move Up')){throw 'Context menu did not switch language'}
    if($script:labels.cpuFan.Text -ne ('Custom '+[char]0x00B7+' Cooler')){throw 'Language switch changed a custom name'}
    if((@($cards.Children | ForEach-Object {$_.Tag}) -join ',') -ne $idsBefore){throw 'Language switch changed card IDs or order'}
    $window.Width=240;$window.UpdateLayout()
    if(-not $window.FindName('Viewport').LayoutTransform.Value.IsIdentity){throw 'Narrow window scales text instead of reflowing layout'}
    Show-Settings $true
    $settingsScroll=$window.FindName('SettingsPage');$window.UpdateLayout()
    $scrollBar=$settingsScroll.Template.FindName('PART_VerticalScrollBar',$settingsScroll)
    $scrollBar.ApplyTemplate() | Out-Null
    $track=$scrollBar.Template.FindName('PART_Track',$scrollBar)
    $track.Thumb.ApplyTemplate() | Out-Null
    if(-not $track.Thumb.Template.FindName('ThumbBody',$track.Thumb) -or $scrollBar.Width -ne 12){throw 'Custom scrollbar template not active'}
    $settingsScroll.ScrollToVerticalOffset(30);$window.UpdateLayout()
    if($settingsScroll.VerticalOffset -le 0){throw 'Settings scrolling unavailable'}
    $settingsScroll.ScrollToTop();$window.UpdateLayout()
    Capture-TestView ($language+'-settings-240')
    Show-Settings $false
    Capture-TestView ($language+'-monitor-240')
    $window.FontSize=14;$window.UpdateLayout()
    $controls=$window.FindName('MonitorControls')
    foreach($buttonName in @('Live','Max','Details')){
        $button=$window.FindName($buttonName)
        $point=$button.TranslatePoint([Windows.Point]::new(0,0),$controls)
        if($point.X+$button.ActualWidth -gt $controls.ActualWidth+1){throw 'Large-text monitor control clipped at 240 DIP'}
    }
    Capture-TestView ($language+'-large-monitor-240')
    $window.FontSize=12;$window.UpdateLayout()
    $window.Width=310
}
if($window.FindName('Live').Content -ne '即時'){throw 'Traditional Chinese text mismatch'}
if((Resolve-PulseLanguage 'unknown') -ne 'en' -or (Get-PulseText 'Unknown Device Model') -ne 'Unknown Device Model'){throw 'Language fallback failed'}
$firstCard=$cards.Children[0]
$script:dragSaved=0
$gesture=[CardDrag+Gesture]::new($cards,$firstCard,[Action]{$script:dragSaved++},$false)
$gesture.Begin(12);$gesture.Move(5000)
if($cards.Children[0] -ne $firstCard){throw 'Preview changed persisted card order'}
if($cards.Children[1].RenderTransform.Children[1].Y -ge 0){throw 'Neighbors did not make space'}
$gesture.Complete($true)
if($script:dragSaved -ne 0 -or $cards.Children[0] -ne $firstCard){throw 'Cancel committed a drag'}
foreach($card in $cards.Children){if($card.RenderTransform.Children[1].Y -ne 0){throw 'Reduced-motion cancel left an offset'}}
$gesture.Begin(12);$gesture.Move(5000);$gesture.Complete($false)
if($cards.Children[4] -ne $firstCard -or $script:dragSaved -ne 1){throw 'Drop did not commit exactly once'}
$gesture=[CardDrag+Gesture]::new($cards,$firstCard,[Action]{$script:dragSaved++},$true)
$start=[Windows.Controls.Primitives.LayoutInformation]::GetLayoutSlot($firstCard).Top+12
$gesture.Begin($start);$gesture.Move(-5000);$gesture.Complete($false)
# Re-grab during the settling animation and cancel without losing the committed order.
$gesture.Begin(12);$gesture.Move(60);$gesture.Complete($true)
Pump
if($cards.Children[0] -ne $firstCard -or $script:dragSaved -ne 2){throw 'Interrupted settling lost order'}
foreach($card in $cards.Children){if([Math]::Abs($card.RenderTransform.Children[1].Y) -gt 0.01){throw 'Animation did not settle'}}
'PASS: reorder preview, cancel, reduced motion, commit and interrupted settling'
$visibilityOrder=@($cards.Children | ForEach-Object {$_.Tag}) -join ','
$script:cardChecks.NVMe.IsChecked=$false
$script:cardChecks.NVMe.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Primitives.ButtonBase]::ClickEvent))
$nvmeCard=@($cards.Children | Where-Object {$_.Tag -eq 'NVMe'})[0]
if($nvmeCard.Visibility -ne 'Collapsed' -or (@($cards.Children | ForEach-Object {$_.Tag}) -join ',') -ne $visibilityOrder){throw 'Hiding card removed or reordered it'}
foreach($key in @($script:cardsVisible.Keys)){$script:cardsVisible[$key]=$false}
Update-CardVisibility
if($window.FindName('CardsEmpty').Visibility -ne 'Visible'){throw 'All hidden cards need an empty state'}
Show-Settings $true
if($window.FindName('CardsEmpty').Visibility -ne 'Collapsed'){throw 'Empty state covers Settings'}
foreach($key in @($script:cardsVisible.Keys)){$script:cardsVisible[$key]=$true}
$script:cardsVisible.NVMe=$false;Show-Settings $false;Save-WidgetSettings
'PASS: card visibility toggles preserve order and all-hidden state retains Settings'
Pump
if(-not(Test-Path $script:settingsPath)){throw 'Settings were not saved while window remained open'}
$saved=Get-Content $script:settingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
if($saved.width -ne 310 -or $saved.height -ne 690 -or $saved.left -ne 90 -or $saved.top -ne 70){throw 'Geometry autosave failed'}
if(-not $saved.large -or -not $saved.pin -or -not $saved.solid -or $saved.opacity -ne 37){throw 'Preference autosave failed'}
if($saved.language -ne 'zh-TW'){throw 'Language autosave failed'}
if($saved.names.cpuFan -ne ('Custom '+[char]0x00B7+' Cooler') -or $script:labels.cpuFan.Text -ne $saved.names.cpuFan){throw 'Custom hardware name autosave failed'}
$window.Width=320
$window.FindName('OpacitySlider').Value=0
$script:backgroundHex='#F2F4F7';Set-Material;Apply-Theme
if($window.FindName('BackgroundColor').Foreground.Color.R -gt 60 -or $window.FindName('Live').Foreground.Color.R -gt 60){throw 'Light-theme buttons must retain dark labels'}
Show-Settings $true
Capture-TestView 'light-settings-240'
Show-Settings $false
$script:autoUpdates=$false
$script:language='en';Update-PulseLanguage
foreach($key in @('cpu','gpu','system')){$script:cells[$key][0].Text='49.5 °C'}
foreach($key in @('cpuFan','gpuFan','bottom','top')){$script:cells[$key][0].Text='1340 RPM'}
$slider=$window.FindName('FontSizeSlider')
foreach($size in @(10,12,16)){
    $slider.Value=$size
    foreach($width in @(240,310)){
        $window.Width=$width;$window.UpdateLayout();Update-CardDensity;$window.UpdateLayout()
        if($window.FontSize -ne $size){throw 'Font slider did not apply'}
        foreach($key in @('cpu','gpu','system')){
            $value=$script:cells[$key][0];$header=$value.Parent;$title=$header.Children[0]
            $titlePoint=$title.TranslatePoint([Windows.Point]::new(0,0),$header)
            $valuePoint=$value.TranslatePoint([Windows.Point]::new(0,0),$header)
            if($valuePoint.Y -lt $titlePoint.Y+$title.ActualHeight-1 -and $valuePoint.X -lt $titlePoint.X+$title.ActualWidth-1){throw "Card title overlaps value: $key at $size / $width"}
            if($valuePoint.X+$value.ActualWidth -gt $header.ActualWidth+1){throw "Card value clipped: $key at $size / $width"}
        }
    }
    Capture-TestView ("font-"+$size)
}
$window.Width=320;$slider.Value=14
$script:language='zh-TW';Update-PulseLanguage
Save-WidgetSettings
'PASS: font slider 10/12/16 and non-overlapping card headers at 240/310 DIP'
Pump
$saved=Get-Content $script:settingsPath -Raw | ConvertFrom-Json
if($saved.fontSize -ne 14){throw 'Font size persistence failed'}
if($saved.width -ne 320){throw 'Atomic replacement of existing settings failed'}
if($saved.opacity -ne 0 -or $saved.background -ne '#F2F4F7' -or $saved.autoUpdates){throw 'Appearance/update settings persistence failed'}
$null=New-Item -ItemType Directory -Path $script:runtime -Force
[IO.File]::WriteAllText((Join-Path $script:runtime 'snapshot.json'),'{}')
$script:availableSensors=@{cpu=$true;cpuLoad=$true;gpuLoad=$true;diskC=$true}
$script:availableUsage=@{ram=@{used=8;total=16};vram=@{used=1;total=8}}
$visibilityBefore=$script:cardsVisible.Clone()
$script:cardsVisible.NVMe=$true
Update-CardVisibility;$window.UpdateLayout()
if(@($cards.Children | Where-Object Tag -eq 'Airflow')[0].Visibility -ne 'Collapsed'){throw 'Unsupported fan card remains visible'}
if($script:cells.cpuFan[0].Parent.Visibility -ne 'Collapsed' -or $script:cells.ramA[0].Parent.Visibility -ne 'Collapsed' -or $script:cells.diskD[0].Parent.Visibility -ne 'Collapsed'){throw 'Unavailable sensor placeholders remain visible'}
if($script:cells.gpu[0].Visibility -ne 'Collapsed' -or $script:usageCells.vram[0].Parent.Visibility -ne 'Visible'){throw 'Load-only GPU or shared-memory display failed'}
if($script:cells.diskC[0].Parent.Parent.ColumnDefinitions[1].Width.Value -ne 0){throw 'Absent disk retains an empty column'}
Capture-TestView 'capability-laptop'
$script:availableSensors=@{}
foreach($key in $script:SensorMap.Keys){$script:availableSensors[$key]=$true}
Update-CardVisibility;$window.UpdateLayout()
if(@($cards.Children | Where-Object Tag -eq 'Airflow')[0].Visibility -ne 'Visible' -or $script:cells.diskD[0].Parent.Visibility -ne 'Visible'){throw 'Newly available sensors were not restored'}
$script:cardsVisible=$visibilityBefore
$script:availableSensors=$null;$script:availableUsage=$null
Update-CardVisibility
'PASS: capability-based laptop layout hides absent fans, DIMMs and second disk; detects returning sensors'
$stopPath=Join-Path $script:runtime 'STOP'
[IO.File]::WriteAllText($stopPath,'Old startup marker')
$script:stopBlocked=$true;$script:ignoredStopTime=[IO.File]::GetLastWriteTimeUtc($stopPath).Ticks
Update-Panel
if(-not $window.IsVisible){throw 'Old blocked startup marker closed window'}
[IO.File]::SetLastWriteTimeUtc($stopPath,[IO.File]::GetLastWriteTimeUtc($stopPath).AddSeconds(2))
Update-Panel
if($window.IsVisible){throw 'Fresh installer STOP ignored after startup access failure'}
'PASS: fresh installer stop request overrides an undeletable startup marker'
'PASS: live WPF geometry and preference autosave before close, and atomic replacement'
'@
$scriptText=$scriptText.Replace('$null=Start-PulseLoop',$exercise)
[IO.File]::WriteAllText("$testRoot/Glass.ps1",$scriptText,[Text.UTF8Encoding]::new($true))
& "$PSScriptRoot/Run-Hidden.ps1" "$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe" @('-NoProfile','-STA','-File',"$testRoot/Glass.ps1") $testRoot
if(Test-Path "$testRoot/glass-error.txt"){throw (Get-Content "$testRoot/glass-error.txt" -Raw)}
if(-not(Test-Path "$testRoot/runtime/STOP")){throw 'Exit did not request collector shutdown'}
$restore=@'
$script:exitRequested=$true
if($script:language -ne 'zh-TW' -or $window.FindName('Live').Content -ne (Get-PulseText 'Live')){throw 'Language restore failed'}
if($window.Width -ne 320 -or $window.Height -ne 690 -or $window.Left -ne 90 -or $window.Top -ne 70){throw 'Geometry restore failed'}
if(-not $window.FindName('Large').IsChecked -or $window.FontSize -ne 14 -or -not $window.Topmost -or -not $window.FindName('Solid').IsChecked -or $window.FindName('OpacitySlider').Value -ne 0 -or $script:backgroundHex -ne '#F2F4F7' -or $script:autoUpdates){throw 'Preference restore failed'}
if($script:labels.cpuFan.Text -ne ('Custom '+[char]0x00B7+' Cooler')){throw 'Unicode hardware name restore failed'}
if($script:cardsVisible.NVMe -or @($cards.Children | Where-Object {$_.Tag -eq 'NVMe'})[0].Visibility -ne 'Collapsed' -or -not $script:cardsVisible.CPU){throw 'Card visibility restore failed'}
$window.Close()
'PASS: geometry and preferences restored in a new process'
'@
$scriptText=$scriptText.Replace($exercise,$restore)
[IO.File]::WriteAllText("$testRoot/Glass.ps1",$scriptText,[Text.UTF8Encoding]::new($true))
& "$PSScriptRoot/Run-Hidden.ps1" "$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe" @('-NoProfile','-STA','-File',"$testRoot/Glass.ps1") $testRoot
if(Test-Path "$testRoot/glass-error.txt"){throw (Get-Content "$testRoot/glass-error.txt" -Raw)}
$lifecycle=@'
$window.ShowInTaskbar=$false
$window.ShowActivated=$false
$script:loopStep=0
$script:loopFailure=$null
$probe=[Windows.Threading.DispatcherTimer]::new()
$probe.Interval=[TimeSpan]::FromMilliseconds(250)
$probe.Add_Tick({
    try {
        $script:loopStep++
        switch($script:loopStep){
            1 {$window.Close();if($window.IsVisible){throw 'Close did not hide in running dispatcher'}}
            2 {if($window.IsVisible -or -not $script:tray.Visible){throw 'Hidden tray did not survive dispatcher tick'};$script:trayShow.PerformClick()}
            3 {if(-not $window.IsVisible){throw 'Dispatcher tray restore failed'};$probe.Stop();$script:trayExit.PerformClick()}
        }
    }catch{$script:loopFailure=$_.Exception.Message;$probe.Stop();$script:exitRequested=$true;$window.Close()}
})
$probe.Start()
Start-PulseLoop
if($script:loopFailure){throw $script:loopFailure}
if($script:loopStep -ne 3 -or $script:tray.Visible){throw 'Dispatcher did not complete explicit Exit'}
'PASS: real dispatcher survives close-to-tray and terminates on tray Exit'
'@
$scriptText=$scriptText.Replace($restore,$lifecycle)
[IO.File]::WriteAllText("$testRoot/Glass.ps1",$scriptText,[Text.UTF8Encoding]::new($true))
& "$PSScriptRoot/Run-Hidden.ps1" "$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe" @('-NoProfile','-STA','-File',"$testRoot/Glass.ps1") $testRoot
if(Test-Path "$testRoot/glass-error.txt"){throw (Get-Content "$testRoot/glass-error.txt" -Raw)}
