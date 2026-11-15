#Requires -Version 7.0
param([string]$KeyDirectory=(Join-Path $env:LOCALAPPDATA 'NotchFlowReleaseKeys'))
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot))
$directory=[IO.Path]::GetFullPath($KeyDirectory)
if($directory.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or $directory -eq $root){throw 'Private keys must be outside the repository.'}
$private=Join-Path $directory 'update-signing-key.dpapi'
if(Test-Path $private){throw 'A key already exists. Never rotate the embedded key accidentally.'}
New-Item -ItemType Directory -Force $directory | Out-Null
$identity=[Security.Principal.WindowsIdentity]::GetCurrent().User
$acl=[Security.AccessControl.DirectorySecurity]::new();$acl.SetAccessRuleProtection($true,$false);$acl.SetOwner($identity)
$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($identity,'FullControl','ContainerInherit,ObjectInherit','None','Allow'))
Set-Acl -LiteralPath $directory -AclObject $acl
$rsa=[Security.Cryptography.RSA]::Create(3072)
try {
 $bytes=$rsa.ExportPkcs8PrivateKey()
 try {$protected=[Security.Cryptography.ProtectedData]::Protect($bytes,$null,[Security.Cryptography.DataProtectionScope]::CurrentUser);[IO.File]::WriteAllBytes($private,$protected)}
 finally{[Security.Cryptography.CryptographicOperations]::ZeroMemory($bytes)}
 $public=Join-Path $root 'NotchFlow/Assets/UpdatePublicKey.pem'
 [IO.File]::WriteAllText($public,$rsa.ExportSubjectPublicKeyInfoPem(),[Text.UTF8Encoding]::new($false))
 Write-Output "Developer-only encrypted key: $private"
 Write-Output "Public verification key: $public"
}finally{$rsa.Dispose()}
