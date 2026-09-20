#requires -Version 7.0
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$fixture=Join-Path $root ('vendor/package-verifier-'+[Guid]::NewGuid().ToString('N'))
Copy-Item -LiteralPath "$root/build/windows-shared/app" -Destination $fixture -Recurse
$manifestPath=Join-Path $fixture 'manifest.json'
$original=[IO.File]::ReadAllText($manifestPath)
function Reject([string]$Expected){
    try{& "$PSScriptRoot/Test-WindowsShared.ps1" -AppDirectory $fixture}
    catch{if($_.Exception.Message.Contains($Expected)){return};throw}
    throw "Verifier accepted invalid package: $Expected"
}
& "$PSScriptRoot/Test-WindowsShared.ps1" -AppDirectory $fixture
$license=Join-Path $fixture 'LICENSE';$licenseBytes=[IO.File]::ReadAllBytes($license)
try{[IO.File]::AppendAllText($license,'tamper');Reject 'SHA-256 mismatch'}
finally{[IO.File]::WriteAllBytes($license,$licenseBytes)}
$manifest=$original | ConvertFrom-Json -AsHashtable
$manifest.files.Remove('LICENSE');Remove-Item -LiteralPath $license
try{$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM;Reject 'Required package file is missing: LICENSE'}
finally{[IO.File]::WriteAllBytes($license,$licenseBytes);[IO.File]::WriteAllText($manifestPath,$original)}
$private=Join-Path $fixture 'auth.json';[IO.File]::WriteAllText($private,'{}')
try{
    $manifest=$original | ConvertFrom-Json -AsHashtable
    $manifest.files['auth.json']=(Get-FileHash -LiteralPath $private).Hash.ToLowerInvariant()
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM
    Reject 'forbidden development/private files'
}finally{Remove-Item -LiteralPath $private;[IO.File]::WriteAllText($manifestPath,$original)}
& "$PSScriptRoot/Test-WindowsShared.ps1" -AppDirectory $fixture
"PASS package verifier: valid payload, tampering, missing required file and private artifact rejection. Evidence: $fixture"
