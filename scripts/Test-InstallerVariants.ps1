param([switch]$SharedDesktop)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$compiler=Join-Path $root 'vendor/inno/ISCC.exe'
if(-not(Test-Path $compiler)){throw 'Run Build.ps1 -Installer first to prepare the pinned Inno compiler.'}
$output=Join-Path $root ('vendor/installer-variants-'+[Guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path $output
$variants=@('modern','win7')
if($SharedDesktop){$variants+='shared'}
foreach($name in $variants){
    $legacy=$name -eq 'win7'
    $shared=$name -eq 'shared'
    $expanded=Join-Path $output ($name+'.iss')
    $arguments=@('/O-',('/DValidationOutput='+$expanded),"$root/installer/HardwarePulse.iss")
    if($legacy){$arguments=@('/DWin7Compatibility')+$arguments}
    if($shared){
        $version=([xml](Get-Content "$root/src/Hosts/Desktop/Pulse.Desktop.csproj" -Raw)).Project.PropertyGroup.Version
        if(-not $version){throw 'Shared version is missing'}
        $arguments=@('/DSharedDesktop',('/DSharedVersion='+$version))+$arguments
    }
    & "$PSScriptRoot/Run-Hidden.ps1" $compiler $arguments $root | Out-File (Join-Path $output ($name+'.log')) -Encoding utf8
    $script=[IO.File]::ReadAllText($expanded)
    $startup=if($shared){'{app}\worker\HardwarePulse.Collector.exe'}else{'{app}\HardwarePulse.exe'}
    foreach($required in @(("Filename: `"$startup`"; Parameters: `"--remove-startup`""),("Exec(ExpandConstant('$startup'), '--install-startup'"))){
        if(-not $script.Contains($required)){throw "Incorrect management owner in ${name}: $required"}
    }
    $launches=@($script -split '\r?\n' | Where-Object {$_ -match '^Filename: .*Check: IsPulseInstallReady'})
    if($launches.Count -ne 2 -or @($launches | Where-Object {$_ -notmatch '^Filename: "\{app\}\\HardwarePulse.exe";'}).Count){throw "Installer launch must use UI in $name"}
    if($shared){
        foreach($required in @('..\build\windows-shared\app\*',"AppVersion=$version","OutputBaseFilename=HardwarePulse-Shared-$version-Setup",'collector-host-error.txt')){
            if(-not $script.Contains($required)){throw "Shared variant contract missing: $required"}
        }
        if($script.Contains('..\build\app\*')){throw 'WPF payload leaked into shared installer'}
    }elseif($script.Contains('..\build\windows-shared\app\*') -or $script.Contains('collector-host-error.txt')){throw "Shared configuration leaked into $name"}
    foreach($required in @('Check: IsPulseInstallReady and not IsPulseUpdate','Flags: postinstall nowait runasoriginaluser; Check: IsPulseInstallReady and IsPulseUpdate','MarkPulseInstallComplete();','GetCustomSetupExitCode','if PulseInstallReady then Result := 0 else Result := 10','Hardware Pulse setup is incomplete')){
        if(-not $script.Contains($required)){throw "Install outcome guard missing in ${name}: $required"}
    }
    foreach($required in @('AppId={{75E8FDDA-D799-4D8A-882D-972DC72151C2}','ArchitecturesAllowed=x64compatible','PrivilegesRequired=admin','Release >= 528040','--install-startup','CollectorRunning(Service, ExpectedPath)','IsPulseCollector(Process.ExecutablePath, CommandLine, ExpectedPath)',"OR Name = ''HardwarePulse.Collector.exe''",'\worker\HardwarePulse.Collector.exe')){
        if(-not $script.Contains($required)){throw "Shared installer contract missing in ${name}: $required"}
    }
    if($legacy){
        foreach($required in @('MinVersion=6.1sp1','OnlyBelowVersion=6.2','OutputBaseFilename=HardwarePulse-Win7-x64-Setup','Excludes: "tools\PresentMon.exe"','https://dotnet.microsoft.com/download/dotnet-framework/net48','DisableWelcomePage=no','WizardForm.WelcomeLabel2.Caption','FPS and Local Contrast are not supported.','不支持 FPS 和局部对比度。','不支援 FPS 和局部對比度。')){
            if(-not $script.Contains($required)){throw "Legacy installer contract missing: $required"}
        }
        if($script -match 'PawnIO|MinVersion=10\.0'){throw 'Legacy installer includes incompatible driver or OS gate'}
    }else{
        foreach($required in @('MinVersion=10.0.19045','PawnIO-2.2.0.exe','if not PawnIOPresent() then begin')){
            if(-not $script.Contains($required)){throw "Modern installer contract missing: $required"}
        }
        if($script -match 'OnlyBelowVersion|Excludes:|Win7-x64-Setup|DisableWelcomePage=no|WelcomeLabel2.Caption'){throw 'Legacy installer options leaked into modern build'}
    }
}
if($SharedDesktop){
    foreach($invalid in @(@('/DSharedDesktop'),@('/DSharedDesktop','/DSharedVersion=0.7.0','/DWin7Compatibility'))){
        $rejected=$false
        try{& "$PSScriptRoot/Run-Hidden.ps1" $compiler (@('/O-')+$invalid+@("$root/installer/HardwarePulse.iss")) $root}
        catch{
            $expected=if($invalid -contains '/DWin7Compatibility'){'SharedDesktop does not support Win7Compatibility'}else{'SharedDesktop requires SharedVersion'}
            if(-not $_.Exception.Message.Contains($expected)){throw}
            $rejected=$true
        }
        if(-not $rejected){throw 'Invalid shared installer combination accepted'}
    }
}
& "$PSScriptRoot/Run-Hidden.ps1" $compiler @("/O$output","$root/installer/tests/CollectorIdentity.iss") $root | Out-File (Join-Path $output 'identity-build.log') -Encoding utf8
$identityLog=Join-Path $output 'identity-run.log'
$rejected=$false
try{& "$PSScriptRoot/Run-Hidden.ps1" (Join-Path $output 'collector-identity.exe') @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',"/LOG=$identityLog") $root}
catch{if($_.Exception.Message -notmatch 'failed with exit code 1:'){throw};$rejected=$true}
if(-not $rejected -or -not(Test-Path $identityLog) -or -not([IO.File]::ReadAllText($identityLog).Contains('PASS collector identity:'))){throw 'Collector identity validation did not finish before rejecting setup'}
"PASS compiled installer variants: OS gates, prerequisite isolation, PresentMon exclusion, stable AppId and startup/upgrade contracts. Evidence: $output"
