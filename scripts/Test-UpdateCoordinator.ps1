$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$output=Join-Path $root ('vendor/update-coordinator-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
# Headless coordinator tests use a fake client: no network, installer or UAC.
$arguments=@('/nologo','/target:exe',"/out:$output\UpdateCoordinatorTests.exe",'/reference:System.Web.Extensions.dll',"$root\src\Adapters\Windows\Models.cs","$root\src\UpdateCheck.cs","$root\src\Native\UpdateCoordinator.cs","$root\scripts\UpdateCoordinatorTests.cs")
& "$PSScriptRoot/Run-Hidden.ps1" $compiler $arguments $root
& "$PSScriptRoot/Run-Hidden.ps1" "$output/UpdateCoordinatorTests.exe" @() $root
