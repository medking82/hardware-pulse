param([string]$NativeTestAppPath,[switch]$ModernCore)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$hostSource=[IO.File]::ReadAllText("$root/src/Native/Program.cs")
$version=[regex]::Match($hostSource,'AssemblyVersion\("(\d+\.\d+\.\d+)\.0"\)').Groups[1].Value
if(-not $version){throw 'Missing application version'}
foreach($check in @(@('installer/HardwarePulse.iss',"AppVersion=$version"),@('scripts/Build.ps1',"HardwarePulse-$version-Setup.exe"),@('src/Panel.xaml',"Version $version"),@('src/Native/Languages.txt',"Version $version"))){
    if(-not [IO.File]::ReadAllText((Join-Path $root $check[0])).Contains($check[1])){throw "Version mismatch in $($check[0])"}
}
foreach($file in Get-ChildItem "$root/src","$root/scripts" -Filter *.ps1){
    $bytes=[IO.File]::ReadAllBytes($file.FullName)
    $null=[Text.UTF8Encoding]::new($false,$true).GetString($bytes)
    if(-not($bytes.Length -ge 3 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191)){throw "UTF-8 BOM required for Windows PowerShell: $($file.Name)"}
    $tokens=$null;$errors=$null
    $null=[Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$errors)
    if($errors){throw "$($file.Name): $errors"}
}
foreach($file in Get-ChildItem "$root/assets" -Filter *.svg){$null=[xml](Get-Content $file.FullName -Raw)}
$null=[xml](Get-Content "$root/src/Panel.xaml" -Raw)
& "$PSScriptRoot/Run-Hidden.ps1" "$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe" @('-NoProfile','-File',"$root/src/Test-Sensors.ps1") $root
& "$PSScriptRoot/Run-Hidden.ps1" "$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe" @('-NoProfile','-STA','-File',"$PSScriptRoot/Test-Snap.ps1") $root
& "$PSScriptRoot/Build.ps1"
& "$PSScriptRoot/Run-Hidden.ps1" "$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe" @('-NoProfile','-File',"$PSScriptRoot/Test-Package.ps1") $root
& "$PSScriptRoot/Build-Native.ps1"
& "$PSScriptRoot/Test-CollectorHistory.ps1"
& "$PSScriptRoot/Test-Core.ps1"
if($ModernCore){& "$PSScriptRoot/Test-CoreModern.ps1"}
& "$PSScriptRoot/Test-NativeSensors.ps1"
& "$PSScriptRoot/Test-WindowsAdapters.ps1"
& "$PSScriptRoot/Run-Hidden.ps1" "$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe" @('-NoProfile','-STA','-File',"$PSScriptRoot/Test-DesktopShortcut.ps1",'-AppPath',"$root/build/native/app") $root
& "$PSScriptRoot/Run-Hidden.ps1" "$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe" @('-NoProfile','-STA','-File',"$PSScriptRoot/Test-LocalContrast.ps1",'-AppPath',"$root/build/native/app") $root
& "$PSScriptRoot/Run-Hidden.ps1" "$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe" @('-NoProfile','-STA','-File',"$PSScriptRoot/Test-DesktopLayout.ps1",'-AppPath',"$root/build/native/app") $root
& "$PSScriptRoot/Run-Hidden.ps1" "$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe" @('-NoProfile','-STA','-File',"$PSScriptRoot/Test-HomeUpdate.ps1",'-AppPath',"$root/build/native/app") $root
& "$PSScriptRoot/Run-Hidden.ps1" "$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe" @('-NoProfile','-STA','-File',"$PSScriptRoot/Test-SettingsQuota.ps1",'-AppPath',"$root/build/native/app") $root
& "$PSScriptRoot/Test-ReadingSession.ps1"
& "$PSScriptRoot/Test-UpdateCoordinator.ps1"
& "$PSScriptRoot/Test-MaterialPolicy.ps1"
if($NativeTestAppPath){& "$PSScriptRoot/Build-Native.ps1" -AppPath $NativeTestAppPath}
& "$PSScriptRoot/Test-Native.ps1" -AppPath $NativeTestAppPath
& "$PSScriptRoot/Run-Hidden.ps1" "$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe" @('-NoProfile','-File',"$PSScriptRoot/Test-Updater.ps1") $root
foreach($file in Get-ChildItem "$root/src" -File){
    if((Get-Content $file.FullName -Raw) -match 'C:\\github\\|C:\\Users\\Marck|HWiNFO') {throw "Personal/deprecated dependency in $($file.Name)"}
}
'Validation passed: syntax, XML/SVG, clean native package, differential sensors, WPF/startup regression, snap and updater checks.'
