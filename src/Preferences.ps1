$script:backgroundHex=if($saved.background -match '^#[0-9A-Fa-f]{6}$'){[string]$saved.background}else{'#35383B'}
$script:autoUpdates=if($null -ne $saved.autoUpdates){[bool]$saved.autoUpdates}else{$false}
$script:themeControls=[Collections.Generic.List[object]]::new()
function Register-Theme($node){
    if($node -isnot [Windows.DependencyObject]){return}
    if($node -is [Windows.Controls.TextBlock] -or $node -is [Windows.Controls.Control]){
        $script:themeControls.Add(@($node,$node.Foreground))
    }
    foreach($child in [Windows.LogicalTreeHelper]::GetChildren($node)){Register-Theme $child}
}
function Apply-Theme {
    $color=[Windows.Media.ColorConverter]::ConvertFromString($script:backgroundHex)
    $brightness=(.2126*$color.R+.7152*$color.G+.0722*$color.B)/255
    $light=$brightness -gt .55
    $script:lightTheme=$light
    foreach($entry in $script:themeControls){
        $node=$entry[0]
        if($node -is [Windows.Controls.ComboBox] -or $node -is [Windows.Controls.ComboBoxItem]){continue}
        $node.Foreground=if($light){[Windows.Media.BrushConverter]::new().ConvertFromString('#17202B')}else{$entry[1]}
    }
    foreach($card in $cards.Children){$card.Background=[Windows.Media.BrushConverter]::new().ConvertFromString($(if($light){'#DDEEF1F4'}else{'#3031485B'}))}
}
$window.FindName('BackgroundColor').Add_Click({
    $dialog=[Windows.Forms.ColorDialog]::new();$dialog.FullOpen=$true;$dialog.Color=[Drawing.ColorTranslator]::FromHtml($script:backgroundHex)
    if($dialog.ShowDialog() -eq [Windows.Forms.DialogResult]::OK){
        $script:backgroundHex='#{0:X2}{1:X2}{2:X2}' -f $dialog.Color.R,$dialog.Color.G,$dialog.Color.B
        Set-Material;Apply-Theme;Save-WidgetSettings
    }
    $dialog.Dispose()
})
function Refresh-StartupState {
    try{
        $tasks=@(Get-ScheduledTask -TaskName 'Hardware Pulse Widget','Hardware Pulse Collector' -ErrorAction Stop)
        $expected=Join-Path $PSScriptRoot 'HardwarePulse.exe'
        if($tasks.Count -ne 2 -or @($tasks | Where-Object {$_.Actions.Execute -ne $expected}).Count){throw 'Not installed'}
        $window.FindName('StartWithWindows').IsEnabled=$true
        $window.FindName('StartWithWindows').IsChecked=@($tasks | Where-Object {-not $_.Settings.Enabled -or @($_.Triggers).Count -ne 1 -or -not $_.Triggers[0].Enabled}).Count -eq 0
        $window.FindName('StartupStatus').Text=''
    }catch{$window.FindName('StartWithWindows').IsEnabled=$false;$window.FindName('StartupStatus').Text=Get-PulseText 'Available after installation'}
}
$window.FindName('StartWithWindows').Add_Click({
    $wanted=[bool]$window.FindName('StartWithWindows').IsChecked
    try{
        $info=[Diagnostics.ProcessStartInfo]::new((Join-Path $PSScriptRoot 'HardwarePulse.exe'),$(if($wanted){'--enable-startup'}else{'--disable-startup'}))
        $info.UseShellExecute=$true;$info.Verb='runas';$info.WindowStyle='Hidden'
        $child=[Diagnostics.Process]::Start($info);$child.WaitForExit();$code=$child.ExitCode;$child.Dispose()
        Refresh-StartupState
        if($code -ne 0){$window.FindName('StartupStatus').Text=Get-PulseText 'Startup change failed'}
    }catch{Refresh-StartupState;$window.FindName('StartupStatus').Text=Get-PulseText 'Startup change canceled or failed'}
})
Add-Type -Path "$PSScriptRoot\UpdateCheck.cs"
$script:updater=[UpdateCheck]::new();$script:updateChecked=$false
$window.FindName('AutoUpdates').IsChecked=$script:autoUpdates
$window.FindName('AutoUpdates').Add_Click({$script:autoUpdates=[bool]$window.FindName('AutoUpdates').IsChecked;Save-WidgetSettings})
function Start-UpdateCheck {$script:updater.Start();$window.FindName('UpdateStatus').Text=Get-PulseText 'Checking for updates…'}
$window.FindName('CheckUpdates').Add_Click({Start-UpdateCheck})
$window.FindName('GetUpdate').Add_Click({Start-Process 'https://github.com/medking82/hardware-pulse/releases/latest'})
$updateTimer=[Windows.Threading.DispatcherTimer]::new();$updateTimer.Interval=[TimeSpan]::FromSeconds(15)
$updateTimer.Add_Tick({
    if(-not $script:updateChecked){$script:updateChecked=$true;if($script:autoUpdates){Start-UpdateCheck}}
    if($script:updater.Pending -and $script:updater.Pending.IsCompleted){
        try{
            if($script:updater.Pending.IsFaulted){throw 'Network error'}
            $release=$script:updater.Pending.Result | ConvertFrom-Json
            $remote=[version]([string]$release.tag_name -replace '^v','')
            $hasUpdate=$remote -gt [version]'0.4.0'
            $window.FindName('UpdateStatus').Text=if($hasUpdate){(Get-PulseText 'Update available')+' · '+$remote}else{Get-PulseText 'You are up to date'}
            $window.FindName('GetUpdate').Visibility=if($hasUpdate){'Visible'}else{'Collapsed'}
            if($hasUpdate -and $script:tray){$script:tray.ShowBalloonTip(5000,'Hardware Pulse',(Get-PulseText 'Update available')+' · '+$remote,[Windows.Forms.ToolTipIcon]::Info)}
        }catch{$window.FindName('UpdateStatus').Text=Get-PulseText 'Update check failed; try again'}
        $script:updater=[UpdateCheck]::new()
    }
});$updateTimer.Start()
Refresh-StartupState
Register-Theme $window
