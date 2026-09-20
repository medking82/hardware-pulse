$ErrorActionPreference='Stop'
. "$PSScriptRoot\Sensors.ps1"
$path=Join-Path $PSScriptRoot 'test-snapshot.json'
try {
    $now=[DateTimeOffset]::Now
    $raw=@{schema=1;time=$now.ToString('o');pid=1;sequence=1;sensors=@(
        @{id='/amdcpu/0/temperature/2';type='Temperature';value=64},
        @{id='/memory/dimm/1/temperature/1';type='Temperature';value=0.25},
        @{id='/memory/dimm/1/temperature/0';type='Temperature';value=43},
        @{id='/gpu-nvidia/0/fan/1';type='Fan';value=0},
        @{id='/gpu-nvidia/0/temperature/3';type='Temperature';value=255},
        @{id='/nvme/0/temperature/0';type='Temperature';value=41;hardware='Wrong disk'})}
    $raw|ConvertTo-Json -Depth 5|Set-Content $path
    $a=Get-PulseSnapshot $path $now
    if($a.values.cpu -ne 64 -or $a.values.ramA -ne 43 -or $a.values.gpuFan -ne 0 -or $a.values.ContainsKey('vram')){throw ('Mapping/invalid-value regression: '+($a|ConvertTo-Json -Depth 5))}
    if($a.values.ContainsKey('diskC')){throw 'Wrong NVMe identity accepted'}
    function Sensor($id,$device,$hwType,$name,$type,$value){@{id=$id;hardwareId=$device;hardware=$device;hardwareType=$hwType;name=$name;type=$type;value=$value}}
    $modern=@{schema=2;time=$now.ToString('o');pid=2;sequence=1;memoryName='64 GB DDR5-6000 configured';memoryModules=@(@{brand='Kingston';part='KF560C30-32';slot='DIMMA2'});ramUsage=@{usedGb=16;totalGb=64};sensors=@(
        (Sensor '/intelcpu/0/temperature/0' '/intelcpu/0' 'Cpu' 'CPU Package' 'Temperature' 61),
        (Sensor '/gpu-amd/0/temperature/0' '/gpu-amd/0' 'GpuAmd' 'GPU Core' 'Temperature' 44),
        (Sensor '/gpu-amd/0/smalldata/0' '/gpu-amd/0' 'GpuAmd' 'GPU Memory Used' 'SmallData' 4096),
        (Sensor '/gpu-amd/0/smalldata/1' '/gpu-amd/0' 'GpuAmd' 'GPU Memory Total' 'SmallData' 16384),
        (Sensor '/gpu-amd/0/fan/1' '/gpu-amd/0' 'GpuAmd' 'GPU Fan 1' 'Fan' 1050),
        (Sensor '/gpu-amd/0/fan/2' '/gpu-amd/0' 'GpuAmd' 'GPU Fan 2' 'Fan' 0),
        (Sensor '/memory/dimm/3/temperature/0' '/memory/dimm/3' 'Memory' 'DIMM #3' 'Temperature' 42),
        (Sensor '/memory/dimm/3/temperature/1' '/memory/dimm/3' 'Memory' 'Temperature Sensor Resolution' 'Temperature' 0.25),
        (Sensor '/nvme/8/temperature/0' '/nvme/8' 'Storage' 'Temperature' 'Temperature' 39)
    )}
    $modern|ConvertTo-Json -Depth 6|Set-Content $path
    $m=Get-PulseSnapshot $path $now
    if($m.values.cpu -ne 61 -or $m.values.gpu -ne 44 -or $m.values.ramA -ne 42 -or $m.values.diskC -ne 39){throw ('Device discovery failed: '+($m|ConvertTo-Json -Depth 6))}
    if($m.names.ramA -ne 'SPD #3' -or $m.names.Memory -notmatch 'Kingston.*Slots A2'){throw 'DIMM identity/slot metadata failed'}
    if(-not $m.names.Memory.Contains([char]0x00B7)){throw 'UTF-8 separator decoding failed'}
    if($m.usage.vram.used -ne 4 -or $m.usage.vram.total -ne 16 -or $m.usage.ram.percent -ne 25){throw 'Usage unit conversion failed'}
    if($m.gpuFanCount -ne 2 -or $m.values.gpuFan2 -ne 0){throw 'Two GPU telemetry channels / zero RPM failed'}
    $duplicate=$modern.Clone();$duplicate.sensors=@($modern.sensors+(Sensor '/gpu-amd/0/fan/3' '/gpu-amd/0' 'GpuAmd' 'GPU Fan 1' 'Fan' 1100))
    $duplicate|ConvertTo-Json -Depth 6|Set-Content $path
    $d=Get-PulseSnapshot $path $now
    if($d.available.gpuFan -or $d.values.ContainsKey('gpuFan')){throw 'Duplicate candidate became a selected sensor'}
    $topology=$modern.Clone();$topology.sensors=@($modern.sensors|ForEach-Object{$copy=$_.Clone();if($copy.hardwareId -eq '/gpu-amd/0'){$copy.hardwareId='/gpu-amd/1';if($copy.type -eq 'Temperature'){$copy.value=46}};$copy})
    $topology|ConvertTo-Json -Depth 6|Set-Content $path
    $t=Get-PulseSnapshot $path $now
    if($t.values.gpu -ne 46 -or $t.values.gpuLoad -ne $null){throw 'Topology change did not preserve fresh per-snapshot mapping'}
    if($null -ne (Convert-ProfileUsage 17 16) -or $null -ne (Convert-ProfileUsage 0 0) -or $null -eq (Convert-ProfileUsage 0 16)){throw 'Usage validation failed'}
    $raw|ConvertTo-Json -Depth 5|Set-Content $path
    $b=Get-PulseSnapshot $path ($now.AddSeconds(16))
    if($b.state -ne 'STALE' -or $b.values.Count -ne 0){throw 'Stale values leaked'}
    $c=Get-PulseSnapshot $path ($now.AddSeconds(-60))
    if($c.state -ne 'STALE'){throw 'Future timestamp accepted'}
    '{"schema":'|Set-Content $path
    if((Get-PulseSnapshot $path).state -ne 'OFFLINE'){throw 'Truncated JSON accepted'}
    $shared=[IO.File]::Open($path,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    try {
        [IO.File]::WriteAllText($path+'.tmp','{}')
        [IO.File]::Replace($path+'.tmp',$path,[Management.Automation.Language.NullString]::Value)
    } finally {$shared.Dispose()}
    $integrated=@{schema=2;time=$now.ToString('o');pid=3;sequence=1;ramUsage=@{usedGb=8;totalGb=16};sensors=@(
        (Sensor '/gpu-intel/test/load/3' '/gpu-intel/test' 'GpuIntel' 'D3D 3D' 'Load' 2.5),
        (Sensor '/gpu-intel/test/memory/0' '/gpu-intel/test' 'GpuIntel' 'D3D Shared Memory Used' 'SmallData' 700),
        (Sensor '/gpu-intel/test/memory/1' '/gpu-intel/test' 'GpuIntel' 'D3D Shared Memory Total' 'SmallData' 8192)
    )}
    $integrated | ConvertTo-Json -Depth 6 | Set-Content $path
    $intel=Get-PulseSnapshot $path $now
    if($intel.values.gpuLoad -ne 2.5 -or $intel.usage.vram.label -ne 'Shared GPU memory' -or $intel.usage.vram.total -ne 8){throw 'Integrated GPU load/shared-memory discovery failed'}
    if($intel.available.gpu -or $intel.available.ramA -or $intel.available.diskD -or -not $intel.available.gpuLoad){throw 'Missing sensors misreported as supported'}
    $integrated.sensors[0].value=$null
    $integrated | ConvertTo-Json -Depth 6 | Set-Content $path
    $intel=Get-PulseSnapshot $path $now
    if(-not $intel.available.gpuLoad -or $intel.values.ContainsKey('gpuLoad')){throw 'Null reading must retain sensor capability without a fabricated value'}
    'PASS: legacy, Intel integrated and AMD discovery; shared-memory units, missing/null capabilities and stale data'
} finally {if(Test-Path $path){Remove-Item -LiteralPath $path}}
