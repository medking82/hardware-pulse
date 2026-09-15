param([string]$BaselineRef='51ef6d8dc61534c5896ccc18a869bb945202eebc',[int]$Runs=3)
$ErrorActionPreference='Stop'
if($Runs -lt 1 -or $Runs -gt 10){throw 'Runs must be 1..10'}
$root=Split-Path $PSScriptRoot
$output=Join-Path $root ('vendor/fps-history-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output | Out-Null
$git=(Get-Command git.exe).Source
$baseline=& "$PSScriptRoot/Run-Hidden.ps1" $git @('show',($BaselineRef+':src/Core/FrameHistory.cs')) $root
[IO.File]::WriteAllText((Join-Path $output 'Baseline.cs'),($baseline -join "`n"))
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
foreach($target in @('baseline','candidate')){
    $source=if($target -eq 'baseline'){Join-Path $output 'Baseline.cs'}else{Join-Path $root 'src/Core/FrameHistory.cs'}
    & "$PSScriptRoot/Run-Hidden.ps1" $compiler @('/nologo','/optimize+','/platform:x64','/target:exe',"/out:$output/$target.exe",(Join-Path $root 'scripts/FpsHistoryBench.cs'),$source) $root
}
$results=@()
for($run=1;$run -le $Runs;$run++){
    foreach($target in @('baseline','candidate')){
        $raw=& "$PSScriptRoot/Run-Hidden.ps1" (Join-Path $output "$target.exe") @() $root
        $row=($raw -join '')|ConvertFrom-Json
        $row|Add-Member target $target;$row|Add-Member run $run;$results+=$row
    }
}
$results|ConvertTo-Json|Set-Content (Join-Path $output 'results.json') -Encoding utf8
$results|Format-Table target,run,cpuMsPerRead,elapsedMsPerRead,allocatedBytesPerRead,privateBytes
"Evidence: $output"