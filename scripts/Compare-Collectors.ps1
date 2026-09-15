param([int]$WarmupSeconds=10,[int]$MeasureSeconds=60)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
if(-not [Security.Principal.WindowsPrincipal]::new($identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Elevated read-only collector benchmark required'}
$output=Join-Path $root ('vendor/compare-collectors-'+[Guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory $output
[IO.File]::WriteAllText("$root/build/native/collector-benchmark-path.txt",$output)
trap { $_ | Out-String | Set-Content "$output/benchmark-error.txt" -Encoding utf8; break }
Add-Type -AssemblyName System.Web.Extensions
$null=[Reflection.Assembly]::LoadFrom("$root/build/native/app/HardwarePulse.exe")
$null=[Reflection.Assembly]::LoadFrom("$root/build/native/app/Pulse.Adapters.Windows.dll")
$store=[HardwarePulse.SchedulerStore]::new()
$installed=Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'Hardware Pulse/HardwarePulse.exe'
$startup=[HardwarePulse.Startup]::new($store,$installed,$identity.User.Value)
foreach($name in @('Hardware Pulse Widget','Hardware Pulse Collector')){$startup.Validate($name,$store.Get($name))}
$runtime=Join-Path ([Environment]::GetFolderPath('CommonApplicationData')) ('HardwarePulse/'+$identity.User.Value+'/runtime')
$original=Get-Content "$runtime/snapshot.json" -Raw | ConvertFrom-Json
$logical=(Get-CimInstance Win32_ComputerSystem).NumberOfLogicalProcessors
$restoreNeeded=$false;$results=@();$failure=$null
try{
    if((Get-Item "$root/vendor/baseline-0.4.9/HardwarePulse.exe").VersionInfo.FileVersion -ne '0.4.9.0'){throw 'Preserved 0.4.9 baseline required'}
    Copy-Item "$root/vendor/baseline-0.4.9" "$output/baseline" -Recurse
    $base=Join-Path $output 'baseline'
    $collector=[IO.File]::ReadAllText("$base/Collector.ps1").Replace('Local\HardwarePulseCollector','Local\HardwarePulseLegacyCollectorBenchmark')
    [IO.File]::WriteAllText("$base/Collector.ps1",$collector,[Text.UTF8Encoding]::new($true))
    [IO.File]::WriteAllText("$base/Paths.ps1",'$script:stateRoot=$PSScriptRoot; $script:runtime=Join-Path $PSScriptRoot ''runtime''',[Text.UTF8Encoding]::new($true))
    [IO.File]::WriteAllText("$runtime/STOP",'Read-only runtime comparison');$restoreNeeded=$true
    $deadline=[DateTime]::Now.AddSeconds(20)
    while(Get-Process -Id $original.pid -ErrorAction SilentlyContinue){if([DateTime]::Now -gt $deadline){throw 'Installed collector did not stop; no competing benchmark started'};Start-Sleep -Milliseconds 250}
    foreach($case in @(@{name='baseline-0.4.9-worker';exe="$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe";state=$base;args=@('-NoProfile','-ExecutionPolicy','RemoteSigned','-File',"$base/Collector.ps1")},@{name='native-candidate-worker';exe="$root/build/native/app/NativeCollectorBench.exe";state="$output/native";args=@("$output/native")})){
        $null=New-Item -ItemType Directory "$($case.state)/runtime" -Force
        $info=[Diagnostics.ProcessStartInfo]::new($case.exe);$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
        # All arguments are generated local paths or fixed switches; quote for the
        # Windows PowerShell 5.1 / .NET Framework ProcessStartInfo API.
        foreach($arg in $case.args){if($arg.Contains('"') -or $arg.EndsWith('\')){throw 'Unexpected benchmark argument'}}
        $info.Arguments=(@($case.args | ForEach-Object {'"'+$_+'"'}) -join ' ')
        $clock=[Diagnostics.Stopwatch]::StartNew();$child=[Diagnostics.Process]::Start($info);$outTask=$child.StandardOutput.ReadToEndAsync();$errTask=$child.StandardError.ReadToEndAsync()
        try{
            $snapshot=Join-Path $case.state 'runtime/snapshot.json'
            while(-not(Test-Path $snapshot)){if($child.HasExited -or $clock.Elapsed.TotalSeconds -gt 60){throw "Collector failed or timed out: $($case.name)"};Start-Sleep -Milliseconds 100}
            $ready=$clock.Elapsed.TotalMilliseconds;$working=@();$private=@();$cpu0=$null;$start=$null
            while($clock.Elapsed.TotalSeconds -lt ($ready/1000+$WarmupSeconds+$MeasureSeconds)){
                $child.Refresh();if($child.HasExited){throw "Collector exited early: $($case.name)"}
                if($clock.Elapsed.TotalSeconds -ge ($ready/1000+$WarmupSeconds)){
                    if($null -eq $cpu0){$cpu0=$child.TotalProcessorTime.TotalSeconds;$start=$clock.Elapsed.TotalSeconds}
                    $working+=$child.WorkingSet64;$private+=$child.PrivateMemorySize64
                }
                Start-Sleep -Seconds 2
            }
            $child.Refresh();$duration=$clock.Elapsed.TotalSeconds-$start
            $reading=[HardwarePulse.SensorProfile]::Read($snapshot,[DateTimeOffset]::Now)
            if($reading.state -ne 'LIVE'){throw 'Collector comparison ended without fresh readings'}
            $results+=[ordered]@{name=$case.name;firstSnapshotMs=[Math]::Round($ready);warmupSeconds=$WarmupSeconds;sampleSeconds=[Math]::Round($duration,2);logicalProcessors=$logical;cpuTotalPercent=[Math]::Round(100*($child.TotalProcessorTime.TotalSeconds-$cpu0)/$duration/$logical,4);meanWorkingSetMB=[Math]::Round(($working | Measure-Object -Average).Average/1MB,2);meanPrivateMB=[Math]::Round(($private | Measure-Object -Average).Average/1MB,2);mappedKeys=@($reading.values.Keys | Sort-Object);capabilityKeys=@($reading.available.Keys | Where-Object {$reading.available[$_]} | Sort-Object)}
        }finally{
            [IO.File]::WriteAllText((Join-Path $case.state 'runtime/STOP'),'Benchmark complete')
            if(-not $child.WaitForExit(10000)){$child.Kill();$child.WaitForExit()};$child.Dispose()
        }
    }
    if(($results[0].capabilityKeys -join ',') -ne ($results[1].capabilityKeys -join ',')){throw 'Live collector capability sets differ'}
}catch{$failure=$_.Exception.ToString()}finally{
    try{if($restoreNeeded){if(Test-Path "$runtime/STOP"){Remove-Item -LiteralPath "$runtime/STOP"};$store.Run('Hardware Pulse Collector');$store.Run('Hardware Pulse Widget')}}catch{$failure+="`nBaseline restart failed: "+$_.Exception.Message}
    $store.Dispose()
    @{kind='sequential live collector worker comparison';pollSeconds=2;limitations='Single run on the development host; excludes passive legacy wrapper and UI. Instantaneous values are not expected to match across time. Raw hardware snapshots remain local.';results=$results;failure=$failure} | ConvertTo-Json -Depth 8 | Set-Content "$output/results.json" -Encoding utf8
}
if($failure){throw $failure}
Get-Content "$output/results.json" -Raw
