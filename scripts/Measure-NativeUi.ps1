param([Parameter(Mandatory=$true)][string]$AppDirectory,[ValidateRange(2,3600)][int]$Seconds=30,[ValidateSet('monitor','tray','desktop','desktop-contrast','desktop-dynamic','desktop-dynamic-contrast')][string]$Scene='monitor',[ValidateSet('Wpf','Shared')][string]$HostKind='Wpf',[ValidateRange(360,1600)][int]$Width=440,[ValidateRange(400,1600)][int]$Height=640,[switch]$ExternalBackground)
$ErrorActionPreference='Stop'
if($ExternalBackground -and -not $Scene.StartsWith('desktop-dynamic')){throw 'External background requires a dynamic Desktop scene'}
$root=Split-Path $PSScriptRoot
$AppDirectory=[IO.Path]::GetFullPath($AppDirectory)
$appFile=Join-Path $AppDirectory 'HardwarePulse.exe'
$version=[Reflection.AssemblyName]::GetAssemblyName($appFile).Version.ToString()
$appHash=(Get-FileHash $appFile -Algorithm SHA256).Hash
$coreHash=(Get-FileHash (Join-Path $AppDirectory 'Pulse.Core.dll') -Algorithm SHA256).Hash
$adapterHash=(Get-FileHash (Join-Path $AppDirectory 'Pulse.Adapters.Windows.dll') -Algorithm SHA256).Hash
$harnessHash=(Get-FileHash (Join-Path $AppDirectory 'NativeTests.exe') -Algorithm SHA256).Hash
if($HostKind -eq 'Shared'){
    $shared=Join-Path $root 'scripts/DesktopTests/bin/Release/net10.0'
    $appFile=Join-Path $shared 'Pulse.Desktop.dll'
    $version=[Reflection.AssemblyName]::GetAssemblyName($appFile).Version.ToString()
    $appHash=(Get-FileHash $appFile -Algorithm SHA256).Hash
    $coreHash=(Get-FileHash (Join-Path $shared 'Pulse.Core.dll') -Algorithm SHA256).Hash
    $adapterHash=(Get-FileHash (Join-Path $shared 'Pulse.Adapters.Windows.Modern.dll') -Algorithm SHA256).Hash
    $harnessHash=(Get-FileHash (Join-Path $shared 'Pulse.Desktop.Tests.dll') -Algorithm SHA256).Hash
}
$state=Join-Path $root ('vendor/ui-measure-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $state | Out-Null
$info=[Diagnostics.ProcessStartInfo]::new()
$info.FileName=Join-Path $AppDirectory 'NativeTests.exe'
$info.WorkingDirectory=$root
$info.UseShellExecute=$false;$info.CreateNoWindow=$true
$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
if($HostKind -eq 'Shared'){
    & "$PSScriptRoot/Run-Hidden.ps1" (Join-Path $AppDirectory 'NativeTests.exe') @('--benchmark-snapshot',(Join-Path $state 'runtime/snapshot.json')) $root
    $info.FileName=Join-Path $root 'vendor/dotnet-sdk/dotnet.exe'
    $info.ArgumentList.Add((Join-Path $shared 'Pulse.Desktop.Tests.dll'));$info.ArgumentList.Add('--ui-benchmark');$info.ArgumentList.Add($state)
}else{$info.ArgumentList.Add($state);$info.ArgumentList.Add('bench')}
$info.ArgumentList.Add($Scene);$info.ArgumentList.Add([string]$Width);$info.ArgumentList.Add([string]$Height)
$info.Environment['PULSE_BENCHMARK_EXTERNAL_BACKGROUND']=$(if($ExternalBackground){'1'}else{'0'})
$process=$null;$backgroundProcess=$null;$backgroundSamples=@()
$samples=@();$failure=$null;$measurementComplete=$false
try {
    if($ExternalBackground){
        $backgroundInfo=[Diagnostics.ProcessStartInfo]::new()
        $backgroundInfo.FileName=Join-Path $AppDirectory 'NativeTests.exe'
        $backgroundInfo.WorkingDirectory=$root;$backgroundInfo.UseShellExecute=$false;$backgroundInfo.CreateNoWindow=$true
        $backgroundInfo.RedirectStandardOutput=$true;$backgroundInfo.RedirectStandardError=$true
        foreach($argument in @('--benchmark-backdrop',$state,[string]$Width,[string]$Height)){$backgroundInfo.ArgumentList.Add($argument)}
        $backgroundProcess=[Diagnostics.Process]::Start($backgroundInfo)
        $backgroundStdout=$backgroundProcess.StandardOutput.ReadToEndAsync();$backgroundStderr=$backgroundProcess.StandardError.ReadToEndAsync()
        $backgroundWatch=[Diagnostics.Stopwatch]::StartNew()
        while(-not (Test-Path "$state/background-ready.json")){
            if($backgroundProcess.HasExited -or $backgroundWatch.Elapsed.TotalSeconds -gt 20){throw 'External background did not start'}
            Start-Sleep -Milliseconds 100
        }
        $backgroundReady=Get-Content "$state/background-ready.json" -Raw|ConvertFrom-Json
    }
    $process=[Diagnostics.Process]::Start($info)
    $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
    $watch=[Diagnostics.Stopwatch]::StartNew()
    while(-not (Test-Path "$state/ready.json")){
        if($process.HasExited -or $watch.Elapsed.TotalSeconds -gt 20){throw 'UI benchmark did not start'}
        Start-Sleep -Milliseconds 100
    }
    $ready=Get-Content "$state/ready.json" -Raw|ConvertFrom-Json
    if($ready.scene -ne $Scene){throw 'Benchmark harness scene mismatch; rebuild NativeTests.exe'}
    if([Math]::Abs($ready.widthDip-$Width) -gt 1 -or [Math]::Abs($ready.heightDip-$Height) -gt 1){throw 'Benchmark window did not reach the requested dimensions'}
    if([bool]$ready.externalBackground -ne [bool]$ExternalBackground){throw 'Benchmark background ownership mismatch; rebuild harness'}
    if($ExternalBackground){
        foreach($field in @('leftPixels','topPixels','widthPixels','heightPixels')){
            if([Math]::Abs($ready.$field-$backgroundReady.$field) -gt 1){throw "External background geometry mismatch: $field"}
        }
    }
    $snapshot=Get-Content "$state/runtime/snapshot.json" -Raw | ConvertFrom-Json
    $cpuStart=$null;$start=$null
    for($i=0;$i -lt [Math]::Ceiling(($Seconds+10)/2);$i++){
        if($process.HasExited){throw 'UI benchmark exited during sampling'}
        if($ExternalBackground -and $backgroundProcess.HasExited){throw 'External background exited during sampling'}
        $snapshot.time=[DateTimeOffset]::Now.ToString('o');$snapshot.sequence=$i+2
        $snapshot.sensors[0].value=50+$i%40
        $temp="$state/runtime/sample.tmp"
        [IO.File]::WriteAllText($temp,($snapshot|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
        [IO.File]::Move($temp,"$state/runtime/snapshot.json",$true)
        if($i -eq 5){$process.Refresh();$cpuStart=$process.TotalProcessorTime;if($ExternalBackground){$backgroundProcess.Refresh();$backgroundCpuStart=$backgroundProcess.TotalProcessorTime};$start=[Diagnostics.Stopwatch]::StartNew()}
        Start-Sleep -Seconds 2
        if($process.HasExited){throw 'UI benchmark exited during sampling'}
        if($i -ge 5){$process.Refresh();$samples+= [pscustomobject]@{seconds=$start.Elapsed.TotalSeconds;cpuSeconds=($process.TotalProcessorTime-$cpuStart).TotalSeconds;workingSet=$process.WorkingSet64;privateBytes=$process.PrivateMemorySize64}}
        if($i -ge 5 -and $ExternalBackground){$backgroundProcess.Refresh();$backgroundSamples+=[pscustomobject]@{seconds=$start.Elapsed.TotalSeconds;cpuSeconds=($backgroundProcess.TotalProcessorTime-$backgroundCpuStart).TotalSeconds;workingSet=$backgroundProcess.WorkingSet64;privateBytes=$backgroundProcess.PrivateMemorySize64}}
    }
    $process.Refresh()
    $result=[pscustomobject]@{scope='Isolated UI harness; excludes live collector, FPS, quota requests and desktop layer integration';harnessSha256=$harnessHash;scene=$Scene;background=$(if($Scene -in @('monitor','tray')){'host desktop'}elseif($Scene.StartsWith('desktop-dynamic')){'moving black-white-gray gradient'}else{'fixed black-white-gray gradient'});backgroundIntervalMilliseconds=$ready.backgroundIntervalMilliseconds;backgroundPeriodSeconds=$ready.backgroundPeriodSeconds;localContrast=$ready.localContrast;version=$version;appSha256=$appHash;coreSha256=$coreHash;adapterSha256=$adapterHash;logicalProcessors=[Environment]::ProcessorCount;warmupSeconds=10;requestedSeconds=$Seconds;sampleIntervalSeconds=2;widthDip=$ready.widthDip;heightDip=$ready.heightDip;widthPixels=$ready.widthPixels;heightPixels=$ready.heightPixels;app=$AppDirectory;seconds=$start.Elapsed.TotalSeconds;cpuPercent=100*($process.TotalProcessorTime-$cpuStart).TotalSeconds/$start.Elapsed.TotalSeconds/[Environment]::ProcessorCount;workingSetMiB=($samples.workingSet|Measure-Object -Average).Average/1MB;privateMiB=($samples.privateBytes|Measure-Object -Average).Average/1MB;samples=$samples.Count}
    Set-Content "$state/BENCH-STOP" 'done'
    if(-not $process.WaitForExit(10000) -or $process.ExitCode -ne 0){throw 'Benchmark did not complete successfully'}
    $completed=Get-Content "$state/completed.json" -Raw|ConvertFrom-Json
    if($ExternalBackground){
        Set-Content "$state/BACKGROUND-STOP" 'done'
        if(-not $backgroundProcess.WaitForExit(10000) -or $backgroundProcess.ExitCode -ne 0){throw 'External background did not complete successfully'}
        if($completed.backgroundUpdates -ne 0){throw 'UI unexpectedly rendered its own background'}
        $completed=Get-Content "$state/background-completed.json" -Raw|ConvertFrom-Json
        $last=$backgroundSamples[-1]
        $result|Add-Member -NotePropertyName backgroundProcess -NotePropertyValue ([pscustomobject]@{harnessSha256=(Get-FileHash (Join-Path $AppDirectory 'NativeTests.exe')).Hash;cpuPercent=100*$last.cpuSeconds/$last.seconds/[Environment]::ProcessorCount;workingSetMiB=($backgroundSamples.workingSet|Measure-Object -Average).Average/1MB;privateMiB=($backgroundSamples.privateBytes|Measure-Object -Average).Average/1MB;samples=$backgroundSamples.Count})
    }
    $result|Add-Member -NotePropertyName externalBackground -NotePropertyValue ([bool]$ExternalBackground)
    if($Scene.StartsWith('desktop-dynamic') -and $completed.backgroundUpdates -le 0){throw 'Dynamic background did not advance'}
    $result|Add-Member -NotePropertyName backgroundUpdates -NotePropertyValue $completed.backgroundUpdates
    $result|Add-Member -NotePropertyName hostKind -NotePropertyValue $HostKind
    $result|Add-Member -NotePropertyName requestedWidthDip -NotePropertyValue $Width
    $result|Add-Member -NotePropertyName requestedHeightDip -NotePropertyValue $Height
    $result|Add-Member -NotePropertyName readingIntervalSeconds -NotePropertyValue 2
    $result|Add-Member -NotePropertyName stateDirectory -NotePropertyValue $state
    if($HostKind -eq 'Shared'){$result.scope='Isolated shared UI with the same synthetic snapshot at 2-second cadence; includes its own Desktop input/layer adapters; excludes live collector, FPS and quota requests';$result.app=$shared}
    if($ExternalBackground){$result.scope+='; external WPF background process measured separately'}
    $result|ConvertTo-Json|Set-Content "$state/result.json" -Encoding utf8
    $measurementComplete=$true
    $result|ConvertTo-Json
} catch {
    $failure=$_.Exception.Message;throw
} finally {
    [pscustomobject]@{scene=$Scene;complete=$measurementComplete;failure=$failure;processId=$process.Id;appSha256=$appHash;coreSha256=$coreHash;adapterSha256=$adapterHash;harnessSha256=$harnessHash;warmupSeconds=10;logicalProcessors=[Environment]::ProcessorCount;samples=@($samples);backgroundSamples=@($backgroundSamples)}|ConvertTo-Json -Depth 5|Set-Content "$state/samples.json" -Encoding utf8
    Set-Content "$state/BENCH-STOP" 'done'
    Set-Content "$state/BACKGROUND-STOP" 'done'
    $cleanupFailure=$null
    foreach($ownedProcess in @($process,$backgroundProcess)){
        if($null -eq $ownedProcess){continue}
        if(-not $ownedProcess.WaitForExit(10000)){$ownedProcess.Kill();$ownedProcess.WaitForExit();$cleanupFailure='Benchmark owned process required forced shutdown'}
        if($ownedProcess.ExitCode -ne 0){$cleanupFailure='Benchmark owned process exited with an error'}
        $ownedProcess.Dispose()
    }
    if($null -ne $stderr){[IO.File]::WriteAllText("$state/stderr.log",$stderr.Result)}
    if($null -ne $backgroundStderr){[IO.File]::WriteAllText("$state/background-stderr.log",$backgroundStderr.Result)}
    if($cleanupFailure){throw $cleanupFailure}
}
