param([Parameter(Mandatory=$true)][string]$AppDirectory,[int]$Seconds=30)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$state=Join-Path $root ('vendor/ui-measure-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $state | Out-Null
$info=[Diagnostics.ProcessStartInfo]::new()
$info.FileName=Join-Path $AppDirectory 'NativeTests.exe'
$info.WorkingDirectory=$root
$info.UseShellExecute=$false;$info.CreateNoWindow=$true
$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
$info.ArgumentList.Add($state);$info.ArgumentList.Add('bench')
$process=[Diagnostics.Process]::Start($info)
$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
try {
    $watch=[Diagnostics.Stopwatch]::StartNew()
    while(-not (Test-Path "$state/ready.json")){
        if($process.HasExited -or $watch.Elapsed.TotalSeconds -gt 20){throw 'UI benchmark did not start'}
        Start-Sleep -Milliseconds 100
    }
    $snapshot=Get-Content "$state/runtime/snapshot.json" -Raw | ConvertFrom-Json
    $samples=@();$cpuStart=$null;$start=$null
    for($i=0;$i -lt [Math]::Ceiling(($Seconds+10)/2);$i++){
        $snapshot.time=[DateTimeOffset]::Now.ToString('o');$snapshot.sequence=$i+2
        $snapshot.sensors[0].value=50+$i%40
        $temp="$state/runtime/sample.tmp"
        [IO.File]::WriteAllText($temp,($snapshot|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
        [IO.File]::Move($temp,"$state/runtime/snapshot.json",$true)
        if($i -eq 5){$process.Refresh();$cpuStart=$process.TotalProcessorTime;$start=[Diagnostics.Stopwatch]::StartNew()}
        Start-Sleep -Seconds 2
        if($i -ge 5){$process.Refresh();$samples+= [pscustomobject]@{workingSet=$process.WorkingSet64;privateBytes=$process.PrivateMemorySize64}}
    }
    $process.Refresh()
    $result=[pscustomobject]@{app=$AppDirectory;seconds=$start.Elapsed.TotalSeconds;cpuPercent=100*($process.TotalProcessorTime-$cpuStart).TotalSeconds/$start.Elapsed.TotalSeconds/[Environment]::ProcessorCount;workingSetMiB=($samples.workingSet|Measure-Object -Average).Average/1MB;privateMiB=($samples.privateBytes|Measure-Object -Average).Average/1MB;samples=$samples.Count}
    $result|ConvertTo-Json|Set-Content "$state/result.json" -Encoding utf8
    $result|ConvertTo-Json
} finally {
    Set-Content "$state/BENCH-STOP" 'done'
    if(-not $process.WaitForExit(10000)){throw 'Benchmark process did not stop'}
    if($process.ExitCode -ne 0){throw $stderr.Result}
    $process.Dispose()
}
