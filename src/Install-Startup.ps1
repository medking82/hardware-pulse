$ErrorActionPreference='Stop'
. "$PSScriptRoot\Paths.ps1"
$exe=Join-Path $PSScriptRoot 'HardwarePulse.exe'
$programFiles=[Environment]::GetFolderPath('ProgramFiles').TrimEnd('\')+'\'
if(-not $exe.StartsWith($programFiles,[StringComparison]::OrdinalIgnoreCase)){throw 'Startup registration requires a Program Files installation.'}
$user=[Security.Principal.WindowsIdentity]::GetCurrent().Name
$taskName='Hardware Pulse Collector'
$widgetTaskName='Hardware Pulse Widget'
$widgetOld=Get-ScheduledTask | Where-Object {$_.TaskName -eq $widgetTaskName}
if($widgetOld -and ($widgetOld.Actions.Execute -ne $exe -or $widgetOld.Actions.Arguments)){throw 'A different task already owns the widget task name.'}
$old=Get-ScheduledTask | Where-Object {$_.TaskName -eq $taskName}
if($old -and ($old.Actions.Execute -ne $exe -or $old.Actions.Arguments -ne '--collector')){throw 'A different task already owns this name.'}
$legacy=Get-ScheduledTask | Where-Object {$_.TaskName -eq 'Hardware Pulse Sensor Collector'}
$legacySid=if($legacy){
    if($legacy.Principal.UserId -like 'S-1-*'){$legacy.Principal.UserId}
    else{[Security.Principal.NTAccount]::new($legacy.Principal.UserId).Translate([Security.Principal.SecurityIdentifier]).Value}
}
$legacyOwned=$legacy -and $legacySid -eq [Security.Principal.WindowsIdentity]::GetCurrent().User.Value -and
    $legacy.Description -eq 'Local read-only LibreHardwareMonitor sensor collector for Hardware Pulse.' -and
    $legacy.Actions.Execute -like '*\pwsh.exe' -and $legacy.Actions.Arguments -like '*hardware-panel\Run-Collector.ps1*'
$action=New-ScheduledTaskAction -Execute $exe -Argument '--collector' -WorkingDirectory $PSScriptRoot
$principal=New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Highest
$trigger=New-ScheduledTaskTrigger -AtLogOn -User $user
$settings=New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew
$null=Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Trigger $trigger -Settings $settings -Description 'Hardware Pulse read-only local sensor collector.' -Force
if($legacyOwned){
    Export-ScheduledTask -TaskName $legacy.TaskName | Set-Content "$stateRoot\legacy-task-backup.xml"
    # The old wrapper can leave its child alive after Task Scheduler stops it.
    # Ask that known collector to exit its read loop before stopping the wrapper.
    if($legacy.Actions.Arguments -match '-File "([A-Za-z]:\\[^"\r\n]+\\hardware-panel\\Run-Collector.ps1)"'){
        $legacyRuntime=Join-Path (Split-Path $Matches[1]) 'runtime'
        if(Test-Path "$legacyRuntime\snapshot.json"){
            Set-Content -LiteralPath "$legacyRuntime\STOP" -Value 'Replaced by installed Hardware Pulse'
            Start-Sleep -Seconds 3
        }
    }
    Stop-ScheduledTask -InputObject $legacy
    $null=Disable-ScheduledTask -InputObject $legacy
}
if(Test-Path "$runtime\STOP"){Remove-Item -LiteralPath "$runtime\STOP"}
Start-ScheduledTask -TaskName $taskName
$fresh=$false
for($attempt=0;$attempt -lt 20;$attempt++){
    Start-Sleep -Milliseconds 500
    if(Test-Path "$runtime\snapshot.json"){
        try{
            $sample=Get-Content "$runtime\snapshot.json" -Raw | ConvertFrom-Json
            if(([DateTimeOffset]::Now-[DateTimeOffset]::Parse($sample.time)).TotalSeconds -lt 5){$fresh=$true;break}
        }catch{}
    }
}
if(-not $fresh){throw 'Collector did not produce a fresh snapshot. See runtime/collector-error.txt.'}
$widgetAction=New-ScheduledTaskAction -Execute $exe -WorkingDirectory $PSScriptRoot
$widgetPrincipal=New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
$widgetTrigger=New-ScheduledTaskTrigger -AtLogOn -User $user
$widgetTrigger.Delay='PT10S'
$null=Register-ScheduledTask -TaskName $widgetTaskName -Action $widgetAction -Principal $widgetPrincipal -Trigger $widgetTrigger -Settings $settings -Description 'Hardware Pulse desktop widget for the current interactive user.' -Force
# Replace only our old login shortcut; the widget now has an independently observable task.
$shortcut=Join-Path ([Environment]::GetFolderPath('Startup')) 'Hardware Pulse.lnk'
if(Test-Path -LiteralPath $shortcut){
    $link=(New-Object -ComObject WScript.Shell).CreateShortcut($shortcut)
    if($link.TargetPath -eq $exe -and -not $link.Arguments){Remove-Item -LiteralPath $shortcut}
}
@{installed=[DateTimeOffset]::Now.ToString('o');task=$taskName} | ConvertTo-Json | Set-Content "$stateRoot\startup.json"
