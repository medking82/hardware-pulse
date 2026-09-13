param([int]$Samples=0)
$ErrorActionPreference='Stop'
. "$PSScriptRoot\Paths.ps1"
New-Item -ItemType Directory $runtime -Force | Out-Null
$mutex=New-Object Threading.Mutex($false,'Local\HardwarePulseCollector')
if(-not $mutex.WaitOne(0)){exit}
$computer=$null
try {
    'Loading Library' | Set-Content "$runtime\collector-stage.txt"
    Add-Type -Path "$PSScriptRoot\lib\LibreHardwareMonitorLib.dll"
    $computer=New-Object LibreHardwareMonitor.Hardware.Computer
    $computer.IsCpuEnabled=$true; $computer.IsGpuEnabled=$true
    $computer.IsMemoryEnabled=$true; $computer.IsMotherboardEnabled=$true
    $computer.IsStorageEnabled=$true
    'Opening Hardware' | Set-Content "$runtime\collector-stage.txt"
    $computer.Open()
    $memory=@(Get-CimInstance Win32_PhysicalMemory -ErrorAction SilentlyContinue)
    $memoryGb=[Math]::Round(($memory | Measure-Object Capacity -Sum).Sum/1GB)
    $speeds=@($memory.ConfiguredClockSpeed | Where-Object {$_ -gt 0} | Sort-Object -Unique)
    $memoryType=if(@($memory | Where-Object {$_.SMBIOSMemoryType -ne 34}).Count -eq 0 -and $memory.Count){'DDR5'}elseif(@($memory | Where-Object {$_.SMBIOSMemoryType -ne 26}).Count -eq 0 -and $memory.Count){'DDR4'}else{'RAM'}
    $memoryName=if($memoryGb){"$memoryGb GB $memoryType"}else{'Memory'}
    if($speeds.Count -eq 1){$memoryName+='-'+$speeds[0]+' configured'}
    $memoryModules=@($memory | ForEach-Object {@{brand=$_.Manufacturer.Trim();part=$_.PartNumber.Trim();slot=$_.DeviceLocator;capacityGb=[Math]::Round($_.Capacity/1GB)}})
    $board=@($computer.Hardware | Where-Object {$_.HardwareType.ToString() -eq 'Motherboard'} | Select-Object -First 1)
    $diskInfo=@(Get-CimInstance Win32_DiskDrive -ErrorAction SilentlyContinue | ForEach-Object {
        $drive=$_
        $letters=@(Get-CimAssociatedInstance -InputObject $drive -Association Win32_DiskDriveToDiskPartition -ErrorAction SilentlyContinue | ForEach-Object {
            Get-CimAssociatedInstance -InputObject $_ -Association Win32_LogicalDiskToPartition -ErrorAction SilentlyContinue
        } | Select-Object -ExpandProperty DeviceID -Unique | Sort-Object)
        @{model=$drive.Model;volumes=$letters}
    })
    'Reading Sensors' | Set-Content "$runtime\collector-stage.txt"
    function Read-Sensors($h) {
        $h.Update()
        foreach($s in $h.Sensors){[pscustomobject]@{id=$s.Identifier.ToString();name=$s.Name;hardware=$h.Name;hardwareId=$h.Identifier.ToString();hardwareType=$h.HardwareType.ToString();type=$s.SensorType.ToString();value=$s.Value}}
        foreach($child in $h.SubHardware){Read-Sensors $child}
    }
    $sequence=0
    while(-not(Test-Path "$runtime\STOP")) {
        $items=@(foreach($h in $computer.Hardware){Read-Sensors $h})
        $sequence++
        $os=Get-CimInstance Win32_OperatingSystem -ErrorAction SilentlyContinue
        $ramUsage=if($os -and $os.TotalVisibleMemorySize -gt 0){@{totalGb=$os.TotalVisibleMemorySize/1MB;usedGb=($os.TotalVisibleMemorySize-$os.FreePhysicalMemory)/1MB}}else{$null}
        $snapshot=@{schema=2;time=[DateTimeOffset]::Now.ToString('o');sequence=$sequence;pid=$PID;sensors=$items;memoryName=$memoryName;memoryModules=$memoryModules;disks=$diskInfo;ramUsage=$ramUsage;boardName=if($board.Count){$board[0].Name}else{'Motherboard'}}
        $json=$snapshot | ConvertTo-Json -Depth 5 -Compress
        'Writing' | Set-Content "$runtime\collector-stage.txt"
        [IO.File]::WriteAllText("$runtime\snapshot.tmp",$json)
        # A third-party reader may briefly deny Delete sharing; retain the last
        # atomic snapshot and retry next cycle rather than terminating monitoring.
        try {
            if(Test-Path "$runtime\snapshot.json"){[IO.File]::Replace("$runtime\snapshot.tmp","$runtime\snapshot.json",[System.Management.Automation.Language.NullString]::Value)}
            else {[IO.File]::Move("$runtime\snapshot.tmp","$runtime\snapshot.json")}
        } catch [IO.IOException] {
            [IO.File]::WriteAllText("$runtime\write-warning.txt", [DateTimeOffset]::Now.ToString('o')+' '+$_.Exception.Message)
        }
        if($Samples -gt 0 -and $sequence -ge $Samples){break}
        Start-Sleep -Seconds 2
    }
} catch {
    [IO.File]::WriteAllText("$runtime\collector-error.txt", $_.Exception.ToString())
    throw
} finally {
    if($computer){$computer.Close()}
    $mutex.ReleaseMutex(); $mutex.Dispose()
}
