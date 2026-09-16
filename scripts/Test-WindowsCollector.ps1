param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
& "$PSScriptRoot/Build-WindowsCollector.ps1"
$worker=Join-Path $root 'build/windows-worker/worker'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$arguments=@('/nologo','/target:exe','/platform:x64',"/out:$worker/WindowsCollectorTests.exe",'/reference:System.Web.Extensions.dll',"/reference:$worker/HardwarePulse.Collector.exe","/reference:$worker/Pulse.Core.dll","/reference:$worker/Pulse.Adapters.Windows.dll",[IO.Path]::GetFullPath((Join-Path $root 'scripts/WindowsCollectorTests.cs')),[IO.Path]::GetFullPath((Join-Path $root 'scripts/SharedStartupTests.cs')))
& "$PSScriptRoot/Run-Hidden.ps1" $compiler $arguments $root
Copy-Item "$worker/HardwarePulse.Collector.exe.config" "$worker/WindowsCollectorTests.exe.config"
$state=Join-Path $root ('vendor/worker-test-'+[Guid]::NewGuid().ToString('N'))
try { & "$PSScriptRoot/Run-Hidden.ps1" "$worker/WindowsCollectorTests.exe" @($state) $worker }
finally {
    # Remove only generated test binaries; leave local evidence under vendor.
    Remove-Item -LiteralPath "$worker/WindowsCollectorTests.exe","$worker/WindowsCollectorTests.exe.config" -Force
}
