param([Parameter(Mandatory=$true)][string]$FilePath,[string[]]$ArgumentList=@(),[string]$WorkingDirectory=$PSScriptRoot)
$info=[Diagnostics.ProcessStartInfo]::new();$info.FileName=$FilePath;$info.WorkingDirectory=$WorkingDirectory
$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
foreach($argument in $ArgumentList){$info.ArgumentList.Add($argument)}
$process=[Diagnostics.Process]::new();$process.StartInfo=$info;$null=$process.Start()
$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync();$process.WaitForExit()
$stdout.Result; if($stderr.Result){Write-Host $stderr.Result}
if($process.ExitCode -ne 0){throw "$FilePath failed with exit code $($process.ExitCode)"}
$process.Dispose()
