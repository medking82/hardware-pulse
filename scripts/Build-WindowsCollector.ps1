param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$version=[string]([xml](Get-Content "$root/src/Hosts/Desktop/Pulse.Desktop.csproj" -Raw)).Project.PropertyGroup.Version
if($version -notmatch '^(\d+\.\d+\.\d+)(?:-[0-9A-Za-z.-]+)?$'){throw 'Invalid shared version'}
$assemblyVersion=$Matches[1]+'.0'
$payload=[IO.Path]::GetFullPath((Join-Path $root 'build/windows-worker'))
for($ancestor=$payload;$ancestor.Length -ge $root.Length;$ancestor=[IO.Path]::GetDirectoryName($ancestor)){
    if((Test-Path -LiteralPath $ancestor) -and ((Get-Item -LiteralPath $ancestor).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Worker output traverses a reparse point'}
    if($ancestor -eq $root){break}
}
if(Test-Path -LiteralPath $payload){
    if($payload -ne [IO.Path]::GetFullPath((Join-Path $root 'build/windows-worker'))){throw 'Unexpected worker output'}
    if(@(Get-Item -LiteralPath $payload; Get-ChildItem -LiteralPath $payload -Recurse -Force) | Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}){throw 'Worker output contains a reparse point'}
    Remove-Item -LiteralPath $payload -Recurse -Force
}
$worker=Join-Path $payload 'worker'
$null=New-Item -ItemType Directory -Path "$worker/lib","$payload/tools","$payload/licenses" -Force
$deps=Get-Content "$root/dependencies.lock.json" -Raw | ConvertFrom-Json
foreach($name in @('LibreHardwareMonitor','PresentMon','LibreHardwareMonitor Source','PawnIO Source')){
    $dep=@($deps | Where-Object name -eq $name)
    if($dep.Count -ne 1){throw "Missing unique dependency: $name"}
    $file=Join-Path "$root/vendor" $dep[0].file
    if(-not(Test-Path -LiteralPath $file)){
        $null=New-Item -ItemType Directory -Path "$root/vendor" -Force
        Invoke-WebRequest $dep[0].url -OutFile $file
    }
    if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $dep[0].sha256){throw "Dependency hash mismatch: $name"}
}
# Extract only verified libraries, avoiding unverified leftovers in vendor/lhm.
$lhm=Join-Path $payload 'lhm-extract'
Expand-Archive "$root/vendor/LibreHardwareMonitor-0.9.6.zip" $lhm
Copy-Item "$lhm/*.dll" "$worker/lib"
Remove-Item -LiteralPath $lhm -Recurse -Force
Copy-Item "$root/vendor/PresentMon-2.5.1-x64.exe" "$payload/tools/PresentMon.exe"
Copy-Item "$root/src/HardwarePulse.exe.config" "$worker/HardwarePulse.Collector.exe.config"
Copy-Item "$root/licenses/*" "$payload/licenses" -Recurse
Copy-Item "$root/vendor/LibreHardwareMonitor-source-0.9.6.zip","$root/vendor/PawnIO-source-2.2.0.zip" "$payload/licenses"
Copy-Item "$root/LICENSE","$root/dependencies.lock.json" $payload
& "$PSScriptRoot/Build-WindowsAdapters.ps1" -OutputDirectory $worker
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$versionSource=Join-Path $payload 'WorkerVersion.cs'
[IO.File]::WriteAllText($versionSource,"[assembly:System.Reflection.AssemblyVersion(`"$assemblyVersion`")]`n[assembly:System.Reflection.AssemblyInformationalVersion(`"$version`")]",[Text.UTF8Encoding]::new($false))
$arguments=@('/nologo','/target:winexe','/platform:x64','/optimize+',"/out:$worker/HardwarePulse.Collector.exe",'/reference:System.Web.Extensions.dll','/reference:System.Management.dll',"/reference:$worker/Pulse.Core.dll","/reference:$worker/Pulse.Adapters.Windows.dll","/reference:$worker/lib/LibreHardwareMonitorLib.dll")
$arguments+=@('/reference:Microsoft.CSharp.dll')
$arguments+=$versionSource
$arguments+=@('src/Native/Collector.cs','src/Native/FpsTransport.cs','src/Native/Startup.cs','src/Hosts/WindowsCollector/Program.cs') | ForEach-Object {[IO.Path]::GetFullPath((Join-Path $root $_))}
& "$PSScriptRoot/Run-Hidden.ps1" $compiler $arguments $root
Remove-Item -LiteralPath $versionSource
'Built dedicated Windows x64 collector payload; no installation or startup registration.'
