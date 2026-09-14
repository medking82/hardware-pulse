param([switch]$Interactive)
$ErrorActionPreference='Stop'
if(-not $Interactive){throw 'This integration test toggles Show Desktop. Supply -Interactive to opt in.'}
$root=Split-Path $PSScriptRoot
$app=Join-Path $root 'build/native/app'
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$out=Join-Path $root 'vendor/desktop-layer-result.txt'
$arguments=@('/nologo','/target:winexe',"/out:$app\NativeDesktopLayerTests.exe","/reference:$app\HardwarePulse.exe",'/reference:System.Xaml.dll','/reference:System.Drawing.dll',"/reference:$framework\WPF\PresentationFramework.dll","/reference:$framework\WPF\PresentationCore.dll","/reference:$framework\WPF\WindowsBase.dll","$root\scripts\NativeDesktopLayerTests.cs")
& "$PSScriptRoot/Run-Hidden.ps1" "$framework/csc.exe" $arguments $root
Copy-Item "$app/HardwarePulse.exe.config" "$app/NativeDesktopLayerTests.exe.config" -Force
& "$PSScriptRoot/Run-Hidden.ps1" "$app/NativeDesktopLayerTests.exe" @($out) $root
Get-Content $out
