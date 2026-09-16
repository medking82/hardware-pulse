$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$compiler=Join-Path $root 'vendor/inno/ISCC.exe'
if(-not(Test-Path $compiler)){throw 'Run Build.ps1 -Installer first to prepare the pinned Inno compiler.'}
$output=Join-Path $root ('vendor/installer-variants-'+[Guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path $output
foreach($legacy in @($false,$true)){
    $name=if($legacy){'win7'}else{'modern'}
    $expanded=Join-Path $output ($name+'.iss')
    $arguments=@('/O-',('/DValidationOutput='+$expanded),"$root/installer/HardwarePulse.iss")
    if($legacy){$arguments=@('/DWin7Compatibility')+$arguments}
    & "$PSScriptRoot/Run-Hidden.ps1" $compiler $arguments $root | Out-File (Join-Path $output ($name+'.log')) -Encoding utf8
    $script=[IO.File]::ReadAllText($expanded)
    foreach($required in @('AppId={{75E8FDDA-D799-4D8A-882D-972DC72151C2}','ArchitecturesAllowed=x64compatible','PrivilegesRequired=admin','Release >= 528040','--install-startup','CollectorRunning(Service, ExpectedPath)')){
        if(-not $script.Contains($required)){throw "Shared installer contract missing in ${name}: $required"}
    }
    if($legacy){
        foreach($required in @('MinVersion=6.1sp1','OnlyBelowVersion=6.2','OutputBaseFilename=HardwarePulse-Win7-x64-Setup','Excludes: "tools\PresentMon.exe"','https://dotnet.microsoft.com/download/dotnet-framework/net48')){
            if(-not $script.Contains($required)){throw "Legacy installer contract missing: $required"}
        }
        if($script -match 'PawnIO|MinVersion=10\.0'){throw 'Legacy installer includes incompatible driver or OS gate'}
    }else{
        foreach($required in @('MinVersion=10.0.19045','PawnIO-2.2.0.exe','if not PawnIOPresent() then begin')){
            if(-not $script.Contains($required)){throw "Modern installer contract missing: $required"}
        }
        if($script -match 'OnlyBelowVersion|Excludes:|Win7-x64-Setup'){throw 'Legacy installer options leaked into modern build'}
    }
}
"PASS compiled installer variants: OS gates, prerequisite isolation, PresentMon exclusion, stable AppId and startup/upgrade contracts. Evidence: $output"
