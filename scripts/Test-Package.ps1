$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$app=Join-Path $root 'build/app'
$payload=@(Get-ChildItem $app -Recurse -File)
if($payload | Where-Object {$_.Extension -in @('.ps1','.cs') -or $_.Name -match '^(Native.*Tests|NativeCollectorBench|System.Management.Automation)\.'}){throw 'Legacy/development files in release package'}
$assembly=[Reflection.Assembly]::LoadFrom("$app/HardwarePulse.exe")
if($assembly.GetReferencedAssemblies().Name -contains 'System.Management.Automation'){throw 'PowerShell runtime reference remains'}
if($assembly.GetName().Version.ToString() -ne '0.6.3.0'){throw 'Wrong native assembly version'}
$installer=[IO.File]::ReadAllText("$root/installer/HardwarePulse.iss")
if(-not $installer.Contains('AppId={{75E8FDDA-D799-4D8A-882D-972DC72151C2}')){throw 'Upgrade application identity changed'}
$section=[regex]::Match($installer,'(?s)\[InstallDelete\](.*?)(?:\r?\n\[|$)').Groups[1].Value
$obsolete=@([regex]::Matches($section,'Type: files; Name: "\{app\}\\([^"\\]+)"') | ForEach-Object {$_.Groups[1].Value})
foreach($line in $section -split '\r?\n'){
    if($line.Trim() -and -not $line.Trim().StartsWith(';') -and $line -notmatch '^Type: files; Name: "\{app\}\\[A-Za-z0-9.-]+"$'){throw "Unbounded installer cleanup: $line"}
}
# Top-level source files were shipped by the pre-migration Build.ps1. Retaining
# them for differential checks must not leave them in an upgraded installation.
foreach($file in Get-ChildItem "$root/src" -File | Where-Object {$_.Extension -in @('.ps1','.cs')}){
    if($obsolete -notcontains $file.Name){throw "Old installed runtime file not retired: $($file.Name)"}
}
foreach($name in $obsolete){if(Test-Path (Join-Path $app $name)){throw "Cleanup deletes current payload file: $name"}}
if($installer -match 'PowerShellEngine|PSVersion'){throw 'Installer still requires PowerShell runtime'}
'PASS: native-only payload, stable upgrade AppId, complete bounded obsolete-file cleanup and no PowerShell prerequisite'
