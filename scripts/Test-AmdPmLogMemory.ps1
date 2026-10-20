param([string]$AppPath)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
if(-not $AppPath){$AppPath=Join-Path $root 'build/app'}
$test=Join-Path $root 'build/amd-pmlog-tests'
$null=New-Item -ItemType Directory -Path $test -Force
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& "$PSScriptRoot/Run-Hidden.ps1" $compiler @('/nologo','/target:exe','/platform:x64',"/out:$test\AmdPmLogMemoryTests.exe",[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'AmdPmLogMemoryTests.cs'))) $root
& "$PSScriptRoot/Run-Hidden.ps1" "$test/AmdPmLogMemoryTests.exe" @([IO.Path]::GetFullPath($AppPath)) $root
Add-Type -Path (Join-Path $root 'vendor/mono-cecil/Mono.Cecil.dll')
$library=[Mono.Cecil.ModuleDefinition]::ReadModule((Join-Path $AppPath 'lib/LibreHardwareMonitorLib.dll'))
try {
    $calls=@($library.GetType('LibreHardwareMonitor.Hardware.Gpu.AmdGpu').Methods|Where-Object HasBody|ForEach-Object {$_.Body.Instructions}|Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -in @('Read','PtrToStructure') -and $_.Operand.DeclaringType.FullName -in @('HardwarePulse.AmdPmLogMemory','System.Runtime.InteropServices.Marshal')
    })
    if($calls.Count -ne 2 -or @($calls|Where-Object {$_.Operand.DeclaringType.FullName -ne 'HardwarePulse.AmdPmLogMemory'}).Count){throw 'Shipped AMD Update/GetReport still marshal driver-owned PMLog memory directly'}
    'PASS shipped LHM: both AMD PMLog marshal sites use the guarded reader.'
} finally {$library.Dispose()}
