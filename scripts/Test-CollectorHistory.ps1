param([string]$AppPath='build/native/app')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$app=[IO.Path]::GetFullPath($AppPath)
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$test=Join-Path $app 'NativeCollectorHistoryTests.exe'
$config=Join-Path $app 'NativeCollectorHistoryTests.exe.config'
$arguments=@('/nologo','/target:exe','/platform:x64',"/out:$test",'/reference:System.Core.dll',"/reference:$app/HardwarePulse.exe",("/reference:"+(Join-Path $app 'Pulse.Adapters.Windows.dll')),("/reference:"+(Join-Path $app 'lib/LibreHardwareMonitorLib.dll')),("/reference:"+(Join-Path $app 'lib/HidSharp.dll')),(Join-Path $root 'scripts/CollectorHistoryTests.cs'))
try {
    & "$PSScriptRoot/Run-Hidden.ps1" $compiler $arguments $root
    if(Test-Path (Join-Path $app 'HardwarePulse.exe.config')){Copy-Item (Join-Path $app 'HardwarePulse.exe.config') $config -Force}
    & "$PSScriptRoot/Run-Hidden.ps1" $test @() $app
} finally {
    Remove-Item -LiteralPath $test,$config -Force -ErrorAction SilentlyContinue
}
