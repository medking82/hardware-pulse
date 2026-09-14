# Select semantic sensor names within one device. Unknown/ambiguous signals remain unavailable.
function Find-ProfileSensor($items,[string]$type,[string[]]$patterns){
    foreach($pattern in $patterns){
        $matches=@($items | Where-Object {$_.type -eq $type -and $_.name -match $pattern})
        if($matches.Count -eq 1){return $matches[0]}
    }
    return $null
}
function Get-DeviceProfile($raw){
    $items=@($raw.sensors)
    $chosen=@{};$names=@{}
    $cpuId=@($items | Where-Object {$_.hardwareType -eq 'Cpu'} | Select-Object -ExpandProperty hardwareId -Unique | Sort-Object | Select-Object -First 1)
    $cpu=@($items | Where-Object {$cpuId.Count -and $_.hardwareId -eq $cpuId[0]})
    $chosen.cpu=Find-ProfileSensor $cpu 'Temperature' @('^Core \(Tctl/Tdie\)$','^CPU \(Tctl/Tdie\)$','^CPU Package$','^Core \(Tdie\)$')
    $chosen.cpuLoad=Find-ProfileSensor $cpu 'Load' @('^CPU Total$')
    if($cpu.Count){$names.CPU=$cpu[0].hardware}
    $gpus=@($items | Where-Object {$_.hardwareType -match '^Gpu'} | Group-Object hardwareId | Sort-Object @{Expression={if($_.Group[0].hardwareType -eq 'GpuNvidia'){0}elseif($_.Group[0].hardware -match 'Radeon\(TM\) Graphics|Intel.*Graphics'){2}else{1}}},Name)
    $gpu=if($gpus.Count){@($gpus[0].Group)}else{@()}
    $chosen.gpu=Find-ProfileSensor $gpu 'Temperature' @('^GPU Core$','^GPU Temperature$')
    $chosen.gpuLoad=Find-ProfileSensor $gpu 'Load' @('^GPU Core$')
    if(-not $chosen.gpuLoad -and $gpu.Count -and $gpu[0].hardwareType -eq 'GpuIntel'){
        $chosen.gpuLoad=Find-ProfileSensor $gpu 'Load' @('^D3D 3D$')
    }
    $chosen.vram=Find-ProfileSensor $gpu 'Temperature' @('^GPU Memory Junction$','^GPU Memory$')
    $chosen.gpuVolt=Find-ProfileSensor $gpu 'Voltage' @('^GPU Core Voltage$','^GPU Core$')
    $chosen.gpuFan=Find-ProfileSensor $gpu 'Fan' @('^GPU Fan 1$','^GPU Fan$','^GPU$')
    $chosen.gpuFan2=Find-ProfileSensor $gpu 'Fan' @('^GPU Fan 2$')
    if($gpu.Count){$names.GPU=$gpu[0].hardware}
    $board=@($items | Where-Object {$_.hardwareType -eq 'SuperIO'})
    $chosen.vcore=Find-ProfileSensor $board 'Voltage' @('^Vcore$','^CPU VCore$')
    $chosen.cpuFan=Find-ProfileSensor $board 'Fan' @('^CPU Fan$','^CPU$')
    $chosen.system=Find-ProfileSensor $board 'Temperature' @('^System$','^Motherboard$')
    $fans=@($board | Where-Object {$_.type -eq 'Fan' -and $_.id -ne $chosen.cpuFan.id -and $null -ne $_.value} | Sort-Object id)
    # This board's verified SYS1/SYS3 inputs retain their slots, without guessing physical placement.
    if($raw.boardName -match 'B850M MORTAR'){
        $chosen.bottom=@($fans | Where-Object {$_.id -eq '/lpc/nct6687dr/0/fan/10'} | Select-Object -First 1)[0]
        $chosen.top=@($fans | Where-Object {$_.id -eq '/lpc/nct6687dr/0/fan/12'} | Select-Object -First 1)[0]
    }else{
        if($fans.Count -gt 0){$chosen.bottom=$fans[0]}
        if($fans.Count -gt 1){$chosen.top=$fans[1]}
    }
    $names.Airflow=if($raw.boardName){$raw.boardName}else{'Motherboard'}
    $names.Memory=if($raw.memoryName){$raw.memoryName}else{'Memory'}
    $brands=@($raw.memoryModules | ForEach-Object {($_.brand+' '+$_.part).Trim()} | Where-Object {$_} | Select-Object -Unique)
    $slots=@($raw.memoryModules | ForEach-Object {$_.slot -replace '^DIMM',''} | Where-Object {$_})
    if($brands.Count){$names.Memory+="`n"+($brands -join ' / ')}
    if($slots.Count){$names.Memory+=' · Slots '+($slots -join ' / ')}
    $dimms=@($items | Where-Object {$_.id -match '^/memory/dimm/' -and $_.type -eq 'Temperature' -and $_.name -match '^DIMM #\d+$'} | Group-Object hardwareId | Sort-Object Name)
    for($i=0;$i -lt [Math]::Min(2,$dimms.Count);$i++){
        $key=@('ramA','ramB')[$i]
        $candidate=@($dimms[$i].Group)
        if($candidate.Count -eq 1){$chosen[$key]=$candidate[0];$names[$key]=$candidate[0].name -replace '^DIMM','SPD'}
    }
    $disks=@($items | Where-Object {$_.id -match '^/nvme/'} | Group-Object hardwareId | Sort-Object Name)
    for($i=0;$i -lt [Math]::Min(2,$disks.Count);$i++){
        $key=@('diskC','diskD')[$i]
        $chosen[$key]=Find-ProfileSensor $disks[$i].Group 'Temperature' @('^Temperature$','^Composite$','^Composite Temperature$')
        $names[$key]=$disks[$i].Group[0].hardware
        $model=[string]$names[$key]
        $diskMatch=@($raw.disks | Where-Object {($_.model -replace '[^a-zA-Z0-9]','') -eq ($model -replace '[^a-zA-Z0-9]','')})
        if($diskMatch.Count -eq 1 -and @($diskMatch[0].volumes).Count){
            $shortModel=$model -replace '^KIOXIA-EXCERIA ',' ' -replace ' SSD$',''
            $names[$key]=(@($diskMatch[0].volumes) -join ' / ')+'  '+$shortModel.Trim()
        }
    }
    foreach($key in @('cpuFan','bottom','top')){if($chosen[$key]){$names[$key]=$chosen[$key].name}}
    $usage=@{}
    if($raw.ramUsage){$usage.ram=Convert-ProfileUsage $raw.ramUsage.usedGb $raw.ramUsage.totalGb}
    $used=Find-ProfileSensor $gpu 'SmallData' @('^GPU Memory Used$')
    $total=Find-ProfileSensor $gpu 'SmallData' @('^GPU Memory Total$')
    if($used -and $total -and $null -ne $used.value -and $null -ne $total.value){$usage.vram=Convert-ProfileUsage ([double]$used.value/1024) ([double]$total.value/1024)}
    if(-not $usage.vram -and $gpu.Count -and $gpu[0].hardwareType -eq 'GpuIntel'){
        $used=Find-ProfileSensor $gpu 'SmallData' @('^D3D Shared Memory Used$')
        $total=Find-ProfileSensor $gpu 'SmallData' @('^D3D Shared Memory Total$')
        if($used -and $total -and $null -ne $used.value -and $null -ne $total.value){
            $usage.vram=Convert-ProfileUsage ([double]$used.value/1024) ([double]$total.value/1024)
            if($usage.vram){$usage.vram.label='Shared GPU memory'}
        }
    }
    return @{sensors=$chosen;names=$names;usage=$usage;gpuFanCount=if($chosen.gpuFan2){2}elseif($chosen.gpuFan){1}else{0}}
}
function Convert-ProfileUsage($used,$total){
    if($null -eq $used -or $null -eq $total){return $null}
    $u=[double]$used;$t=[double]$total
    if([double]::IsNaN($u) -or [double]::IsNaN($t) -or [double]::IsInfinity($u) -or [double]::IsInfinity($t) -or $u -lt 0 -or $t -le 0 -or $u -gt $t){return $null}
    return @{used=$u;total=$t;percent=100*$u/$t}
}
