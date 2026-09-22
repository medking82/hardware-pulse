[CmdletBinding(SupportsShouldProcess=$true,DefaultParameterSetName='Check')]
param(
    [Parameter(Mandatory=$true,ParameterSetName='Set')]
    [ValidatePattern('^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$')][string]$Version,
    [Parameter(Mandatory=$true,ParameterSetName='Set')][string]$ExpectedVersion,
    [Parameter(Mandatory=$true,ParameterSetName='Check')][switch]$Check
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
# Strict decoding retains any BOM as U+FEFF. Encoding that string back to bytes
# preserves the original BOM, non-ASCII text and every existing newline verbatim.
$utf8=[Text.UTF8Encoding]::new($false,$true)
$capture='(?<version>\d+\.\d+\.\d+)'
$rules=@(
    @('src/Native/Program.cs',('AssemblyVersion\("'+$capture+'\.0"\)'),1),
    @('src/Panel.xaml',('Version '+$capture),1),
    @('src/Native/Languages.txt',('(?:Version|版本) '+$capture),3),
    @('installer/HardwarePulse.iss',('(?m)^(?:AppVersion=|OutputBaseFilename=HardwarePulse-)'+$capture),2),
    @('scripts/Build.ps1',('HardwarePulse-'+$capture+'-Setup\.exe'),1),
    @('scripts/Test-Package.ps1',("'"+$capture+"\.0'"),1),
    @('src/Adapters/Windows/CodexAppServerQuota.cs',([regex]::Escape('\"version\":\"')+$capture+[regex]::Escape('\"')),1),
    @('README.md',('releases/(?:download/v|tag/v)'+$capture+'(?=/|\))'),2),
    @('README.zh-CN.md',('releases/(?:download/v|tag/v)'+$capture+'(?=/|\))'),1)
)
$files=@()
$current=$null
foreach($rule in $rules){
    $path=Join-Path $root $rule[0]
    $bytes=[IO.File]::ReadAllBytes($path)
    $text=$utf8.GetString($bytes)
    $matches=[regex]::Matches($text,$rule[1])
    if($matches.Count -ne $rule[2]){throw "Missing or ambiguous version fields: $($rule[0])"}
    if(-not $current){$current=$matches[0].Groups['version'].Value}
    foreach($match in $matches){
        if($match.Groups['version'].Value -ne $current){throw "Version mismatch in $($rule[0]); expected $current"}
    }
    $files+=@{Path=$path;Relative=$rule[0];Bytes=$bytes;Text=$text}
}
if($Check){"PASS: native version $current agrees across $($files.Count) release files";return}
if($ExpectedVersion -cne $current){throw "Expected version $ExpectedVersion does not match current version $current"}
if($Version -eq $current){throw 'New version must differ from the current version'}
foreach($part in $Version.Split('.')){if([long]$part -gt 65534){throw 'Assembly version components must be at most 65534'}}
$pattern='(?<![\d.])'+[regex]::Escape($current)+'(?!\d)'
# Finish all discoverable validation before changing the first file.
foreach($file in $files){
    if((Get-Item -LiteralPath $file.Path).IsReadOnly){throw "Read-only version file: $($file.Relative)"}
    $file.Next=$utf8.GetBytes([regex]::Replace($file.Text,$pattern,$Version))
}
if(-not $PSCmdlet.ShouldProcess($root,"Update native version $current -> $Version in $($files.Count) files")){return}
$written=@()
try{
    foreach($file in $files){
        $written+=$file
        [IO.File]::WriteAllBytes($file.Path,$file.Next)
    }
}catch{
    $failure=$_
    foreach($file in $written){[IO.File]::WriteAllBytes($file.Path,$file.Bytes)}
    throw $failure
}
"Updated native version $current -> $Version in $($files.Count) files; BOM and newlines preserved"
