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
    Update-SurfaceOpacity
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
$script:autoDownload=[bool]$saved.autoDownload
$script:checkResultHandled=$true;$script:downloadResultHandled=$true
$script:updateAsset=$null;$script:updateTag=$null
$script:nextUpdateCheck=[DateTime]::MinValue
$window.FindName('AutoUpdates').IsChecked=$script:autoUpdates
$window.FindName('AutoDownload').IsChecked=$script:autoDownload
$window.FindName('AutoUpdates').Add_Click({$script:autoUpdates=[bool]$window.FindName('AutoUpdates').IsChecked;Save-WidgetSettings})
$window.FindName('AutoDownload').Add_Click({$script:autoDownload=[bool]$window.FindName('AutoDownload').IsChecked;if($script:autoDownload){$script:autoUpdates=$true;$window.FindName('AutoUpdates').IsChecked=$true};Save-WidgetSettings})
function Start-UpdateCheck {
    if($script:updater.Installing){return}
    if($script:updater.Ready){$window.FindName('InstallUpdate').Visibility='Visible';$window.FindName('UpdateStatus').Text=Get-PulseText 'Update ready to install';return}
    if($script:updater.DownloadPending -and -not $script:updater.DownloadPending.IsCompleted){return}
    $script:updater.Start();$script:checkResultHandled=$false
    $window.FindName('InstallUpdate').Visibility='Collapsed'
    $script:nextUpdateCheck=[DateTime]::Now.AddHours(6)
    $window.FindName('UpdateStatus').Text=Get-PulseText 'Checking for updates…'
    $window.FindName('CheckUpdates').IsEnabled=$false
}
function Start-UpdateDownload {
    try {
        $asset=$script:updateAsset
        if(-not $asset){throw 'No release selected'}
        $script:updater.Download([string]$asset.browser_download_url,$script:updateTag,[string]$asset.digest,[long]$asset.size)
        $script:downloadResultHandled=$false
        $window.FindName('GetUpdate').IsEnabled=$false
        $window.FindName('CheckUpdates').IsEnabled=$false
        $window.FindName('InstallUpdate').Visibility='Collapsed'
        $window.FindName('DownloadProgress').Visibility='Visible'
    } catch {$window.FindName('UpdateStatus').Text=Get-PulseText 'Update download failed; try again'}
}
$window.FindName('CheckUpdates').Add_Click({Start-UpdateCheck})
$window.FindName('GetUpdate').Add_Click({Start-UpdateDownload})
$window.FindName('InstallUpdate').Add_Click({
    try {$script:updater.Install();$window.FindName('UpdateStatus').Text=Get-PulseText 'Installer started';$window.FindName('InstallUpdate').IsEnabled=$false}
    catch {$window.FindName('UpdateStatus').Text=Get-PulseText 'Installation canceled or failed; try again';if(-not $script:updater.Ready){$window.FindName('InstallUpdate').Visibility='Collapsed';$window.FindName('GetUpdate').Visibility='Visible'}}
})
function Update-UpdateState {
    if($window.FindName('InstallUpdate').Visibility -eq 'Visible' -and -not $script:updater.Installing){$window.FindName('InstallUpdate').IsEnabled=$true}
    if($script:autoUpdates -and -not $script:updater.Ready -and [DateTime]::Now -ge $script:nextUpdateCheck -and $script:checkResultHandled){Start-UpdateCheck}
    if(-not $script:checkResultHandled -and $script:updater.Pending.IsCompleted){
        $script:checkResultHandled=$true;$window.FindName('CheckUpdates').IsEnabled=$true
        try {
            if($script:updater.Pending.IsFaulted){throw 'Network error'}
            $release=$script:updater.Pending.Result | ConvertFrom-Json
            if($release.draft -or $release.prerelease){throw 'Not a stable release'}
            $remote=[version]([string]$release.tag_name -replace '^v','')
            $hasUpdate=$remote -gt [version]'0.4.9'
            $window.FindName('UpdateStatus').Text=if($hasUpdate){(Get-PulseText 'Update available')+' · '+$remote}else{Get-PulseText 'You are up to date'}
            $window.FindName('GetUpdate').Visibility='Collapsed';$script:updateAsset=$null
            if($hasUpdate){
                $assets=@($release.assets | Where-Object {$_.name -eq 'HardwarePulse-Setup.exe'})
                if($assets.Count -ne 1 -or -not [UpdateCheck]::ValidAsset($assets[0].browser_download_url,$release.tag_name,$assets[0].digest,$assets[0].size)){throw 'Invalid installer metadata'}
                $script:updateAsset=$assets[0];$script:updateTag=[string]$release.tag_name
                $window.FindName('GetUpdate').Visibility='Visible'
                if($script:autoDownload){Start-UpdateDownload}
            }
        } catch {$window.FindName('UpdateStatus').Text=Get-PulseText 'Update check failed; try again'}
    }
    if(-not $script:downloadResultHandled -and $script:updater.DownloadPending){
        $window.FindName('DownloadProgress').Value=$script:updater.Progress
        $window.FindName('UpdateStatus').Text=(Get-PulseText 'Downloading update')+' · '+$script:updater.Progress+'%'
        if($script:updater.DownloadPending.IsCompleted){
            $script:downloadResultHandled=$true
            $window.FindName('GetUpdate').IsEnabled=$true;$window.FindName('CheckUpdates').IsEnabled=$true
            $window.FindName('DownloadProgress').Visibility='Collapsed'
            if($script:updater.DownloadPending.IsFaulted -or $script:updater.DownloadPending.IsCanceled){$window.FindName('UpdateStatus').Text=Get-PulseText 'Update download failed; try again'}
            else {
                $window.FindName('GetUpdate').Visibility='Collapsed'
                $window.FindName('InstallUpdate').Visibility='Visible';$window.FindName('InstallUpdate').IsEnabled=$true
                $window.FindName('UpdateStatus').Text=Get-PulseText 'Update ready to install'
                $script:tray.ShowBalloonTip(5000,'Hardware Pulse',(Get-PulseText 'Update ready to install'),[Windows.Forms.ToolTipIcon]::Info)
            }
        }
    }
}
$updateTimer=[Windows.Threading.DispatcherTimer]::new();$updateTimer.Interval=[TimeSpan]::FromMilliseconds(500)
$updateTimer.Add_Tick({Update-UpdateState});$updateTimer.Start()
Refresh-StartupState
Register-Theme $window
