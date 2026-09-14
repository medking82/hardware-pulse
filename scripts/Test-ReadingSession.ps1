$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$output=Join-Path $root ('vendor/reading-session-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
# Compile the domain boundary without referencing WPF, WinForms or the app EXE.
$arguments=@('/nologo','/target:exe',"/out:$output\ReadingSessionTests.exe",'/reference:System.Web.Extensions.dll',"$root\src\Native\Models.cs","$root\src\Native\SensorProfile.cs","$root\src\Native\ReadingSession.cs","$root\scripts\ReadingSessionTests.cs")
& "$PSScriptRoot/Run-Hidden.ps1" $compiler $arguments $root
& "$PSScriptRoot/Run-Hidden.ps1" "$output/ReadingSessionTests.exe" @($output) $root
