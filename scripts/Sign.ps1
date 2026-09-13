param([Parameter(Mandatory=$true)][string]$Path,[Parameter(Mandatory=$true)][string]$Thumbprint)
$ErrorActionPreference='Stop'
$cert=Get-Item -LiteralPath ('Cert:\CurrentUser\My\'+$Thumbprint)
if(-not $cert.HasPrivateKey -or $cert.NotAfter -le (Get-Date)){throw 'A current code-signing certificate with a private key is required.'}
if('1.3.6.1.5.5.7.3.3' -notin @($cert.EnhancedKeyUsageList | ForEach-Object {[string]$_.ObjectId})){throw 'Certificate does not permit code signing.'}
$result=Set-AuthenticodeSignature -FilePath $Path -Certificate $cert -HashAlgorithm SHA256
if(-not $result.SignerCertificate -or $result.SignerCertificate.Thumbprint -ne $Thumbprint){throw 'Authenticode signer did not match the requested certificate.'}
if($result.Status -notin @('Valid','NotTrusted','UnknownError')){throw ('Signing failed: '+$result.StatusMessage)}
# Untrusted-root status is expected for a self-signed certificate; never install it in Root.
[pscustomobject]@{File=(Split-Path $Path -Leaf);Signer=$result.SignerCertificate.Subject;Status=[string]$result.Status;Message=$result.StatusMessage}
