#Requires -Version 7.0
param([Parameter(Mandatory)][string]$Destination,[string]$KeyFile=(Join-Path $env:LOCALAPPDATA 'NotchFlowReleaseKeys/update-signing-key.dpapi'))
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot))
$target=[IO.Path]::GetFullPath($Destination)
if($target.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Backups must be outside the repository.'}
if(Test-Path $target){throw 'Backup already exists.'}
$password=Read-Host 'Backup passphrase (store separately; minimum 16 characters)' -AsSecureString
if($password.Length -lt 16){throw 'Passphrase too short.'}
$rsa=[Security.Cryptography.RSA]::Create();$pointer=[IntPtr]::Zero
try {
 $raw=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($KeyFile),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
 try{$read=0;$rsa.ImportPkcs8PrivateKey($raw,[ref]$read)}finally{[Security.Cryptography.CryptographicOperations]::ZeroMemory($raw)}
 $pointer=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($password)
 $plain=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
 $parameters=[Security.Cryptography.PbeParameters]::new([Security.Cryptography.PbeEncryptionAlgorithm]::Aes256Cbc,[Security.Cryptography.HashAlgorithmName]::SHA256,600000)
 [IO.File]::WriteAllText($target,$rsa.ExportEncryptedPkcs8PrivateKeyPem($plain,$parameters))
 Write-Output 'Encrypted backup created. Keep it offline and store the passphrase separately.'
}finally{if($pointer -ne [IntPtr]::Zero){[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)};$plain=$null;$password.Dispose();$rsa.Dispose()}
