param([switch]$Live)
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path (Split-Path $PSScriptRoot) 'src/UpdateCheck.cs')
$tag='v0.4.2';$url='https://github.com/medking82/hardware-pulse/releases/download/v0.4.2/HardwarePulse-Setup.exe';$digest='sha256:'+('a'*64)
if(-not [UpdateCheck]::ValidAsset($url,$tag,$digest,100)){throw 'Valid asset rejected'}
foreach($bad in @($url.Replace('https:','http:'),$url.Replace('medking82','someone'),($url+'?file=evil'),$url.Replace('github.com','github.com.evil.test'),'file:///C:/evil.exe')){
    if([UpdateCheck]::ValidAsset($bad,$tag,$digest,100)){throw ('Invalid asset accepted: '+$bad)}
}
foreach($size in @(0,-1,104857601)){if([UpdateCheck]::ValidAsset($url,$tag,$digest,$size)){throw 'Invalid size accepted'}}
if([UpdateCheck]::ValidAsset($url,$tag,'',100)){throw 'Missing checksum accepted'}
$stream=[IO.MemoryStream]::new([Text.Encoding]::UTF8.GetBytes('abc'))
try {if(-not [UpdateCheck]::HashMatches($stream,'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad')){throw 'SHA-256 validation failed'}}finally{$stream.Dispose()}
$updater=[UpdateCheck]::new();$rejected=$false
try{$updater.Install()}catch{$rejected=$true}
if(-not $rejected){throw 'Unverified install permitted'}
'PASS: trusted asset identity, HTTPS, size bounds, required digest, SHA-256 and unverified-install rejection'
if($Live){
    $release=Invoke-RestMethod 'https://api.github.com/repos/medking82/hardware-pulse/releases/latest'
    $asset=@($release.assets | Where-Object name -eq 'HardwarePulse-Setup.exe')[0]
    $updater.Download($asset.browser_download_url,$release.tag_name,$asset.digest,$asset.size)
    try {
        if(-not $updater.DownloadPending.Wait(60000)){throw 'Live download timeout'}
        $download=$updater.DownloadPending.Result
        $sha=[Security.Cryptography.SHA256]::Create();$file=[IO.File]::OpenRead($download)
        try{$actual=[BitConverter]::ToString($sha.ComputeHash($file)).Replace('-','').ToLowerInvariant()}finally{$file.Dispose();$sha.Dispose()}
        if($actual -ne $asset.digest.Substring(7)){throw 'Live download hash mismatch'}
        $completedTask=$updater.DownloadPending
        $updater.Download($asset.browser_download_url,$release.tag_name,$asset.digest,$asset.size)
        if(-not $updater.Ready -or -not [object]::ReferenceEquals($completedTask,$updater.DownloadPending)){throw 'Ready update was downloaded again'}
        # Corrupt only the new test download, proving install rejects tampering before UAC/launch.
        $file=[IO.File]::OpenWrite($download);try{$file.WriteByte(0)}finally{$file.Dispose()}
        $rejected=$false;try{$updater.Install()}catch{$rejected=$true}
        if(-not $rejected){throw 'Tampered installer accepted'}
        if($updater.Ready){throw 'Tampered cache prevents a fresh retry'}
        'PASS: real GitHub download, checksum, and tampered-cache rejection without launching installer'
    }finally{$updater.CancelDownload();if($download){Remove-Item -LiteralPath $download -Force}}
}
