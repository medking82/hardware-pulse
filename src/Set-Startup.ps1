param([bool]$Enabled)
$ErrorActionPreference='Stop'
$expected=Join-Path $PSScriptRoot 'HardwarePulse.exe'
$sid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$tasks=@()
foreach($name in @('Hardware Pulse Widget','Hardware Pulse Collector')){
    $task=Get-ScheduledTask -TaskName $name -ErrorAction Stop
    $owner=if($task.Principal.UserId -like 'S-1-*'){$task.Principal.UserId}else{[Security.Principal.NTAccount]::new($task.Principal.UserId).Translate([Security.Principal.SecurityIdentifier]).Value}
    $arg=if($name -eq 'Hardware Pulse Collector'){'--collector'}else{''}
    if(@($task.Actions).Count -ne 1 -or $task.Actions.Execute -ne $expected -or [string]$task.Actions.Arguments -ne $arg -or $owner -ne $sid){throw 'Startup task ownership mismatch'}
    if(@($task.Triggers).Count -ne 1 -or $task.Triggers[0].CimClass.CimClassName -ne 'MSFT_TaskLogonTrigger'){throw 'Unexpected startup trigger'}
    $tasks+=@{task=$task;enabled=[bool]$task.Settings.Enabled;logon=[bool]$task.Triggers[0].Enabled}
}
try{
    foreach($entry in $tasks){
        # Keep on-demand launches available even when login startup is off.
        $entry.task.Settings.Enabled=$true
        $entry.task.Triggers[0].Enabled=$Enabled
        $null=Set-ScheduledTask -InputObject $entry.task
    }
    foreach($entry in $tasks){
        $actual=Get-ScheduledTask -TaskName $entry.task.TaskName
        if(-not $actual.Settings.Enabled -or [bool]$actual.Triggers[0].Enabled -ne $Enabled){throw 'Startup state did not change'}
    }
}catch{
    foreach($entry in $tasks){$entry.task.Settings.Enabled=$entry.enabled;$entry.task.Triggers[0].Enabled=$entry.logon;$null=Set-ScheduledTask -InputObject $entry.task}
    throw
}
