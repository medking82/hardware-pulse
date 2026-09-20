#requires -Version 7.0
param([string]$DotNet,[string]$Python,[switch]$Installer,[string]$SigningCertificateThumbprint)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot))
# Reject an unavailable verification gate before touching prior build evidence.
if(-not (Test-Path -LiteralPath "$PSScriptRoot/Test-WindowsShared.ps1" -PathType Leaf)){
    throw 'Windows shared package verifier is unavailable. Existing build output was preserved; resolve the verifier quarantine before building.'
}
if(-not $DotNet){$DotNet=Join-Path $root 'vendor/dotnet-sdk/dotnet.exe'}
if(-not $Python){$Python=(Get-Command python.exe -ErrorAction Stop).Source}
$commit=((& "$PSScriptRoot/Run-Hidden.ps1" 'git' @('rev-parse','HEAD') $root)|Out-String).Trim()
$dirty=((& "$PSScriptRoot/Run-Hidden.ps1" 'git' @('status','--porcelain','--untracked-files=all') $root)|Out-String).Trim().Length -gt 0
$version=([xml](Get-Content "$root/src/Hosts/Desktop/Pulse.Desktop.csproj" -Raw)).Project.PropertyGroup.Version
$kind=if($version -match '-'){'windows-development'}else{'windows-release'}
if($kind -eq 'windows-release' -and $dirty){throw 'Stable Windows payloads require a clean tracked worktree.'}
$output=[IO.Path]::GetFullPath((Join-Path $root 'build/windows-shared'))
for($check=$output;$check.Length -ge $root.Length;$check=[IO.Path]::GetDirectoryName($check)){
    if((Test-Path -LiteralPath $check) -and ((Get-Item -LiteralPath $check).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Shared output traverses a reparse point'}
}
if(Test-Path -LiteralPath $output){
    if($output -ne [IO.Path]::GetFullPath((Join-Path $root 'build/windows-shared'))){throw 'Unexpected shared output'}
    if(Get-ChildItem -LiteralPath $output -Recurse -Force | Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}){throw 'Shared output contains a reparse point'}
    Remove-Item -LiteralPath $output -Recurse -Force
}
$app=Join-Path $output 'app'
$null=New-Item -ItemType Directory -Path $app -Force
& "$PSScriptRoot/Run-Hidden.ps1" $Python @('scripts/prepare_desktop_fonts.py') $root
& "$PSScriptRoot/Build-WindowsCollector.ps1"
& "$PSScriptRoot/Run-Hidden.ps1" $DotNet @('restore','src/Hosts/Desktop/Pulse.Desktop.csproj','--locked-mode') $root
& "$PSScriptRoot/Run-Hidden.ps1" $DotNet @('publish','src/Hosts/Desktop/Pulse.Desktop.csproj','-c','Release','-r','win-x64','--self-contained','true','--no-restore','--disable-build-servers','-p:UseSharedCompilation=false','-p:DebugType=None','-p:DebugSymbols=false','-o',$app) $root
# AppHost embeds the managed DLL name. Rename only the native entry point so
# the worker authenticates HardwarePulse.exe and Avalonia resource URIs stay valid.
Move-Item -LiteralPath "$app/Pulse.Desktop.exe" -Destination "$app/HardwarePulse.exe"
foreach($name in @('libSkiaSharp.pdb','libHarfBuzzSharp.pdb')){if(Test-Path -LiteralPath "$app/$name"){Remove-Item -LiteralPath "$app/$name"}}
Copy-Item "$root/build/windows-worker/*" $app -Recurse
Copy-Item "$root/src/Hosts/Desktop/packages.lock.json","$root/PRIVACY.md","$root/SIGNING.md" $app
Copy-Item "$root/docs/distribution/README.windows-shared.txt" "$app/README.txt"
Copy-Item "$root/docs/distribution/README.windows-shared.zh-CN.txt" "$app/README.zh-CN.txt"
if($SigningCertificateThumbprint){
    foreach($relative in @('HardwarePulse.exe','worker/HardwarePulse.Collector.exe')){
        & "$PSScriptRoot/Sign.ps1" -Path (Join-Path $app $relative) -Thumbprint $SigningCertificateThumbprint
    }
}
$files=[ordered]@{}
foreach($file in Get-ChildItem -LiteralPath $app -Recurse -File | Sort-Object FullName){
    $relative=[IO.Path]::GetRelativePath($app,$file.FullName).Replace('\','/')
    $files[$relative]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
}
@{schema=1;kind=$kind;version=$version;commit=$commit;dirty=$dirty;rid='win-x64';files=$files} | ConvertTo-Json -Depth 4 | Set-Content "$app/manifest.json" -Encoding utf8NoBOM
& "$PSScriptRoot/Test-WindowsShared.ps1"
if($Installer){
    & "$PSScriptRoot/Test-InstallerVariants.ps1" -SharedDesktop
    & "$PSScriptRoot/Run-Hidden.ps1" "$root/vendor/inno/ISCC.exe" @('/DSharedDesktop',"/DSharedVersion=$version","$root/installer/HardwarePulse.iss") $root
    if($SigningCertificateThumbprint){& "$PSScriptRoot/Sign.ps1" -Path "$root/dist/HardwarePulse-Shared-$version-Setup.exe" -Thumbprint $SigningCertificateThumbprint}
    Get-FileHash -LiteralPath "$root/dist/HardwarePulse-Shared-$version-Setup.exe" -Algorithm SHA256
}
"Built shared Windows x64 $kind payload: $app"
