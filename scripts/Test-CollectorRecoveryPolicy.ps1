$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$output=Join-Path $root ('vendor/collector-recovery-policy-'+[Guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path $output -Force
$testExe=Join-Path $output 'CollectorRecoveryTests.exe'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$arguments=@('/nologo','/target:exe',"/out:$testExe")
$arguments+=@('src/Native/CollectorRecovery.cs','scripts/CollectorRecoveryTests.cs') | ForEach-Object {[IO.Path]::GetFullPath((Join-Path $root $_))}
& "$PSScriptRoot/Run-Hidden.ps1" $compiler $arguments $root
& "$PSScriptRoot/Run-Hidden.ps1" $testExe @() $root
