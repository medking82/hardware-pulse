param([switch]$NativeArm64)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
if($NativeArm64 -and [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString() -ne 'Arm64'){throw 'Native ARM64 probe requires ARM64 Windows'}
$output=Join-Path $root 'build/adapters'
& "$PSScriptRoot/Build-WindowsAdapters.ps1" -OutputDirectory $output
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& "$PSScriptRoot/Run-Hidden.ps1" $compiler @('/nologo','/target:exe','/reference:System.Web.Extensions.dll','/reference:System.Runtime.InteropServices.RuntimeInformation.dll',"/out:$output\WindowsCapabilityProbe.exe","/reference:$output\Pulse.Adapters.Windows.dll","/reference:$output\Pulse.Core.dll",(Join-Path $root 'scripts/WindowsCapabilityProbe.cs')) $root
if($NativeArm64){
    $launcher=[IO.Path]::GetFullPath((Join-Path $output 'RunArm64CapabilityProbe.cmd'))
    [IO.File]::WriteAllText($launcher,"@echo off`r`nstart `"`" /machine arm64 /b /wait `"%~dp0WindowsCapabilityProbe.exe`" --self-test`r`nexit /b %errorlevel%`r`n",[Text.Encoding]::ASCII)
    & "$PSScriptRoot/Run-Hidden.ps1" "$env:WINDIR\System32\cmd.exe" @('/d','/c',$launcher) $root
}else{
    & "$PSScriptRoot/Run-Hidden.ps1" "$output/WindowsCapabilityProbe.exe" @('--self-test') $root
}
