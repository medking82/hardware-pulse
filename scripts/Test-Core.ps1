$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$output=Join-Path $root 'build/core'
& "$PSScriptRoot/Build-Core.ps1" -OutputPath "$output/Pulse.Core.dll"
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& "$PSScriptRoot/Run-Hidden.ps1" $compiler @('/nologo','/target:exe',"/out:$output/CoreTests.exe","/reference:$output/Pulse.Core.dll",(Join-Path $root 'scripts\CoreTests.cs')) $root
& "$PSScriptRoot/Run-Hidden.ps1" "$output/CoreTests.exe" @() $root
