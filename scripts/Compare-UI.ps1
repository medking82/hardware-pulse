param([int]$WarmupSeconds=10,[int]$MeasureSeconds=60)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$baseline=Join-Path $root 'vendor/baseline-0.4.9'
if((Get-Item "$baseline/HardwarePulse.exe").VersionInfo.FileVersion -ne '0.4.9.0'){throw 'Benchmark requires the preserved 0.4.9 baseline build'}
$output=Join-Path $root ('vendor/compare-ui-'+[Guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory $output
Copy-Item $baseline "$output/baseline" -Recurse
$baseApp=Join-Path $output 'baseline'
$baseState=Join-Path $baseApp 'state'
$nativeState=Join-Path $output 'native-state'
foreach($state in @($baseState,$nativeState)){$null=New-Item -ItemType Directory "$state/runtime" -Force}
$source=[IO.File]::ReadAllText("$baseApp/Glass.ps1").Replace('Local\HardwarePulseGlass','Local\HardwarePulseBaselineBenchmark')
$source=$source.Replace('$script:tray.Visible=$true','$script:tray.Visible=$false')
$source=$source.Replace('$window.Show()', '$window.ShowInTaskbar=$false;$window.ShowActivated=$false;$window.Show()')
$source=$source.Replace('$null=Start-PulseLoop', '$window.Width=310;$window.Height=690;$window.Add_ContentRendered({[IO.File]::WriteAllText((Join-Path $script:stateRoot ''ready.json''),''{}'')});$null=Start-PulseLoop')
[IO.File]::WriteAllText("$baseApp/Glass.ps1",$source,[Text.UTF8Encoding]::new($true))
$paths=@'
$script:stateRoot=Join-Path $PSScriptRoot 'state'
$script:runtime=Join-Path $script:stateRoot 'runtime'
$PSDefaultParameterValues['Get-Content:Encoding']='UTF8'
'@
[IO.File]::WriteAllText("$baseApp/Paths.ps1",$paths,[Text.UTF8Encoding]::new($true))
$fixture=Get-ChildItem "$root/vendor" -Filter native-ui-* | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$raw=Get-Content (Join-Path $fixture.FullName 'runtime/snapshot.json') -Raw | ConvertFrom-Json
$logical=(Get-CimInstance Win32_ComputerSystem).NumberOfLogicalProcessors
$results=@()
foreach($case in @(@{name='baseline-0.4.9';exe="$baseApp/HardwarePulse.exe";state=$baseState;args=@()},@{name='native-candidate';exe="$root/build/native/app/NativeTests.exe";state=$nativeState;args=@($nativeState,'bench')})){
    $raw.time=[DateTimeOffset]::Now.ToString('o');[IO.File]::WriteAllText((Join-Path $case.state 'runtime/snapshot.json'),($raw | ConvertTo-Json -Depth 8 -Compress),[Text.UTF8Encoding]::new($false))
    $info=[Diagnostics.ProcessStartInfo]::new($case.exe);$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    foreach($argument in $case.args){$info.ArgumentList.Add($argument)}
    $clock=[Diagnostics.Stopwatch]::StartNew();$child=[Diagnostics.Process]::Start($info);$stdout=$child.StandardOutput.ReadToEndAsync();$stderr=$child.StandardError.ReadToEndAsync()
    try{
        while(-not(Test-Path (Join-Path $case.state 'ready.json'))){if($child.HasExited -or $clock.Elapsed.TotalSeconds -gt 20){throw "Benchmark startup failed or timed out (PID $($child.Id))"};Start-Sleep -Milliseconds 100}
        $startup=$clock.Elapsed.TotalMilliseconds;$private=@();$working=@();$cpu0=$null;$start=$null
        while($clock.Elapsed.TotalSeconds -lt ($startup/1000+$WarmupSeconds+$MeasureSeconds)){
            $raw.time=[DateTimeOffset]::Now.ToString('o');$raw.sequence++;[IO.File]::WriteAllText((Join-Path $case.state 'runtime/snapshot.json'),($raw | ConvertTo-Json -Depth 8 -Compress),[Text.UTF8Encoding]::new($false))
            $child.Refresh();if($child.HasExited){throw "Benchmark exited early: $($stderr.Result)"}
            if($clock.Elapsed.TotalSeconds -ge ($startup/1000+$WarmupSeconds)){
                if($null -eq $cpu0){$cpu0=$child.TotalProcessorTime.TotalSeconds;$start=$clock.Elapsed.TotalSeconds}
                $private+=$child.PrivateMemorySize64;$working+=$child.WorkingSet64
            }
            Start-Sleep -Seconds 2
        }
        $child.Refresh();$duration=$clock.Elapsed.TotalSeconds-$start
        $view=Get-Content (Join-Path $case.state 'view-status.json') -Raw | ConvertFrom-Json
        if($view.state -ne 'LIVE' -or $view.sensors -ne 7){throw 'Benchmark readings not equivalent'}
        $results+=[ordered]@{name=$case.name;startupMs=[Math]::Round($startup);warmupSeconds=$WarmupSeconds;sampleSeconds=[Math]::Round($duration,2);logicalProcessors=$logical;cpuTotalPercent=[Math]::Round(100*($child.TotalProcessorTime.TotalSeconds-$cpu0)/$duration/$logical,4);meanWorkingSetMB=[Math]::Round(($working | Measure-Object -Average).Average/1MB,2);meanPrivateMB=[Math]::Round(($private | Measure-Object -Average).Average/1MB,2);mappedReadings=$view.sensors}
    }finally{
        [IO.File]::WriteAllText((Join-Path $case.state 'BENCH-STOP'),'stop');[IO.File]::WriteAllText((Join-Path $case.state 'runtime/STOP'),'stop')
        if(-not $child.WaitForExit(10000)){$child.Kill();$child.WaitForExit()};$child.Dispose()
    }
}
$report=@{kind='UI replay comparison';window='310x690 DIP';pollSeconds=2;fixture='synthetic CPU/GPU/pump/storage; 7 mapped readings';limitations='Sequential single-run replay; excludes live collector, game ETW and battery power. Both startup markers use WPF ContentRendered. Native benchmark has one extra 2-second stop-check timer.';results=$results}
$report | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $output 'results.json') -Encoding utf8
$report | ConvertTo-Json -Depth 6
"Evidence: $output"
