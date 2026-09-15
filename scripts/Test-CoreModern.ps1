param([string]$DotNetPath)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
if(-not $DotNetPath){
    $local=Join-Path $root 'vendor/dotnet-sdk/dotnet.exe'
    if(Test-Path $local){$DotNetPath=$local}else{$DotNetPath=(Get-Command dotnet -ErrorAction Stop).Source}
}
$project=Join-Path $root 'scripts/CoreTests/Pulse.Core.Tests.csproj'
& "$PSScriptRoot/Run-Hidden.ps1" $DotNetPath @('build',$project,'--configuration','Release','--disable-build-servers','-p:UseSharedCompilation=false','-nologo') $root
& "$PSScriptRoot/Run-Hidden.ps1" $DotNetPath @((Join-Path $root 'scripts/CoreTests/bin/Release/net10.0/Pulse.Core.Tests.dll')) $root
'PASS modern Core: shared source and tests on .NET 10'
