#requires -Version 7.0

[CmdletBinding()]
param(
    [string]$AppDirectory
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot))
$expectedApp = [IO.Path]::GetFullPath((Join-Path $root 'build/windows-shared/app'))
$app = if ($AppDirectory) { [IO.Path]::GetFullPath($AppDirectory) } else { $expectedApp }

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Assert-RequiredFile([string]$RelativePath) {
    $path = Join-Path $app $RelativePath
    Assert-True (Test-Path -LiteralPath $path -PathType Leaf) "Required package file is missing: $RelativePath"
    Assert-True ((Get-Item -LiteralPath $path).Length -gt 0) "Required package file is empty: $RelativePath"
}

function Get-PeMachine([string]$RelativePath) {
    $path = Join-Path $app $RelativePath
    $stream = [IO.File]::OpenRead($path)
    try {
        Assert-True ($stream.Length -ge 64) "Invalid PE file: $RelativePath"
        $reader = [IO.BinaryReader]::new($stream)
        Assert-True ($reader.ReadUInt16() -eq 0x5A4D) "Missing DOS header: $RelativePath"
        $stream.Position = 0x3C
        $peOffset = $reader.ReadInt32()
        Assert-True ($peOffset -ge 0 -and ($peOffset + 6) -le $stream.Length) "Invalid PE offset: $RelativePath"
        $stream.Position = $peOffset
        Assert-True ($reader.ReadUInt32() -eq 0x00004550) "Missing PE header: $RelativePath"
        return $reader.ReadUInt16()
    }
    finally {
        $stream.Dispose()
    }
}

Assert-True (Test-Path -LiteralPath $app -PathType Container) "Shared Windows package directory does not exist: $app"
Assert-True ($app.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) 'Package directory must remain inside the repository.'

for ($check = $app; $check.Length -ge $root.Length; $check = [IO.Path]::GetDirectoryName($check)) {
    if (Test-Path -LiteralPath $check) {
        Assert-True (-not ((Get-Item -LiteralPath $check).Attributes -band [IO.FileAttributes]::ReparsePoint)) "Package path traverses a reparse point: $check"
    }
    if ($check -eq $root) { break }
}
Assert-True (-not (Get-ChildItem -LiteralPath $app -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint })) 'Package contains a reparse point.'

$manifestPath = Join-Path $app 'manifest.json'
Assert-True (Test-Path -LiteralPath $manifestPath -PathType Leaf) 'Package manifest is missing.'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -AsHashtable
Assert-True ($manifest.schema -eq 1) 'Unsupported package manifest schema.'
Assert-True ($manifest.kind -in @('windows-development', 'windows-release')) 'Unexpected package kind.'
Assert-True ($manifest.rid -eq 'win-x64') 'Unexpected package runtime identifier.'
Assert-True ($manifest.commit -is [string] -and $manifest.commit -cmatch '^[0-9a-f]{40}$') 'Invalid package source commit.'
Assert-True ($manifest.dirty -is [bool]) 'Invalid package dirty marker.'
Assert-True ($manifest.files -is [Collections.IDictionary] -and $manifest.files.Count -gt 0) 'Package manifest inventory is empty.'

$project = [xml](Get-Content -LiteralPath (Join-Path $root 'src/Hosts/Desktop/Pulse.Desktop.csproj') -Raw)
$expectedVersion = [string]$project.Project.PropertyGroup.Version
Assert-True ($manifest.version -eq $expectedVersion) "Package version '$($manifest.version)' does not match project version '$expectedVersion'."
$expectedKind = if ($expectedVersion -match '-') { 'windows-development' } else { 'windows-release' }
Assert-True ($manifest.kind -eq $expectedKind) "Package kind '$($manifest.kind)' does not match version '$expectedVersion'."
if ($expectedKind -eq 'windows-release') { Assert-True (-not $manifest.dirty) 'Stable Windows package was built from a dirty worktree.' }

if ($app -eq $expectedApp) {
    $head = ((& (Join-Path $PSScriptRoot 'Run-Hidden.ps1') 'git' @('rev-parse', 'HEAD') $root) | Out-String).Trim()
    $dirty = (((& (Join-Path $PSScriptRoot 'Run-Hidden.ps1') 'git' @('status', '--porcelain', '--untracked-files=all') $root) | Out-String).Trim().Length -gt 0)
    Assert-True ($manifest.commit -eq $head) "Package commit '$($manifest.commit)' does not match HEAD '$head'."
    Assert-True ($manifest.dirty -eq $dirty) 'Package dirty marker does not match the current tracked worktree.'
}

