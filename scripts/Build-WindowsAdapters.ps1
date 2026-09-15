param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
if(-not $OutputDirectory){$OutputDirectory=Join-Path $root 'build/adapters'}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
& "$PSScriptRoot/Build-Core.ps1" -OutputPath (Join-Path $OutputDirectory 'Pulse.Core.dll')
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$arguments=@('/nologo','/target:library','/platform:anycpu','/optimize+',"/out:$OutputDirectory\Pulse.Adapters.Windows.dll","/reference:$OutputDirectory\Pulse.Core.dll",'/reference:System.Web.Extensions.dll','/reference:System.Management.dll')
$arguments+=@(Get-ChildItem "$root/src/Adapters/Windows" -Filter *.cs | Select-Object -ExpandProperty FullName)
& "$PSScriptRoot/Run-Hidden.ps1" $compiler $arguments $root
