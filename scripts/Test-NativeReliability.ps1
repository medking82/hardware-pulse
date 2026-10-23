param([string]$AppPath,[string]$OutputPath,[ValidateSet('all','persistence','ui')][string]$Mode='all')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$app=if($AppPath){[IO.Path]::GetFullPath($AppPath)}else{Join-Path $root 'build/native/app'}
if(-not $OutputPath){$OutputPath=Join-Path $root ('build/audit-'+[Guid]::NewGuid().ToString('N'))}
$OutputPath=[IO.Path]::GetFullPath($OutputPath)
$null=New-Item -ItemType Directory -Path $OutputPath -Force
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$arguments=@('/nologo','/target:exe',"/out:$app/NativeReliabilityTests.exe","/reference:$app/HardwarePulse.exe","/reference:$app/Pulse.Core.dll","/reference:$app/Pulse.Adapters.Windows.dll",'/reference:System.Core.dll','/reference:System.Xaml.dll',"/reference:$framework/WPF/PresentationFramework.dll","/reference:$framework/WPF/PresentationCore.dll","/reference:$framework/WPF/WindowsBase.dll",(Join-Path $root 'scripts/NativeReliabilityTests.cs'))
& "$PSScriptRoot/Run-Hidden.ps1" "$framework/csc.exe" $arguments $root
Copy-Item -LiteralPath "$app/HardwarePulse.exe.config" -Destination "$app/NativeReliabilityTests.exe.config" -Force
$linked=Join-Path $OutputPath 'directory-link'
if($Mode -ne 'ui'){
    $target=Join-Path $OutputPath 'directory-target'
    $null=New-Item -ItemType Directory -Path $target
    $null=New-Item -ItemType Junction -Path $linked -Target $target
}
try{& "$PSScriptRoot/Run-Hidden.ps1" "$app/NativeReliabilityTests.exe" @($app,$OutputPath,$Mode) $root}
finally{if($Mode -ne 'ui'){Remove-Item -LiteralPath $linked -Force}}
