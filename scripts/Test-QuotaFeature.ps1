param([string]$AppPath)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$app=if($AppPath){[IO.Path]::GetFullPath($AppPath)}else{Join-Path $root 'build/native/app'}
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$arguments=@('/nologo','/target:exe',"/out:$app\QuotaFeatureTests.exe","/reference:$app\HardwarePulse.exe","/reference:$app\Pulse.Core.dll","/reference:$app\Pulse.Adapters.Windows.dll",'/reference:System.Web.Extensions.dll','/reference:System.Core.dll','/reference:System.Windows.Forms.dll','/reference:System.Xaml.dll',"/reference:$framework\WPF\PresentationFramework.dll","/reference:$framework\WPF\PresentationCore.dll","/reference:$framework\WPF\WindowsBase.dll","$root\scripts\QuotaFeatureTests.cs","$root\scripts\NativeQuotaTests.cs","$root\scripts\NativeQuotaRecoveryTests.cs")
$arguments+=@("$root\scripts\QuotaDiagnosticLogTests.cs","$root\scripts\QuotaScheduleStoreTests.cs","$root\scripts\NativeArgonTests.cs")
$arguments+=@("$root\scripts\NativeSnapshotRecoveryTests.cs")
$arguments+=@("$root\scripts\NativeLocalInferenceTests.cs")
& "$PSScriptRoot/Run-Hidden.ps1" "$framework/csc.exe" $arguments $root
Copy-Item "$app/HardwarePulse.exe.config" "$app/QuotaFeatureTests.exe.config" -Force
$testRoot=Join-Path $root ('vendor/quota-feature-'+[Guid]::NewGuid().ToString('N'))
& "$PSScriptRoot/Run-Hidden.ps1" "$app/QuotaFeatureTests.exe" @($testRoot) $root
