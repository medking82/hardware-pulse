$PSDefaultParameterValues['Get-Content:Encoding']='UTF8'
$PSDefaultParameterValues['Set-Content:Encoding']='UTF8'
$script:stateRoot=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'HardwarePulse'
$sid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$script:runtime=Join-Path ([Environment]::GetFolderPath('CommonApplicationData')) "HardwarePulse\$sid\runtime"
$null=New-Item -ItemType Directory -Path $script:stateRoot -Force
