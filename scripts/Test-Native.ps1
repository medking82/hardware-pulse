param([string]$AppPath)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$app=if($AppPath){[IO.Path]::GetFullPath($AppPath)}else{Join-Path $root 'build/native/app'}
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$arguments=@('/nologo','/target:exe',"/out:$app\NativeTests.exe","/reference:$app\HardwarePulse.exe","/reference:$app\Pulse.Core.dll","/reference:$app\Pulse.Adapters.Windows.dll",'/reference:System.Web.Extensions.dll','/reference:System.Core.dll','/reference:System.Windows.Forms.dll','/reference:System.Xaml.dll',"/reference:$framework\WPF\PresentationFramework.dll","/reference:$framework\WPF\PresentationCore.dll","/reference:$framework\WPF\WindowsBase.dll","$root\scripts\NativeTests.cs","$root\scripts\NativeStartupTests.cs","$root\scripts\NativeFpsTests.cs","$root\scripts\NativeQuotaTests.cs")
& "$PSScriptRoot/Run-Hidden.ps1" "$framework/csc.exe" $arguments $root
Copy-Item "$app/HardwarePulse.exe.config" "$app/NativeTests.exe.config" -Force
& "$PSScriptRoot/Run-Hidden.ps1" "$framework/csc.exe" @('/nologo','/target:exe',"/out:$app\NativeCollectorBench.exe","/reference:$app\HardwarePulse.exe","/reference:$app\Pulse.Core.dll","/reference:$app\Pulse.Adapters.Windows.dll","$root\scripts\NativeCollectorBench.cs") $root
Copy-Item "$app/HardwarePulse.exe.config" "$app/NativeCollectorBench.exe.config" -Force
$testRoot=Join-Path $root ('vendor/native-ui-'+[Guid]::NewGuid().ToString('N'))
& "$PSScriptRoot/Run-Hidden.ps1" "$app/NativeTests.exe" @($testRoot) $root
& "$PSScriptRoot/Run-Hidden.ps1" "$framework/csc.exe" @('/nologo','/target:exe',"/out:$app\NativeCodexRpcTests.exe","/reference:$app\Pulse.Core.dll","/reference:$app\Pulse.Adapters.Windows.dll","$root\scripts\CodexRpcTests.cs") $root
Copy-Item "$app/HardwarePulse.exe.config" "$app/NativeCodexRpcTests.exe.config" -Force
foreach($fixture in @(@('NativeCodexRpcFixture','RPC_NORMAL'),@('NativeCodexRpcFixture-hang','RPC_HANG'),@('NativeCodexRpcFixture-stderr','RPC_STDERR'))){
    & "$PSScriptRoot/Run-Hidden.ps1" "$framework/csc.exe" @('/nologo','/target:exe',"/define:$($fixture[1])","/out:$app\$($fixture[0]).exe","$root\scripts\CodexRpcFixture.cs") $root
    Copy-Item "$app/HardwarePulse.exe.config" "$app/$($fixture[0]).exe.config" -Force
}
& "$PSScriptRoot/Run-Hidden.ps1" "$app/NativeCodexRpcTests.exe" @() $root

& "$PSScriptRoot/Run-Hidden.ps1" "$framework/csc.exe" @('/nologo','/target:exe',"/out:$app\NativeQuotaPipeTests.exe","$root\scripts\QuotaPipeTests.cs") $root
& "$PSScriptRoot/Run-Hidden.ps1" "$app/NativeQuotaPipeTests.exe" @("$app/Pulse.Adapters.Windows.dll") $root

& "$PSScriptRoot/Run-Hidden.ps1" "$framework/csc.exe" @('/nologo','/target:winexe',"/out:$app\NativeClaudeStatusLineTests.exe","/reference:$app\Pulse.Core.dll",'/reference:System.Web.Extensions.dll',"$root\src\Native\ClaudeStatusLineReceiver.cs","$root\src\Native\ClaudeStatusLineCommand.cs","$root\scripts\ClaudeStatusLineTests.cs") $root
Copy-Item "$app/HardwarePulse.exe.config" "$app/NativeClaudeStatusLineTests.exe.config" -Force
& "$PSScriptRoot/Run-Hidden.ps1" "$app/NativeClaudeStatusLineTests.exe" @((Join-Path $root ('vendor/claude-statusline-'+[Guid]::NewGuid().ToString('N')))) $root
