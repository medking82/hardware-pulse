$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$utf8=[Text.UTF8Encoding]::new($false,$true)
$fixture=Join-Path $root ('vendor/version-tests-'+[Guid]::NewGuid().ToString('N'))
$paths=@('src/Native/Program.cs','src/Panel.xaml','src/Native/Languages.txt',
    'installer/HardwarePulse.iss','scripts/Build.ps1','scripts/Test-Package.ps1',
    'src/Adapters/Windows/CodexAppServerQuota.cs','README.md','README.zh-CN.md')
$original=@{};$expected=@{}
$source=[IO.File]::ReadAllText((Join-Path $root $paths[0]))
$current=[regex]::Match($source,'AssemblyVersion\("(\d+\.\d+\.\d+)\.0"\)').Groups[1].Value
$parts=$current.Split('.');$next=$parts[0]+'.'+$parts[1]+'.'+(1+[int]$parts[2])
function Assert-Bytes([string]$relative,[byte[]]$bytes){
    $actual=[IO.File]::ReadAllBytes((Join-Path $fixture $relative))
    if([Convert]::ToBase64String($actual) -cne [Convert]::ToBase64String($bytes)){throw "Unexpected bytes: $relative"}
}
function Assert-Unchanged{foreach($relative in $paths){Assert-Bytes $relative $original[$relative]}}
function Assert-Rejected([scriptblock]$action,[string]$message){
    $rejected=$false
    try{& $action | Out-Null}catch{if(-not $_.Exception.Message.Contains($message)){throw};$rejected=$true}
    if(-not $rejected){throw "Expected rejection: $message"}
}
# Actual release inputs with both BOM forms, non-ASCII text, and mixed newlines.
foreach($relative in $paths){
    $destination=Join-Path $fixture $relative
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))
    $text=[IO.File]::ReadAllText((Join-Path $root $relative)).Replace("`r`n","`n")
    $first=$text.IndexOf("`n");if($first -ge 0){$text=$text.Insert($first,"`r")}
    if($paths.IndexOf($relative)%2 -eq 0){$text=[string][char]0xFEFF+$text}
    $expected[$relative]=$utf8.GetBytes($text.Replace($current,$next))
    $original[$relative]=$utf8.GetBytes($text)
    [IO.File]::WriteAllBytes($destination,$original[$relative])
}
$helper=Join-Path $fixture 'scripts/Set-Version.ps1'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Set-Version.ps1') -Destination $helper
# A longer version and historical release notes must stay untouched.
$marker="`nHistorical example $($current)0; 中文保留。`r`n"
$readme=Join-Path $fixture 'README.md'
$original['README.md']=$utf8.GetBytes($utf8.GetString($original['README.md'])+$marker)
$expected['README.md']=$utf8.GetBytes($utf8.GetString($expected['README.md'])+$marker)
[IO.File]::WriteAllBytes($readme,$original['README.md'])
$history=Join-Path $fixture 'CHANGELOG.md'
[IO.File]::WriteAllText($history,"Historical $current",$utf8)
$historyBytes=[IO.File]::ReadAllBytes($history)
& $helper -Check
& $helper -ExpectedVersion $current -Version $next -WhatIf
Assert-Unchanged
Assert-Rejected {& $helper -ExpectedVersion $next -Version $next} 'does not match current version'
Assert-Unchanged
Assert-Rejected {& $helper -ExpectedVersion $current -Version '65535.0.0'} 'at most 65534'
Assert-Unchanged
# Late-file drift must reject without partially bumping earlier files.
$client='src/Adapters/Windows/CodexAppServerQuota.cs'
[IO.File]::WriteAllBytes((Join-Path $fixture $client),$expected[$client])
Assert-Rejected {& $helper -ExpectedVersion $current -Version $next} 'Version mismatch'
[IO.File]::WriteAllBytes((Join-Path $fixture $client),$original[$client])
Assert-Unchanged
# Direct Build must discover drift before deleting an existing package.
[IO.File]::WriteAllBytes((Join-Path $fixture 'installer/HardwarePulse.iss'),$expected['installer/HardwarePulse.iss'])
$app=Join-Path $fixture 'build/app';[void][IO.Directory]::CreateDirectory($app)
$sentinel=Join-Path $app 'keep.txt';[IO.File]::WriteAllText($sentinel,'keep')
Assert-Rejected {& (Join-Path $fixture 'scripts/Build.ps1')} 'Version mismatch'
if([IO.File]::ReadAllText($sentinel) -ne 'keep'){throw 'Version rejection destroyed build output'}
[IO.File]::WriteAllBytes((Join-Path $fixture 'installer/HardwarePulse.iss'),$original['installer/HardwarePulse.iss'])
Assert-Unchanged
$last=Join-Path $fixture $paths[-1]
try{
    (Get-Item -LiteralPath $last).IsReadOnly=$true
    Assert-Rejected {& $helper -ExpectedVersion $current -Version $next} 'Read-only version file'
    Assert-Unchanged
}finally{(Get-Item -LiteralPath $last).IsReadOnly=$false}
& $helper -ExpectedVersion $current -Version $next
& $helper -Check
foreach($relative in $paths){Assert-Bytes $relative $expected[$relative]}
Assert-Bytes 'CHANGELOG.md' $historyBytes
& $helper -ExpectedVersion $next -Version $current
Assert-Unchanged
"PASS: version bump preserves BOM/newlines/text/history; preview, drift and read-only preflight are safe. Evidence: $fixture"