$actual = @{}
foreach ($file in Get-ChildItem -LiteralPath $app -Recurse -File) {
    $relative = [IO.Path]::GetRelativePath($app, $file.FullName).Replace('\', '/')
    if ($relative -ne 'manifest.json') {
        Assert-True (-not $actual.ContainsKey($relative)) "Duplicate case-insensitive package path: $relative"
        $actual[$relative] = $file.FullName
    }
}

$declared = @{}
foreach ($entry in $manifest.files.GetEnumerator()) {
    $relative = [string]$entry.Key
    $digest = [string]$entry.Value
    Assert-True ($relative -cmatch '^[^/\\]+(?:/[^/\\]+)*$') "Invalid manifest path: $relative"
    Assert-True (-not [IO.Path]::IsPathRooted($relative) -and $relative -notmatch '(^|/)\.\.(/|$)') "Unsafe manifest path: $relative"
    Assert-True ($digest -cmatch '^[0-9a-f]{64}$') "Invalid SHA-256 digest for $relative"
    Assert-True (-not $declared.ContainsKey($relative)) "Duplicate case-insensitive manifest path: $relative"
    $declared[$relative] = $digest
}

$missing = @($declared.Keys | Where-Object { -not $actual.ContainsKey($_) } | Sort-Object)
$unexpected = @($actual.Keys | Where-Object { -not $declared.ContainsKey($_) } | Sort-Object)
Assert-True ($missing.Count -eq 0) "Manifest files are missing from the package: $($missing -join ', ')"
Assert-True ($unexpected.Count -eq 0) "Package contains undeclared files: $($unexpected -join ', ')"

foreach ($relative in $declared.Keys) {
    $digest = (Get-FileHash -LiteralPath $actual[$relative] -Algorithm SHA256).Hash.ToLowerInvariant()
    Assert-True ($digest -ceq $declared[$relative]) "SHA-256 mismatch: $relative"
}

$required = @(
    'HardwarePulse.exe', 'Pulse.Desktop.dll', 'Pulse.Desktop.deps.json', 'Pulse.Desktop.runtimeconfig.json',
    'Pulse.Core.dll', 'Pulse.Adapters.Windows.Modern.dll',
    'worker/HardwarePulse.Collector.exe', 'worker/HardwarePulse.Collector.exe.config',
    'worker/Pulse.Core.dll', 'worker/Pulse.Adapters.Windows.dll', 'tools/PresentMon.exe',
    'dependencies.lock.json', 'packages.lock.json', 'LICENSE', 'PRIVACY.md', 'SIGNING.md',
    'README.txt', 'README.zh-CN.txt',
    'licenses/Avalonia-MIT.txt', 'licenses/Avalonia-NOTICE.md', 'licenses/Avalonia-ANGLE-LICENSE.txt',
    'licenses/DotNet-MIT.txt', 'licenses/DotNet-NOTICES.txt', 'licenses/MicroCom-MIT.txt',
    'licenses/SkiaSharp-MIT.txt', 'licenses/HarfBuzzSharp-MIT.txt',
    'licenses/SkiaSharp-HarfBuzzSharp-NOTICES.txt', 'licenses/LobeIcons-MIT.txt',
    'licenses/TokenMonitor.txt', 'licenses/NotoSansCJK-OFL.txt', 'licenses/SOURCES.md',
    'licenses/LibreHardwareMonitor-MPL-2.0.txt', 'licenses/LibreHardwareMonitor-NOTICES.txt',
    'licenses/LibreHardwareMonitor-source-0.9.6.zip', 'licenses/PawnIO-COPYING.txt',
    'licenses/PawnIO-source-2.2.0.zip', 'licenses/PresentMon-LICENSE.txt',
    'licenses/PresentMon-THIRD-PARTY.txt'
)
foreach ($relative in $required) { Assert-RequiredFile $relative }
$workerVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $app 'worker/HardwarePulse.Collector.exe')).ProductVersion
Assert-True ($workerVersion -eq $expectedVersion) 'Collector version does not match shared host.'

$forbidden = @(Get-ChildItem -LiteralPath $app -Recurse -File | Where-Object {
    $_.Extension -in @('.pdb', '.ps1', '.cs', '.csproj', '.user', '.suo') -or
    $_.Name -ieq 'auth.json' -or $_.Name -match 'Native(?:Tests|CollectorBench)'
})
Assert-True ($forbidden.Count -eq 0) "Package contains forbidden development/private files: $((@($forbidden | ForEach-Object { [IO.Path]::GetRelativePath($app, $_.FullName) })) -join ', ')"

foreach ($relative in @('HardwarePulse.exe', 'worker/HardwarePulse.Collector.exe', 'tools/PresentMon.exe')) {
    Assert-True ((Get-PeMachine $relative) -eq 0x8664) "Package executable is not AMD64: $relative"
}

$runtime = Get-Content -LiteralPath (Join-Path $app 'Pulse.Desktop.runtimeconfig.json') -Raw | ConvertFrom-Json
Assert-True ($null -eq $runtime.runtimeOptions.framework -and $null -eq $runtime.runtimeOptions.frameworks) 'Desktop runtime config is framework-dependent.'
$included = @($runtime.runtimeOptions.includedFrameworks)
Assert-True ($included.Count -eq 1) 'Desktop runtime config must declare exactly one bundled framework.'
Assert-True ($included[0].name -eq 'Microsoft.NETCore.App' -and $included[0].version -eq '10.0.12') 'Unexpected bundled .NET runtime.'
foreach ($relative in @('hostfxr.dll', 'coreclr.dll', 'libSkiaSharp.dll', 'libHarfBuzzSharp.dll')) { Assert-RequiredFile $relative }

foreach ($relative in @('Pulse.Desktop.deps.json', 'dependencies.lock.json', 'packages.lock.json')) {
    $null = Get-Content -LiteralPath (Join-Path $app $relative) -Raw | ConvertFrom-Json
}

"PASS: verified shared Windows x64 package ($($actual.Count) files, version $($manifest.version), commit $($manifest.commit.Substring(0, 12)))."
