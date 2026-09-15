$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$output=Join-Path $root ('vendor/material-policy-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output | Out-Null
& "$PSScriptRoot/Build-Core.ps1" -OutputPath "$output/Pulse.Core.dll"
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$arguments=@('/nologo','/target:exe',"/out:$output\MaterialPolicyTests.exe",'/reference:System.Web.Extensions.dll',"$root\src\Adapters\Windows\Models.cs","$root\src\Native\Settings.cs","/reference:$output\Pulse.Core.dll","$root\scripts\MaterialPolicyTests.cs")
& "$PSScriptRoot/Run-Hidden.ps1" $compiler $arguments $root
& "$PSScriptRoot/Run-Hidden.ps1" "$output/MaterialPolicyTests.exe" @($output) $root
