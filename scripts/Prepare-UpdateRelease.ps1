#Requires -Version 7.0
param(
 [Parameter(Mandatory)][string]$Installer,
 [Parameter(Mandatory)][ValidatePattern('^\d{1,6}\.\d{1,6}\.\d{1,6}(\.\d{1,6})?$')][string]$Version,
 [Parameter(Mandatory)][string]$ReleaseNotesFile,
 [Parameter(Mandatory)][string]$OutputDirectory,
 [ValidateSet('https://195.78.49.209:8443/notchflow/releases/','http://127.0.0.1:8765/notchflow/releases/')]
 [string]$ReleaseBaseUri='https://195.78.49.209:8443/notchflow/releases/',
 [string]$KeyFile=(Join-Path $env:LOCALAPPDATA 'NotchFlowReleaseKeys/update-signing-key.dpapi')
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot))
$key=[IO.Path]::GetFullPath($KeyFile)
if($key.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Private key must be outside the repository.'}
if(Test-Path $OutputDirectory){throw 'Output directory already exists; use a fresh directory.'}
$inputFile=Get-Item -LiteralPath $Installer
if($inputFile.Extension -ne '.exe' -or $inputFile.Length -le 0 -or $inputFile.Length -gt 512MB){throw 'Invalid installer.'}
$notes=[IO.File]::ReadAllText((Resolve-Path $ReleaseNotesFile))
if($notes.Length -gt 16000){throw 'Release notes too long.'}
$rsa=[Security.Cryptography.RSA]::Create();$verification=[Security.Cryptography.RSA]::Create()
try {
 $bytes=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($key),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
 try{$read=0;$rsa.ImportPkcs8PrivateKey($bytes,[ref]$read)}finally{[Security.Cryptography.CryptographicOperations]::ZeroMemory($bytes)}
 $verification.ImportFromPem([IO.File]::ReadAllText((Join-Path $root 'NotchFlow/Assets/UpdatePublicKey.pem')))
 $name="NotchFlow-$Version-Setup.exe"
 New-Item -ItemType Directory -Path (Join-Path $OutputDirectory 'releases') -Force | Out-Null
 $target=Join-Path $OutputDirectory "releases/$name";Copy-Item -LiteralPath $inputFile.FullName -Destination $target
 $hash=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
 $manifest=[ordered]@{version=$Version;channel='stable';platform='win-x64';downloadUrl="$ReleaseBaseUri$name";sha256=$hash;sizeBytes=(Get-Item $target).Length;releaseNotes=$notes}
 $raw=[Text.UTF8Encoding]::new($false).GetBytes(($manifest|ConvertTo-Json -Depth 5))
 $signature=$rsa.SignData($raw,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pss)
 if(!$verification.VerifyData($raw,$signature,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pss)){throw 'Signing key does not match embedded public key.'}
 [IO.File]::WriteAllBytes((Join-Path $OutputDirectory 'version.json'),$raw)
 [IO.File]::WriteAllText((Join-Path $OutputDirectory 'version.json.sig'),[Convert]::ToBase64String($signature),[Text.Encoding]::ASCII)
 if((Get-FileHash $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash){throw 'Installer changed during preparation.'}
 Write-Output "Verified signed package: $([IO.Path]::GetFullPath($OutputDirectory))"
 Write-Output 'No files have been uploaded. Never upload the key directory.'
}finally{$rsa.Dispose();$verification.Dispose()}
