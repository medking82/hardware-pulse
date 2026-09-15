param([string]$OutputPath)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
if(-not $OutputPath){$OutputPath=Join-Path $root 'build/core/Pulse.Core.dll'}
$OutputPath=[IO.Path]::GetFullPath($OutputPath)
[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($OutputPath))
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$arguments=@('/nologo','/target:library','/platform:anycpu','/optimize+',"/out:$OutputPath")
$arguments+=@(Get-ChildItem "$root/src/Core" -Filter *.cs | Select-Object -ExpandProperty FullName)
& "$PSScriptRoot/Run-Hidden.ps1" $compiler $arguments $root
