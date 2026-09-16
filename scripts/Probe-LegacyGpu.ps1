$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$library=Join-Path $root 'build/app/lib/LibreHardwareMonitorLib.dll'
if(-not(Test-Path $library)){throw 'Build the pinned runtime first with Build.ps1.'}
$directory=Join-Path $root ('vendor/legacy-gpu-probe-'+[Guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path $directory
$executable=Join-Path $directory 'LegacyGpuProbe.exe'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& "$PSScriptRoot/Run-Hidden.ps1" $compiler @('/nologo','/target:exe','/platform:x64',"/out:$executable","/reference:$library","$root\scripts\LegacyGpuProbe.cs") $root
# The native process owns all temporary GPU handles, including failed initialization.
# It never loads installed Pulse settings or requests a fan-control mode.
& "$PSScriptRoot/Run-Hidden.ps1" $executable @((Split-Path $library)) $root
