# Run with Windows PowerShell 5.1 after compiling CollectorRecoveryFixture.cs
# as a windowless EXE. Registers and removes one isolated limited-user task.
param([Parameter(Mandatory=$true)][string]$FixturePath,[string]$AppPath,[string]$ReportPath,[switch]$TaskSchedulerOnly)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
if(-not $AppPath){$AppPath=Join-Path $root 'build/app'}
$state=Join-Path $root ('vendor/collector-recovery-'+[Guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path $state
if(-not $ReportPath){$ReportPath=Join-Path $state 'report.json'}
$report=@{phase='Preparing';passed=$false;productionTasksModified=$false;state=$state}
function Save-Report { $report|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $ReportPath -Encoding UTF8 }
Save-Report
$null=[Reflection.Assembly]::LoadFrom((Join-Path $AppPath 'HardwarePulse.exe'))
$store=$null;$task=$null;$registered=$false;$failure=$null
$service=New-Object -ComObject Schedule.Service
$service.Connect();$folder=$service.GetFolder('\')
$name='HardwarePulseRecovery-'+[Guid]::NewGuid().ToString('N')
try {
    $store=[HardwarePulse.SchedulerStore]::new()
    $exe=Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'Hardware Pulse/HardwarePulse.exe'
    $startup=[HardwarePulse.Startup]::new($store,$exe,[Security.Principal.WindowsIdentity]::GetCurrent().User.Value)
    $method=$startup.GetType().GetMethod('NewXml',[Reflection.BindingFlags]'Instance,NonPublic')
    $xml=[xml]$method.Invoke($startup,@([HardwarePulse.Startup]::CollectorTask,$true))
    $ns=New-Object Xml.XmlNamespaceManager($xml.NameTable);$ns.AddNamespace('t',$xml.DocumentElement.NamespaceURI)
    $settings=$xml.SelectSingleNode('/t:Task/t:Settings',$ns)
    if($settings.SelectSingleNode('t:RestartOnFailure/t:Interval',$ns).InnerText -ne 'PT1M' -or $settings.SelectSingleNode('t:RestartOnFailure/t:Count',$ns).InnerText -ne '3'){throw 'Production collector XML lacks the bounded restart policy'}
    # Only the fixture command and test privileges differ. The actual
    # production restart XML is exercised by Task Scheduler.
    $xml.SelectSingleNode('/t:Task/t:Principals/t:Principal/t:RunLevel',$ns).InnerText='LeastPrivilege'
    $settings.SelectSingleNode('t:ExecutionTimeLimit',$ns).InnerText='PT3M'
    $action=$xml.SelectSingleNode('/t:Task/t:Actions/t:Exec',$ns)
    $action.SelectSingleNode('t:Command',$ns).InnerText=[IO.Path]::GetFullPath($FixturePath)
    $action.SelectSingleNode('t:Arguments',$ns).InnerText='"'+$state+'"'
    $action.SelectSingleNode('t:WorkingDirectory',$ns).InnerText=$state
    $task=$folder.RegisterTask($name,$xml.OuterXml,6,$null,$null,3,$null);$registered=$true
    $report.phase='WaitingForFailure';$report.task=$name;Save-Report
    $null=$task.Run($null)
    $deadline=[DateTime]::UtcNow.AddSeconds(15)
    do {
        Start-Sleep -Milliseconds 250
        if($task.State -eq 3 -and $task.LastTaskResult -in @(-1073741819,3221225477)){break}
    } while([DateTime]::UtcNow -lt $deadline)
    if($task.LastTaskResult -notin @(-1073741819,3221225477)){throw 'Fixture did not exit with the injected native crash code'}
    $failed=[DateTime]::UtcNow
    $recovery=if($TaskSchedulerOnly){$null}else{[HardwarePulse.CollectorRecovery]::new()}
    $report.phase='WaitingForAutomaticRestart';$report.firstExitCode='0xC0000005';$report.applicationRestarts=0;Save-Report
    $deadline=$failed.AddSeconds(95)
    do {
        Start-Sleep -Milliseconds 500
        $runs=if(Test-Path -LiteralPath "$state/runs.txt"){[int][IO.File]::ReadAllText("$state/runs.txt")}else{0}
        if($runs -eq 2 -and (Test-Path -LiteralPath "$state/heartbeat.txt")){break}
        if($recovery -and $recovery.Observe([DateTimeOffset]::UtcNow,$false,(Test-Path -LiteralPath "$state/STOP"))){
            $null=$task.Run($null);$report.applicationRestarts++;Save-Report
        }
    } while([DateTime]::UtcNow -lt $deadline)
    if($runs -ne 2 -or -not(Test-Path -LiteralPath "$state/heartbeat.txt")){throw 'The failed collector fixture did not recover automatically'}
    $report.restartDelaySeconds=[Math]::Round(([DateTime]::UtcNow-$failed).TotalSeconds,1)
    if($report.restartDelaySeconds -lt 50){throw 'Restart did not honor the one-minute backoff'}
    [IO.File]::WriteAllText("$state/STOP",'Normal fixture shutdown')
    $deadline=[DateTime]::UtcNow.AddSeconds(10)
    while($task.State -ne 3 -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 250}
    if($task.State -ne 3 -or $task.LastTaskResult -ne 0){throw 'Normal STOP did not finish successfully'}
    $report.phase='CheckingSuccessfulStopDoesNotRestart';$report.stopExitCode=0;Save-Report
    $deadline=[DateTime]::UtcNow.AddSeconds(65)
    while([DateTime]::UtcNow -lt $deadline){
        Start-Sleep -Milliseconds 500
        if($recovery -and $recovery.Observe([DateTimeOffset]::UtcNow,$false,$true)){throw 'Application recovery ignored the normal STOP signal'}
        if([int][IO.File]::ReadAllText("$state/runs.txt") -ne 2){throw 'Successful STOP was automatically restarted'}
    }
    $report.passed=$true;$report.phase='Passed';$report.runs=2;Save-Report
}catch{$failure=$_;$report.phase='Failed';$report.error=$_.Exception.Message;Save-Report}
finally{
    if($registered){try{$task.Stop(0)}catch{};$folder.DeleteTask($name,0)}
    if($store){$store.Dispose()}
    $report.taskRemoved=(-not $registered -or $null -eq (Get-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue));Save-Report
}
if($failure){throw $failure}
Get-Content -LiteralPath $ReportPath -Raw
