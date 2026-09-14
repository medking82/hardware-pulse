param([switch]$Installer,[string]$SigningCertificateThumbprint)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$app=Join-Path $root 'build/app'
if(Test-Path $app){
    $resolved=[IO.Path]::GetFullPath($app)
    if($resolved -ne [IO.Path]::GetFullPath((Join-Path $root 'build/app'))){throw 'Unexpected build output path'}
    if(@(Get-Item -LiteralPath $app; Get-ChildItem -LiteralPath $app -Recurse -Force) | Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}){throw 'Build output contains a reparse point'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
$null=New-Item -ItemType Directory -Path $app,"$app/lib","$root/vendor","$root/dist" -Force
$deps=Get-Content "$root/dependencies.lock.json" -Raw | ConvertFrom-Json
foreach($dep in $deps){
    $file=Join-Path "$root/vendor" $dep.file
    if(-not(Test-Path $file)){Invoke-WebRequest $dep.url -OutFile $file}
    if((Get-FileHash $file -Algorithm SHA256).Hash -ne $dep.sha256){throw "Dependency hash mismatch: $($dep.name)"}
}
Copy-Item "$root/src/*" $app -Force
Copy-Item "$root/assets" $app -Recurse -Force
Copy-Item "$root/licenses" $app -Recurse -Force
Copy-Item "$root/LICENSE","$root/PRIVACY.md","$root/SIGNING.md" $app -Force
Copy-Item "$root/dependencies.lock.json" $app -Force
Copy-Item "$root/vendor/*-source-*.zip" "$app/licenses" -Force
Expand-Archive "$root/vendor/LibreHardwareMonitor-0.9.6.zip" "$root/vendor/lhm" -Force
Copy-Item "$root/vendor/lhm/*.dll" "$app/lib" -Force
$null=New-Item -ItemType Directory -Path "$app/tools" -Force
Copy-Item "$root/vendor/PresentMon-2.5.1-x64.exe" "$app/tools/PresentMon.exe" -Force
$ps5="$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe"
& "$PSScriptRoot/Run-Hidden.ps1" $ps5 @('-NoProfile','-STA','-File',"$PSScriptRoot/Make-Icon.ps1",'-OutputPath',"$root/assets/pulse.ico") $root
Copy-Item "$root/assets/pulse.ico" "$app/assets" -Force
$csc="$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe"
$automation="$env:WINDIR/Microsoft.NET/assembly/GAC_MSIL/System.Management.Automation/v4.0_3.0.0.0__31bf3856ad364e35/System.Management.Automation.dll"
& "$PSScriptRoot/Run-Hidden.ps1" $csc @('/nologo','/target:winexe','/platform:x64',("/out:"+(Join-Path $app 'HardwarePulse.exe')),'/reference:System.Windows.Forms.dll',"/reference:$automation",("/win32icon:"+(Join-Path $root 'assets\pulse.ico')),(Join-Path $root 'src\WidgetHost.cs')) $root
if($SigningCertificateThumbprint){& "$PSScriptRoot/Sign.ps1" -Path "$app/HardwarePulse.exe" -Thumbprint $SigningCertificateThumbprint}
if($Installer){
    $iscc="$root/vendor/inno/ISCC.exe"
    if(-not(Test-Path $iscc)){
        $setup="$root/vendor/innosetup-6.7.3.exe"
        if((Get-AuthenticodeSignature $setup).Status -ne 'Valid'){throw 'Inno compiler installer signature invalid'}
        & "$PSScriptRoot/Run-Hidden.ps1" $setup @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER',"/DIR=$root/vendor/inno",'/NOICONS') $root
    }
    & "$PSScriptRoot/Run-Hidden.ps1" $iscc @("$root/installer/HardwarePulse.iss") $root
    if($SigningCertificateThumbprint){& "$PSScriptRoot/Sign.ps1" -Path "$root/dist/HardwarePulse-0.4.9-Setup.exe" -Thumbprint $SigningCertificateThumbprint}
    Copy-Item "$root/dist/HardwarePulse-0.4.9-Setup.exe" "$root/dist/HardwarePulse-Setup.exe" -Force
    Get-FileHash "$root/dist/HardwarePulse-0.4.9-Setup.exe" -Algorithm SHA256 | Select-Object Hash,Path
}
