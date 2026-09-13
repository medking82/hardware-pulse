$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
foreach($file in Get-ChildItem "$root/src","$root/scripts" -Filter *.ps1){
    $bytes=[IO.File]::ReadAllBytes($file.FullName)
    $null=[Text.UTF8Encoding]::new($false,$true).GetString($bytes)
    if(-not($bytes.Length -ge 3 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191)){throw "UTF-8 BOM required for Windows PowerShell: $($file.Name)"}
    $tokens=$null;$errors=$null
    $null=[Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$errors)
    if($errors){throw "$($file.Name): $errors"}
}
foreach($file in Get-ChildItem "$root/assets" -Filter *.svg){$null=[xml](Get-Content $file.FullName -Raw)}
$null=[xml](Get-Content "$root/src/Panel.xaml" -Raw)
& "$PSScriptRoot/Run-Hidden.ps1" "$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe" @('-NoProfile','-File',"$root/src/Test-Sensors.ps1") $root
& "$PSScriptRoot/Run-Hidden.ps1" "$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe" @('-NoProfile','-STA','-File',"$PSScriptRoot/Test-Snap.ps1") $root
& "$PSScriptRoot/Test-Settings.ps1"
foreach($file in Get-ChildItem "$root/src" -File){
    if((Get-Content $file.FullName -Raw) -match 'C:\\github\\|C:\\Users\\Marck|HWiNFO') {throw "Personal/deprecated dependency in $($file.Name)"}
}
'Validation passed: script syntax, XML/SVG, sensor regression, portable source paths.'
