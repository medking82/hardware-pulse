param([switch]$NativeArm64)
$ErrorActionPreference='Stop'
if($NativeArm64 -and [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString() -ne "Arm64"){throw "Native ARM64 adapter tests require an ARM64 Windows host"}
$root=Split-Path $PSScriptRoot
$output=Join-Path $root 'build/adapters'
& "$PSScriptRoot/Build-WindowsAdapters.ps1" -OutputDirectory $output
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& "$PSScriptRoot/Run-Hidden.ps1" $compiler @('/nologo','/target:exe','/reference:System.Runtime.InteropServices.RuntimeInformation.dll',"/out:$output\WindowsAdapterTests.exe","/reference:$output\Pulse.Adapters.Windows.dll","/reference:$output\Pulse.Core.dll",(Join-Path $root 'scripts\WindowsAdapterTests.cs'),(Join-Path $root 'scripts\CodexQuotaTests.cs'),(Join-Path $root 'scripts\QuotaLoginFileTests.cs')) $root
if($NativeArm64){
    # Framework AnyCPU defaults to x64 emulation on Windows ARM64. Opt in for this test process only.
    $launcher=[IO.Path]::GetFullPath((Join-Path $output 'RunArm64Tests.cmd'))
    [IO.File]::WriteAllText($launcher,"@echo off`r`nstart `"`" /machine arm64 /b /wait `"%~dp0WindowsAdapterTests.exe`"`r`nexit /b %errorlevel%`r`n",[Text.Encoding]::ASCII)
    & "$PSScriptRoot/Run-Hidden.ps1" "$env:WINDIR\System32\cmd.exe" @('/d','/c',$launcher) $root
}else{
    & "$PSScriptRoot/Run-Hidden.ps1" "$output/WindowsAdapterTests.exe" @() $root
}
