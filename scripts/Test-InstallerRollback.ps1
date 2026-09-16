#requires -Version 7.0
param([switch]$GuardOutcome)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot))
$compiler=Join-Path $root 'vendor/inno/ISCC.exe'
if(-not(Test-Path -LiteralPath $compiler)){throw 'Prepare the pinned Inno compiler first.'}
$output=Join-Path $root ('vendor/installer-rollback-'+[Guid]::NewGuid().ToString('N'))
for($check=Split-Path $output;$check.Length -ge $root.Length;$check=[IO.Path]::GetDirectoryName($check)){
    if((Get-Item -LiteralPath $check).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Probe output traverses a reparse point'}
}
$null=New-Item -ItemType Directory -Path $output
$token=[Guid]::NewGuid().ToString('N')
$payload=Join-Path $output 'payload.txt'
[IO.File]::WriteAllText($payload,'new payload')
$arguments=@("/O$output","/DProbeToken=$token","/DProbePayload=$payload","$root/installer/tests/RollbackProbe.iss")
if($GuardOutcome){$arguments=@('/DGuardInstallOutcome')+$arguments}
& "$PSScriptRoot/Run-Hidden.ps1" $compiler $arguments $root | Out-File "$output/compile.log" -Encoding utf8
$results=@()
foreach($phase in @('before','prerequisite','during','after','success')){
    $fixture=Join-Path $output $phase
    $installed=Join-Path $fixture 'installed'
    $null=New-Item -ItemType Directory -Path $installed
    Copy-Item -LiteralPath "$output/rollback-probe.exe" -Destination $fixture
    [IO.File]::WriteAllText("$fixture/fixture-owner.txt",$token)
    [IO.File]::WriteAllText("$installed/replaced.txt",'old payload')
    [IO.File]::WriteAllText("$installed/keeper.txt",'user keeper')
    $info=[Diagnostics.ProcessStartInfo]::new("$fixture/rollback-probe.exe")
    $info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.WindowStyle='Hidden';$info.WorkingDirectory=$fixture
    foreach($arg in @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',"/PROBEPHASE=$phase","/LOG=$fixture/setup.log")){$info.ArgumentList.Add($arg)}
    $child=[Diagnostics.Process]::Start($info)
    try{
        if(-not $child.WaitForExit(30000)){throw "Isolated rollback probe is still running: PID $($child.Id); inspect it before another run."}
        $code=$child.ExitCode
    }finally{$child.Dispose()}
    $content=if(Test-Path -LiteralPath "$installed/replaced.txt"){[IO.File]::ReadAllText("$installed/replaced.txt")}else{'<missing>'}
    $new=Test-Path -LiteralPath "$installed/new.txt"
    if([IO.File]::ReadAllText("$installed/keeper.txt") -ne 'user keeper'){throw "Probe changed a keeper: $phase"}
    $preflight=$phase -in @('before','prerequisite')
    $expected=if($preflight){7}elseif($phase -eq 'after' -and $GuardOutcome){10}else{0}
    if($code -ne $expected){throw "Unexpected exit code for ${phase}: $code; inspect $fixture/setup.log"}
    $launch=Test-Path -LiteralPath "$fixture/launch-permitted.txt"
    if($launch -ne ($expected -eq 0)){throw "Launch gate mismatch for $phase"}
    if($preflight -and ($content -ne 'old payload' -or $new)){throw 'Preflight failure changed payload'}
    if(-not $preflight -and ($content -ne 'new payload' -or -not $new)){throw 'Observed file replacement behavior changed; inspect the rollback contract'}
    $log=[IO.File]::ReadAllText("$fixture/setup.log")
    if($phase -ne 'success' -and -not $log.Contains('PROBE deliberate failure')){throw "Failure did not reach the intended boundary: $phase"}
    if($phase -eq 'prerequisite' -and -not $log.Contains('PROBE prerequisite extracted before file replacement')){throw 'Prerequisite probe did not extract its harmless input'}
    $executed=$log.Contains('whoami.exe')
    if($executed -ne $launch){throw "Launch execution mismatch for $phase"}
    if($GuardOutcome -and $executed -and (-not $log.Contains('PROBE post-install completed') -or $log.IndexOf('PROBE post-install completed') -gt $log.IndexOf('whoami.exe'))){throw 'Launch ran without completed post-install work'}
    $results+=[pscustomobject]@{phase=$phase;exitCode=$code;replacedFile=$content;newFilePresent=$new;keeperUnchanged=$true;launchPermitted=$launch;launchExecuted=$executed}
}
$report=[pscustomobject]@{guardOutcome=[bool]$GuardOutcome;compilerSha256=(Get-FileHash $compiler).Hash;probeSha256=(Get-FileHash "$output/rollback-probe.exe").Hash;results=$results}
$report|ConvertTo-Json -Depth 5|Set-Content "$output/result.json" -Encoding utf8
$report|ConvertTo-Json -Depth 5
"Rollback behavior recorded (not a shared installer acceptance): $output"
