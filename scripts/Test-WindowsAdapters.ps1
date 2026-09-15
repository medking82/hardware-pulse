$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$output=Join-Path $root 'build/adapters'
& "$PSScriptRoot/Build-WindowsAdapters.ps1" -OutputDirectory $output
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& "$PSScriptRoot/Run-Hidden.ps1" $compiler @('/nologo','/target:exe',"/out:$output\WindowsAdapterTests.exe","/reference:$output\Pulse.Adapters.Windows.dll","/reference:$output\Pulse.Core.dll",(Join-Path $root 'scripts\WindowsAdapterTests.cs')) $root
& "$PSScriptRoot/Run-Hidden.ps1" "$output/WindowsAdapterTests.exe" @() $root
