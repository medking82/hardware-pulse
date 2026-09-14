$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$fixture=Join-Path $root ('vendor/host-policy-'+[Guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path $fixture
try {
# Compile the actual entry point; isolate log paths, not policy or invocation behavior.
$source=[IO.File]::ReadAllText("$root/src/WidgetHost.cs")
$source=$source.Replace('Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)', 'AppDomain.CurrentDomain.BaseDirectory')
$source=$source.Replace('Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)', 'AppDomain.CurrentDomain.BaseDirectory')
[IO.File]::WriteAllText("$fixture/WidgetHost.cs",$source)
$automation="$env:WINDIR/Microsoft.NET/assembly/GAC_MSIL/System.Management.Automation/v4.0_3.0.0.0__31bf3856ad364e35/System.Management.Automation.dll"
& "$PSScriptRoot/Run-Hidden.ps1" "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" @('/nologo','/target:winexe','/platform:x64',"/out:$fixture/HardwarePulse.exe",'/reference:System.Windows.Forms.dll',"/reference:$automation",(Join-Path $fixture 'WidgetHost.cs')) $root
Copy-Item "$root/src/HardwarePulse.exe.config" $fixture
$stub=@'
param([bool]$Enabled)
Get-ExecutionPolicy | Set-Content (Join-Path $PSScriptRoot 'policy.txt')
'@
foreach($name in @('Set-Startup.ps1','Collector.ps1')){[IO.File]::WriteAllText("$fixture/$name",$stub,[Text.UTF8Encoding]::new($true))}
function Invoke-Fixture($argument){
    $info=[Diagnostics.ProcessStartInfo]::new("$fixture/HardwarePulse.exe",$argument)
    $info.UseShellExecute=$false;$info.CreateNoWindow=$true
    $info.EnvironmentVariables['PSExecutionPolicyPreference']='Restricted'
    $info.EnvironmentVariables['PSModulePath'] = "$env:WINDIR\System32\WindowsPowerShell\v1.0\Modules"
    $process=[Diagnostics.Process]::Start($info)
    if(-not $process.WaitForExit(20000)){$process.Kill();throw 'Policy fixture timed out'}
    $code=$process.ExitCode;$process.Dispose();return $code
}
foreach($argument in @('--enable-startup','--collector')){
    if(Test-Path "$fixture/policy.txt"){Remove-Item "$fixture/policy.txt"}
    if((Invoke-Fixture $argument) -ne 0){throw "Local script rejected under inherited Restricted: $argument"}
    if((Get-Content "$fixture/policy.txt" -Raw).Trim() -ne 'RemoteSigned'){throw 'Unexpected effective process policy'}
}
# The app must not silently unblock downloaded, unsigned scripts.
Set-Content -LiteralPath "$fixture/Set-Startup.ps1" -Stream Zone.Identifier -Value "[ZoneTransfer]`r`nZoneId=3"
Remove-Item "$fixture/policy.txt"
if((Invoke-Fixture '--enable-startup') -eq 0 -or (Test-Path "$fixture/policy.txt")){throw 'Unsigned Internet script was allowed'}
'PASS: host and collector accept installed local scripts under Restricted; Internet unsigned script remains blocked'

} finally {
    $expectedParent=[IO.Path]::GetFullPath((Join-Path $root 'vendor'))
    if([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($fixture)) -ne $expectedParent){throw 'Unexpected policy fixture cleanup path'}
    if((Get-Item -LiteralPath $fixture).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Policy fixture is a reparse point'}
    Remove-Item -LiteralPath $fixture -Recurse -Force
}