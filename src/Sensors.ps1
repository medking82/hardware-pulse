# Explicit identities prevent CPU VID, DIMM limit metadata and iGPU mix-ups.
. "$PSScriptRoot\DeviceProfile.ps1"
$script:SensorMap=@{
 cpu=@('/amdcpu/0/temperature/2','Temperature',1,110)
 cpuLoad=@('/amdcpu/0/load/0','Load',0,100)
 vcore=@('/lpc/nct6687dr/0/voltage/4','Voltage',0.1,2)
 cpuFan=@('/lpc/nct6687dr/0/fan/0','Fan',0,10000)
 gpu=@('/gpu-nvidia/0/temperature/0','Temperature',1,110)
 gpuLoad=@('/gpu-nvidia/0/load/0','Load',0,100)
 vram=@('/gpu-nvidia/0/temperature/3','Temperature',1,120)
 gpuVolt=@('/gpu-nvidia/0/voltage/0','Voltage',0.1,2)
 gpuFan=@('/gpu-nvidia/0/fan/1','Fan',0,10000)
 gpuFan2=@('/gpu-nvidia/0/fan/2','Fan',0,10000)
 ramA=@('/memory/dimm/1/temperature/0','Temperature',1,100)
 ramB=@('/memory/dimm/3/temperature/0','Temperature',1,100)
 system=@('/lpc/nct6687dr/0/temperature/1','Temperature',1,100)
 bottom=@('/lpc/nct6687dr/0/fan/10','Fan',0,10000)
 top=@('/lpc/nct6687dr/0/fan/12','Fan',0,10000)
 diskC=@('/nvme/0/temperature/0','Temperature',1,100)
 diskD=@('/nvme/1/temperature/0','Temperature',1,100)
}
function Get-PulseSnapshot([string]$Path,[DateTimeOffset]$Now=[DateTimeOffset]::Now) {
    try {
        # Allow the collector to atomically replace the file while this handle reads.
        $stream=[IO.File]::Open($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
        $reader=[IO.StreamReader]::new($stream)
        try {$json=$reader.ReadToEnd()} finally {$reader.Dispose()}
        $raw=$json | ConvertFrom-Json -ErrorAction Stop
        if($raw.schema -notin @(1,2)){throw 'Unsupported snapshot schema'}
        $stamp=[DateTimeOffset]::Parse($raw.time)
        $age=($Now-$stamp).TotalSeconds
        if($age -lt -2 -or $age -gt 15){return @{state='STALE';values=@{};time=$stamp}}
        $values=@{};$names=@{}
        if($raw.schema -eq 2){$profile=Get-DeviceProfile $raw;$names=$profile.names}
        foreach($key in $script:SensorMap.Keys) {
            $spec=$script:SensorMap[$key]
            $found=@(if($raw.schema -eq 2){$profile.sensors[$key] | Where-Object {$null -ne $_ -and $_.type -eq $spec[1]}}else{$raw.sensors | Where-Object {$_.id -eq $spec[0] -and $_.type -eq $spec[1]}})
            if($found.Count -ne 1 -or $null -eq $found[0].value){continue}
            if($raw.schema -eq 1 -and $key -eq 'diskC' -and $found[0].hardware -ne 'KIOXIA-EXCERIA PLUS G4 SSD'){continue}
            if($raw.schema -eq 1 -and $key -eq 'diskD' -and $found[0].hardware -ne 'KIOXIA-EXCERIA BASIC SSD'){continue}
            $v=[double]$found[0].value
            if([double]::IsNaN($v) -or [double]::IsInfinity($v) -or $v -lt $spec[2] -or $v -gt $spec[3]){continue}
            $values[$key]=$v
        }
        $available=$null
        if($raw.schema -eq 2){$available=@{};foreach($key in $script:SensorMap.Keys){$available[$key]=[bool]$profile.sensors[$key]}}
        return @{state='LIVE';values=$values;available=$available;names=$names;gpuFanCount=if($raw.schema -eq 2){$profile.gpuFanCount}else{1};usage=if($raw.schema -eq 2){$profile.usage}else{@{}};time=$stamp;identity="$($raw.pid):$($raw.sequence)"}
    } catch {return @{state='OFFLINE';values=@{};error=$_.Exception.Message}}
}
