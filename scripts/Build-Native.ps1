param([string]$AppPath)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$app=if($AppPath){[IO.Path]::GetFullPath($AppPath)}else{Join-Path $root 'build/native/app'}
$null=New-Item -ItemType Directory -Path $app -Force
if($app -ne [IO.Path]::GetFullPath("$root/build/app")){
    foreach($name in @('lib','assets','licenses','tools')){Copy-Item "$root/build/app/$name" $app -Recurse -Force}
}
Copy-Item "$root/src/HardwarePulse.exe.config","$root/src/Native/Languages.txt" $app -Force
Copy-Item "$root/src/Panel.xaml" $app -Force
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
& "$PSScriptRoot/Build-WindowsAdapters.ps1" -OutputDirectory $app
$arguments=@('/nologo','/target:winexe','/platform:x64',"/out:$app\HardwarePulse.exe",'/reference:System.Web.Extensions.dll','/reference:System.Management.dll','/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:Microsoft.CSharp.dll','/reference:System.Xaml.dll',"/reference:$framework\WPF\PresentationFramework.dll","/reference:$framework\WPF\PresentationCore.dll","/reference:$framework\WPF\WindowsBase.dll","/reference:$app\lib\LibreHardwareMonitorLib.dll","/win32icon:$app\assets\pulse.ico")
$arguments+=@(Get-ChildItem "$root/src/Native" -Filter *.cs | Select-Object -ExpandProperty FullName)
$arguments+=@("/reference:$app/Pulse.Core.dll","/reference:$app/Pulse.Adapters.Windows.dll")
$arguments+=@('CardDrag.cs','WindowSnap.cs','UpdateCheck.cs','GameOverlay.cs') | ForEach-Object {Join-Path "$root/src" $_}
& "$PSScriptRoot/Run-Hidden.ps1" "$framework/csc.exe" $arguments $root
if(@(Get-ChildItem $app -Recurse -File | Where-Object {$_.Extension -eq '.ps1' -or $_.Name -eq 'System.Management.Automation.dll'}).Count){throw 'PowerShell runtime found in native package'}
'Native runtime built without PowerShell scripts or automation assembly.'
