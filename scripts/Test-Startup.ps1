$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$expected=Join-Path $root 'src\HardwarePulse.exe'
$sid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$global:pulseStartupTestTasks=@{}
foreach($name in @('Hardware Pulse Widget','Hardware Pulse Collector')){
    $global:pulseStartupTestTasks[$name]=[pscustomobject]@{
        TaskName=$name
        Principal=[pscustomobject]@{UserId=$sid}
        Actions=@([pscustomobject]@{Execute=$expected;Arguments=$(if($name -like '*Collector'){'--collector'}else{''})})
        Settings=[pscustomobject]@{Enabled=$true}
        Triggers=@([pscustomobject]@{Enabled=$true;CimClass=[pscustomobject]@{CimClassName='MSFT_TaskLogonTrigger'}})
    }
}
$global:pulseStartupTestWrites=0
function Get-ScheduledTask { param($TaskName) $global:pulseStartupTestTasks[$TaskName] }
function Set-ScheduledTask { param($InputObject) $global:pulseStartupTestWrites++;$global:pulseStartupTestTasks[$InputObject.TaskName]=$InputObject }
function Start-ScheduledTask { param($TaskName) if(-not $global:pulseStartupTestTasks[$TaskName].Settings.Enabled){throw 'Cannot start disabled collector'} }
& "$root/src/Set-Startup.ps1" -Enabled $false
foreach($task in $global:pulseStartupTestTasks.Values){if(-not $task.Settings.Enabled -or $task.Triggers[0].Enabled){throw 'Autostart off must retain on-demand task execution'}}
Start-ScheduledTask -TaskName 'Hardware Pulse Collector'
& "$root/src/Set-Startup.ps1" -Enabled $true
foreach($task in $global:pulseStartupTestTasks.Values){if(-not $task.Settings.Enabled -or -not $task.Triggers[0].Enabled){throw 'Autostart on did not restore logon trigger'}}
$before=$global:pulseStartupTestWrites
$global:pulseStartupTestTasks['Hardware Pulse Collector'].Actions[0].Execute='C:\Unrelated\app.exe'
$rejected=$false
try{& "$root/src/Set-Startup.ps1" -Enabled $false}catch{$rejected=$true}
if(-not $rejected -or $global:pulseStartupTestWrites -ne $before){throw 'Unrelated task was not rejected before writes'}
'PASS: startup toggles login triggers, retains on-demand collector and rejects unrelated task'
