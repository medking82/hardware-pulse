param([string]$AppPath,[string]$CecilPath)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
if(-not $AppPath){$AppPath=Join-Path $root 'build/app'}
if(-not $CecilPath){$CecilPath=Join-Path $root 'vendor/mono-cecil/Mono.Cecil.dll'}
Add-Type -Path ([IO.Path]::GetFullPath($CecilPath))
$lhm=[Mono.Cecil.ModuleDefinition]::ReadModule((Join-Path $AppPath 'lib/LibreHardwareMonitorLib.dll'))
$black=[Mono.Cecil.ModuleDefinition]::ReadModule((Join-Path $AppPath 'lib/BlackSharp.Core.dll'))
try {
    $method=@($lhm.GetType('LibreHardwareMonitor.Hardware.Storage.StorageDevice').Methods|Where-Object Name -eq 'UpdatePerformanceSensors')[0]
    $sites=@($method.Body.Instructions|Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'get_PhysicalPath'})
    if($sites.Count -ne 1 -or $sites[0].Next.OpCode.Name -ne 'ldc.i4.0'){throw 'LHM physical-disk performance query requests unnecessary data/write access'}
    $method=@($black.GetType('BlackSharp.Core.Interop.Windows.Utilities.SafeFileHandler').Methods|Where-Object Name -eq 'OpenHandle')[0]
    if($method.Body.Instructions[1].OpCode.Name -ne 'ldc.i4' -or $method.Body.Instructions[1].Operand -ne [int]::MinValue){throw 'DiskInfoToolkit handle helper requests unnecessary write access'}
    'Storage access passed: query-only performance handle; read-only SMART/enumeration handle.'
} finally {$lhm.Dispose();$black.Dispose()}
# Idempotence and refusal are part of the build contract; use isolated fixtures.
$fixture=Join-Path $root ('build/storage-access-tests-'+[Guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Force "$fixture/lib"
try {
    foreach($name in @('LibreHardwareMonitorLib.dll','BlackSharp.Core.dll')){Copy-Item (Join-Path $AppPath ('lib/'+$name)) "$fixture/lib/$name"}
    $before=(Get-FileHash "$fixture/lib/LibreHardwareMonitorLib.dll" -Algorithm SHA256).Hash
    & "$PSScriptRoot/Patch-StorageAccess.ps1" -LibraryDirectory "$fixture/lib"
    if((Get-FileHash "$fixture/lib/LibreHardwareMonitorLib.dll" -Algorithm SHA256).Hash -ne $before){throw 'Repeated storage patch changed pinned output'}
    Copy-Item "$root/vendor/lhm/LibreHardwareMonitorLib.dll" "$fixture/lib/LibreHardwareMonitorLib.dll" -Force
    $before=(Get-FileHash "$fixture/lib/LibreHardwareMonitorLib.dll" -Algorithm SHA256).Hash
    [IO.File]::WriteAllText("$fixture/lib/BlackSharp.Core.dll",'fixture-unknown-library')
    $refused=$false
    try {& "$PSScriptRoot/Patch-StorageAccess.ps1" -LibraryDirectory "$fixture/lib"}catch{if($_.Exception.Message -notlike 'Unknown storage dependency:*'){throw};$refused=$true}
    if(-not $refused -or (Get-FileHash "$fixture/lib/LibreHardwareMonitorLib.dll" -Algorithm SHA256).Hash -ne $before){throw 'Unknown dependency did not fail closed before writes'}
    $refused=$false
    try {& "$PSScriptRoot/Patch-StorageAccess.ps1" -LibraryDirectory $root}catch{if($_.Exception.Message -notlike '*restricted to this checkout build outputs*'){throw};$refused=$true}
    if(-not $refused){throw 'Storage patch accepted a non-build path'}
    $fake=Join-Path $fixture 'project'
    $null=New-Item -ItemType Directory -Force "$fake/scripts","$fixture/outside/lib"
    Copy-Item "$PSScriptRoot/Patch-StorageAccess.ps1" "$fake/scripts/Patch-StorageAccess.ps1"
    $null=New-Item -ItemType Junction -Path "$fake/build" -Target "$fixture/outside"
    $refused=$false
    try {& "$fake/scripts/Patch-StorageAccess.ps1" -LibraryDirectory "$fake/build/lib"}catch{if($_.Exception.Message -like '*reparse point*'){$refused=$true}else{throw 'Build-root junction was not rejected before reading manifest'}}
    if(-not $refused){throw 'Build-root junction was accepted'}
    'Storage patch guards passed: repeatable, unknown input refused, build-only scope.'
} finally {Remove-Item -LiteralPath $fixture -Recurse -Force}
