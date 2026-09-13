$ErrorActionPreference='Stop'
. "$PSScriptRoot\Paths.ps1"
$exe=Join-Path $PSScriptRoot 'HardwarePulse.exe'
$widget=Get-ScheduledTask | Where-Object {$_.TaskName -eq 'Hardware Pulse Widget'}
if($widget -and $widget.Actions.Execute -eq $exe -and -not $widget.Actions.Arguments){
    Stop-ScheduledTask -InputObject $widget
    Unregister-ScheduledTask -InputObject $widget -Confirm:$false
}
$task=Get-ScheduledTask | Where-Object {$_.TaskName -eq 'Hardware Pulse Collector'}
if($task -and $task.Actions.Execute -eq $exe -and $task.Actions.Arguments -eq '--collector'){
    Set-Content -LiteralPath "$runtime\STOP" -Value 'Uninstall requested'
    Start-Sleep -Seconds 3
    Stop-ScheduledTask -InputObject $task
    Unregister-ScheduledTask -InputObject $task -Confirm:$false
}
# Shared PawnIO and local user preferences are intentionally retained.
