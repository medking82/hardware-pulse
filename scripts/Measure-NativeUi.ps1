param([Parameter(Mandatory=$true)][string]$AppDirectory,[ValidateRange(2,3600)][int]$Seconds=30,[ValidateSet('monitor','desktop','desktop-contrast','desktop-dynamic','desktop-dynamic-contrast')][string]$Scene='monitor')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$AppDirectory=[IO.Path]::GetFullPath($AppDirectory)
$appFile=Join-Path $AppDirectory 'HardwarePulse.exe'
$version=[Reflection.AssemblyName]::GetAssemblyName($appFile).Version.ToString()
$appHash=(Get-FileHash $appFile -Algorithm SHA256).Hash
$coreHash=(Get-FileHash (Join-Path $AppDirectory 'Pulse.Core.dll') -Algorithm SHA256).Hash
$adapterHash=(Get-FileHash (Join-Path $AppDirectory 'Pulse.Adapters.Windows.dll') -Algorithm SHA256).Hash
$harnessHash=(Get-FileHash (Join-Path $AppDirectory 'NativeTests.exe') -Algorithm SHA256).Hash
$state=Join-Path $root ('vendor/ui-measure-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $state | Out-Null
$info=[Diagnostics.ProcessStartInfo]::new()
$info.FileName=Join-Path $AppDirectory 'NativeTests.exe'
$info.WorkingDirectory=$root
$info.UseShellExecute=$false;$info.CreateNoWindow=$true
$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
$info.ArgumentList.Add($state);$info.ArgumentList.Add('bench');$info.ArgumentList.Add($Scene)
$process=[Diagnostics.Process]::Start($info)
$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
$samples=@();$failure=$null;$measurementComplete=$false
try {
    $watch=[Diagnostics.Stopwatch]::StartNew()
    while(-not (Test-Path "$state/ready.json")){
        if($process.HasExited -or $watch.Elapsed.TotalSeconds -gt 20){throw 'UI benchmark did not start'}
        Start-Sleep -Milliseconds 100
    }
    $ready=Get-Content "$state/ready.json" -Raw|ConvertFrom-Json
    if($ready.scene -ne $Scene){throw 'Benchmark harness scene mismatch; rebuild NativeTests.exe'}
    $snapshot=Get-Content "$state/runtime/snapshot.json" -Raw | ConvertFrom-Json
    $cpuStart=$null;$start=$null
    for($i=0;$i -lt [Math]::Ceiling(($Seconds+10)/2);$i++){
        if($process.HasExited){throw 'UI benchmark exited during sampling'}
        $snapshot.time=[DateTimeOffset]::Now.ToString('o');$snapshot.sequence=$i+2
        $snapshot.sensors[0].value=50+$i%40
        $temp="$state/runtime/sample.tmp"
        [IO.File]::WriteAllText($temp,($snapshot|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
        [IO.File]::Move($temp,"$state/runtime/snapshot.json",$true)
        if($i -eq 5){$process.Refresh();$cpuStart=$process.TotalProcessorTime;$start=[Diagnostics.Stopwatch]::StartNew()}
        Start-Sleep -Seconds 2
        if($process.HasExited){throw 'UI benchmark exited during sampling'}
        if($i -ge 5){$process.Refresh();$samples+= [pscustomobject]@{seconds=$start.Elapsed.TotalSeconds;cpuSeconds=($process.TotalProcessorTime-$cpuStart).TotalSeconds;workingSet=$process.WorkingSet64;privateBytes=$process.PrivateMemorySize64}}
    }
    $process.Refresh()
    $result=[pscustomobject]@{scope='Isolated UI harness; excludes live collector, FPS, quota requests and desktop layer integration';harnessSha256=$harnessHash;scene=$Scene;background=$(if($Scene -eq 'monitor'){'host desktop'}elseif($Scene.StartsWith('desktop-dynamic')){'moving black-white-gray gradient'}else{'fixed black-white-gray gradient'});backgroundIntervalMilliseconds=$ready.backgroundIntervalMilliseconds;backgroundPeriodSeconds=$ready.backgroundPeriodSeconds;localContrast=$ready.localContrast;version=$version;appSha256=$appHash;coreSha256=$coreHash;adapterSha256=$adapterHash;logicalProcessors=[Environment]::ProcessorCount;warmupSeconds=10;requestedSeconds=$Seconds;sampleIntervalSeconds=2;widthDip=$ready.widthDip;heightDip=$ready.heightDip;widthPixels=$ready.widthPixels;heightPixels=$ready.heightPixels;app=$AppDirectory;seconds=$start.Elapsed.TotalSeconds;cpuPercent=100*($process.TotalProcessorTime-$cpuStart).TotalSeconds/$start.Elapsed.TotalSeconds/[Environment]::ProcessorCount;workingSetMiB=($samples.workingSet|Measure-Object -Average).Average/1MB;privateMiB=($samples.privateBytes|Measure-Object -Average).Average/1MB;samples=$samples.Count}
    Set-Content "$state/BENCH-STOP" 'done'
    if(-not $process.WaitForExit(10000) -or $process.ExitCode -ne 0){throw 'Benchmark did not complete successfully'}
    $completed=Get-Content "$state/completed.json" -Raw|ConvertFrom-Json
    if($Scene.StartsWith('desktop-dynamic') -and $completed.backgroundUpdates -le 0){throw 'Dynamic background did not advance'}
    $result|Add-Member -NotePropertyName backgroundUpdates -NotePropertyValue $completed.backgroundUpdates
    $result|ConvertTo-Json|Set-Content "$state/result.json" -Encoding utf8
    $measurementComplete=$true
    $result|ConvertTo-Json
} catch {
    $failure=$_.Exception.Message;throw
} finally {
    [pscustomobject]@{scene=$Scene;complete=$measurementComplete;failure=$failure;processId=$process.Id;appSha256=$appHash;coreSha256=$coreHash;adapterSha256=$adapterHash;harnessSha256=$harnessHash;warmupSeconds=10;logicalProcessors=[Environment]::ProcessorCount;samples=@($samples)}|ConvertTo-Json -Depth 5|Set-Content "$state/samples.json" -Encoding utf8
    Set-Content "$state/BENCH-STOP" 'done'
    if(-not $process.WaitForExit(10000)){throw 'Benchmark process did not stop'}
    [IO.File]::WriteAllText("$state/stderr.log",$stderr.Result)
    if($process.ExitCode -ne 0){throw $stderr.Result}
    $process.Dispose()
}
