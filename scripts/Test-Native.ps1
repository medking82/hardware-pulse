$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$app=Join-Path $root 'build/native/app'
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$arguments=@('/nologo','/target:exe',"/out:$app\NativeTests.exe","/reference:$app\HardwarePulse.exe",'/reference:System.Core.dll','/reference:System.Windows.Forms.dll','/reference:System.Xaml.dll',"/reference:$framework\WPF\PresentationFramework.dll","/reference:$framework\WPF\PresentationCore.dll","/reference:$framework\WPF\WindowsBase.dll","$root\scripts\NativeTests.cs","$root\scripts\NativeStartupTests.cs","$root\scripts\NativeFpsTests.cs","$root\scripts\NativeQuotaTests.cs")
& "$PSScriptRoot/Run-Hidden.ps1" "$framework/csc.exe" $arguments $root
Copy-Item "$app/HardwarePulse.exe.config" "$app/NativeTests.exe.config" -Force
& "$PSScriptRoot/Run-Hidden.ps1" "$framework/csc.exe" @('/nologo','/target:exe',"/out:$app\NativeCollectorBench.exe","/reference:$app\HardwarePulse.exe","$root\scripts\NativeCollectorBench.cs") $root
Copy-Item "$app/HardwarePulse.exe.config" "$app/NativeCollectorBench.exe.config" -Force
$testRoot=Join-Path $root ('vendor/native-ui-'+[Guid]::NewGuid().ToString('N'))
& "$PSScriptRoot/Run-Hidden.ps1" "$app/NativeTests.exe" @($testRoot) $root
