param([Parameter(Mandatory=$true)][string]$LibraryDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$directory=[IO.Path]::GetFullPath($LibraryDirectory)
$build=[IO.Path]::GetFullPath((Join-Path $root 'build'))+[IO.Path]::DirectorySeparatorChar
if(-not $directory.StartsWith($build,[StringComparison]::OrdinalIgnoreCase)){throw 'Storage patch is restricted to this checkout build outputs'}
$cursor=Get-Item -LiteralPath $directory
while($cursor.FullName.Length -ge $build.TrimEnd([IO.Path]::DirectorySeparatorChar).Length){
    if($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Storage patch output contains a reparse point'}
    $cursor=$cursor.Parent
}
$spec=Get-Content -Encoding UTF8 (Join-Path $root 'scripts/storage-access-patch.json') -Raw|ConvertFrom-Json
# Verify every input before loading tools or modifying either build-output library.
$pending=@()
foreach($entry in $spec.libraries){
    $path=Join-Path $directory $entry.file
    if((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Storage patch input is a reparse point'}
    $hash=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if($hash -eq $entry.output_sha256){continue}
    if($hash -ne $entry.input_sha256){throw "Unknown storage dependency: $($entry.file); refusing to patch"}
    $pending+=@{entry=$entry;path=$path;temporary=$path+'.storage-patch.tmp'}
}
if(-not $pending.Count){'Storage dependencies already match pinned read-only outputs.';return}
$dep=@((Get-Content (Join-Path $root 'dependencies.lock.json') -Raw|ConvertFrom-Json)|Where-Object name -eq 'Mono.Cecil Build Tool')
if($dep.Count -ne 1){throw 'Missing unique Mono.Cecil build-tool dependency'}
$package=Join-Path $root ('vendor/'+$dep[0].file)
if(-not(Test-Path -LiteralPath $package)){Invoke-WebRequest $dep[0].url -OutFile $package}
if((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne $dep[0].sha256){throw 'Mono.Cecil package hash mismatch'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$toolDirectory=Join-Path $root 'vendor/mono-cecil'
$null=New-Item -ItemType Directory -Force $toolDirectory
$tool=Join-Path $toolDirectory 'Mono.Cecil.dll'
$zip=[IO.Compression.ZipFile]::OpenRead($package)
try {
    $item=$zip.GetEntry('lib/net40/Mono.Cecil.dll')
    if($null -eq $item){throw 'Pinned Cecil assembly missing from package'}
    if(Test-Path $tool){if((Get-FileHash $tool -Algorithm SHA256).Hash -ne $spec.cecil_sha256){throw 'Cached Cecil assembly hash mismatch'}}
    else {[IO.Compression.ZipFileExtensions]::ExtractToFile($item,$tool,$false)}
} finally {$zip.Dispose()}
if((Get-FileHash $tool -Algorithm SHA256).Hash -ne $spec.cecil_sha256){throw 'Cecil assembly hash mismatch'}
Add-Type -Path $tool
try {
    foreach($item in $pending){
        $module=[Mono.Cecil.ModuleDefinition]::ReadModule($item.path)
        try {
            if($item.entry.file -eq 'LibreHardwareMonitorLib.dll'){
                $methods=@($module.GetType('LibreHardwareMonitor.Hardware.Storage.StorageDevice').Methods|Where-Object Name -eq 'UpdatePerformanceSensors')
                if($methods.Count -ne 1){throw 'Unexpected storage performance method'}
                $sites=@($methods[0].Body.Instructions|Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'get_PhysicalPath'})
                if($sites.Count -ne 1 -or $sites[0].Next.OpCode.Name -ne 'ldc.i4.3'){throw 'Unexpected LHM permission instruction'}
                $sites[0].Next.OpCode=[Mono.Cecil.Cil.OpCodes]::Ldc_I4_0
                $sites[0].Next.Operand=$null
                # Marshal the revocable ADL PMLog mapping only after a guarded
                # local copy; preserve the pinned layout and sensor fallbacks.
                $adapter=[Mono.Cecil.ModuleDefinition]::ReadModule((Join-Path (Split-Path $directory) 'Pulse.Adapters.Windows.dll'))
                try {
                    $reader=@($adapter.GetType('HardwarePulse.AmdPmLogMemory').Methods|Where-Object Name -eq 'Read')
                    if($reader.Count -ne 1){throw 'Missing guarded AMD PMLog reader'}
                    $replacement=$module.ImportReference($reader[0])
                    $amd=$module.GetType('LibreHardwareMonitor.Hardware.Gpu.AmdGpu')
                    $reads=@($amd.Methods|Where-Object HasBody|ForEach-Object {$_.Body.Instructions}|Where-Object {
                        $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -eq 'System.Runtime.InteropServices.Marshal' -and $_.Operand.Name -eq 'PtrToStructure'
                    })
                    if($reads.Count -ne 2){throw 'Unexpected AMD PMLog marshal sites'}
                    foreach($read in $reads){
                        if($read.Operand.FullName -ne 'System.Object System.Runtime.InteropServices.Marshal::PtrToStructure(System.IntPtr,System.Type)'){throw 'Unexpected AMD PMLog marshal signature'}
                        $read.Operand=$replacement
                    }
                } finally {$adapter.Dispose()}
            } else {
                $methods=@($module.GetType('BlackSharp.Core.Interop.Windows.Utilities.SafeFileHandler').Methods|Where-Object Name -eq 'OpenHandle')
                if($methods.Count -ne 1){throw 'Unexpected storage handle method'}
                $site=$methods[0].Body.Instructions[1]
                if($site.OpCode.Name -ne 'ldc.i4' -or $site.Operand -ne -1073741824){throw 'Unexpected BlackSharp permission instruction'}
                $site.Operand=[int]::MinValue # GENERIC_READ only; never GENERIC_WRITE.
            }
            $writer=New-Object Mono.Cecil.WriterParameters
            $writer.DeterministicMvid=$true;$writer.Timestamp=[uint32]0
            $module.Write($item.temporary,$writer)
        } finally {$module.Dispose()}
        $outputHash=(Get-FileHash $item.temporary -Algorithm SHA256).Hash
        if($outputHash -ne $item.entry.output_sha256){throw "Non-reproducible dependency patch: $($item.entry.file); actual SHA256 $outputHash"}
    }
    foreach($item in $pending){Move-Item -LiteralPath $item.temporary -Destination $item.path -Force}
} finally {foreach($item in $pending){if(Test-Path $item.temporary){Remove-Item -LiteralPath $item.temporary -Force}}}
'Applied pinned read-only storage permissions and guarded AMD PMLog reads; sensor identifiers unchanged.'
