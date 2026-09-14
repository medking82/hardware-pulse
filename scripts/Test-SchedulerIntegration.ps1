#Requires -RunAsAdministrator
# Run with Windows PowerShell 5.1. Creates only one GUID-named test folder;
# never changes or runs the installed application's tasks.
param([string]$ReportPath)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
if(-not $ReportPath){$ReportPath=Join-Path $root ('vendor/scheduler-integration-'+[Guid]::NewGuid().ToString('N')+'.json')}
$null=[Reflection.Assembly]::LoadFrom("$root/build/native/app/HardwarePulse.exe")
$service=New-Object -ComObject Schedule.Service
$service.Connect()
$taskRoot=$service.GetFolder('\')
$leaf='HardwarePulseRegression-'+[Guid]::NewGuid().ToString('N')
$store=$null;$startup=$null;$created=$false;$redirected=$false;$failure=$null
try{
    $isolated=$taskRoot.CreateFolder($leaf,$null);$created=$true
    $store=[HardwarePulse.SchedulerStore]::new()
    # Redirect only this test object's COM folder; runtime defaults remain unchanged.
    $field=[HardwarePulse.SchedulerStore].GetField('folder',[Reflection.BindingFlags]'Instance,NonPublic')
    $original=$field.GetValue($store);$field.SetValue($store,$isolated);$redirected=$true
    $null=[Runtime.InteropServices.Marshal]::FinalReleaseComObject($original)
    $exe=Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'Hardware Pulse/HardwarePulse.exe'
    $startup=[HardwarePulse.Startup]::new($store,$exe,[Security.Principal.WindowsIdentity]::GetCurrent().User.Value)
    foreach($name in @('Hardware Pulse Widget','Hardware Pulse Collector')){if($null -ne $store.Get($name)){throw 'Test folder was not empty'}}
    $startup.Install()
    if(-not $startup.IsEnabled()){throw 'First-time registration failed'}
    $store.Delete('Hardware Pulse Widget')
    $startup.Install()
    if(-not $startup.IsEnabled()){throw 'Partial-install repair failed'}
    $startup.SetEnabled($false);$startup.Install()
    if($startup.IsEnabled()){throw 'Reinstall lost disabled startup preference'}
    $startup.Remove()
    foreach($name in @('Hardware Pulse Widget','Hardware Pulse Collector')){if($null -ne $store.Get($name)){throw 'Test tasks remained after removal'}}
}catch{$failure=$_.Exception.ToString()}finally{
    if($redirected -and $startup){try{$startup.Remove()}catch{$failure+=' Cleanup: '+$_.Exception.Message}}
    if($store){$store.Dispose()}
    if($created){try{$taskRoot.DeleteFolder($leaf,0)}catch{$failure+=' Folder cleanup: '+$_.Exception.Message}}
    @{passed=($null -eq $failure);failure=$failure;folder=$leaf;checks=@('real missing-task COM translation','first registration','partial-install repair','disabled preference','owned cleanup');productionTasksModified=$false} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ReportPath -Encoding utf8
}
if($failure){throw $failure}
Get-Content -LiteralPath $ReportPath -Raw
