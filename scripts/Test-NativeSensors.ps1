$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$null=New-Item -ItemType Directory "$root/build/native" -Force
& "$PSScriptRoot/Build-Core.ps1" -OutputPath "$root/build/native/Pulse.Core.dll"
& "$PSScriptRoot/Run-Hidden.ps1" "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" @('/nologo','/target:library',"/out:$root\build\native\Pulse.SensorFixture.dll",'/reference:System.Web.Extensions.dll',"/reference:$root\build\native\Pulse.Core.dll","$root\src\Native\Models.cs","$root\src\Native\SensorProfile.cs") $root
$testRoot=Join-Path $root ('vendor/native-sensors-'+[Guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path $testRoot
Copy-Item "$root/src/Sensors.ps1","$root/src/DeviceProfile.ps1" $testRoot
Copy-Item "$root/build/native/Pulse.SensorFixture.dll","$root/build/native/Pulse.Core.dll" $testRoot
$harness=@'
. "$PSScriptRoot/Sensors.ps1"
Add-Type -AssemblyName System.Web.Extensions
Add-Type -Path "$PSScriptRoot/Pulse.SensorFixture.dll"
$script:legacySnapshot=${function:Get-PulseSnapshot}
function Assert-MapEqual($a,$b,$context){
    if(@($a.Keys).Count -ne @($b.Keys).Count){throw "$context key count differs"}
    foreach($key in $a.Keys){
        if(-not $b.ContainsKey($key)){throw "$context missing $key"}
        if($a[$key] -is [double]){if([Math]::Abs($a[$key]-$b[$key]) -gt 0.000001){throw "$context $key differs"}}
        elseif($a[$key] -ne $b[$key]){throw "$context $key differs: $($a[$key]) / $($b[$key])"}
    }
}
function Get-PulseSnapshot([string]$Path,[DateTimeOffset]$Now=[DateTimeOffset]::Now){
    $old=& $script:legacySnapshot $Path $Now
    $native=[HardwarePulse.SensorProfile]::Read($Path,$Now)
    if($old.state -ne $native.state){throw "State differs: $($old.state) / $($native.state): $($native.error)"}
    Assert-MapEqual $old.values $native.values 'values'
    if($old.state -eq 'LIVE'){
        Assert-MapEqual $old.names $native.names 'names'
        if($null -ne $old.available){Assert-MapEqual $old.available $native.available 'available'}
        if($old.gpuFanCount -ne $native.gpuFanCount){throw 'Fan count differs'}
        if(@($old.usage.Keys).Count -ne @($native.usage.Keys).Count){throw 'Usage key count differs'}
        foreach($key in $old.usage.Keys){foreach($property in @('used','total','percent')){if([Math]::Abs($old.usage[$key][$property]-$native.usage[$key].$property) -gt 0.000001){throw "Usage $key/$property differs"}}}
    }
    $script:comparisonCount++
    return $old
}
'@
$test=[IO.File]::ReadAllText("$root/src/Test-Sensors.ps1").Replace('. "$PSScriptRoot\Sensors.ps1"',$harness)
$test+="`r`n'PASS: native/PowerShell differential snapshot comparisons: '+`$script:comparisonCount`r`n"
[IO.File]::WriteAllText("$testRoot/Test.ps1",$test,[Text.UTF8Encoding]::new($true))
& "$PSScriptRoot/Run-Hidden.ps1" "$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe" @('-NoProfile','-File',"$testRoot/Test.ps1") $testRoot
