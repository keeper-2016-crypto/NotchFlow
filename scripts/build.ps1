param([string]$Dotnet = 'dotnet', [string]$ReleaseName = ('release-' + (Get-Date -Format 'yyyyMMdd-HHmmss')))
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
Set-Location $root
if ($ReleaseName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'ReleaseName must be a simple directory name.' }
$out = Join-Path $root "dist\$ReleaseName"
# Never overwrite or relocate an existing release, which may still be running.
if (Test-Path -LiteralPath $out) { throw "Release already exists: $out. Choose a new ReleaseName." }
function Invoke-Dotnet { & $Dotnet @args; if ($LASTEXITCODE -ne 0) { throw "dotnet failed: $LASTEXITCODE" } }
Invoke-Dotnet restore NotchFlow.slnx --configfile NotchFlow/NuGet.Config --disable-parallel '-m:1'
Invoke-Dotnet build NotchFlow.slnx -c Release --no-restore '-m:1' '-p:UseSharedCompilation=false'
Invoke-Dotnet NotchFlow.Tests/bin/Release/net10.0-windows10.0.22621.0/NotchFlow.Tests.dll
foreach ($profile in @('Portable','SingleFile')) {
    $destination = Join-Path $out $profile
    Invoke-Dotnet publish NotchFlow/NotchFlow.csproj -c Release "-p:PublishProfile=$profile" '-p:UseSharedCompilation=false' --configfile NotchFlow/NuGet.Config -o $destination
    Copy-Item NotchFlow/THIRD-PARTY-NOTICES.md,NotchFlow/NAudio-LICENSE.txt -Destination $destination
    Copy-Item NotchFlow/Assets/Fonts/OFL.txt -Destination (Join-Path $destination 'GEIST-OFL.txt')
    $sdkDirectory = Split-Path (Get-Command $Dotnet).Source
    foreach ($notice in @('LICENSE.txt','ThirdPartyNotices.txt')) {
        $noticePath = Join-Path $sdkDirectory $notice
        if (Test-Path $noticePath) { Copy-Item $noticePath (Join-Path $destination ('DOTNET-' + $notice)) }
    }
    $config = Join-Path $root ('.build\release-check-' + [guid]::NewGuid().ToString('N'))
    foreach ($mode in @('one','many','read','corrupt','recover')) {
        $arguments = @('--settings-check',$mode,('"' + $config + '"'))
        $process = Start-Process -FilePath (Join-Path $destination 'NotchFlow.exe') -ArgumentList $arguments -WindowStyle Hidden -PassThru
        if (!$process.WaitForExit(30000)) { $process.Kill(); throw "Diagnostic timeout: $profile/$mode" }
        if ($process.ExitCode -ne 0) { throw "Diagnostic failed: $profile/$mode; see $config" }
    }
    $checks = Join-Path $out "checks-$profile"
    New-Item -ItemType Directory -Path $checks | Out-Null
    Copy-Item (Join-Path $config 'check-*.txt') -Destination $checks
    Get-FileHash (Join-Path $destination 'NotchFlow.exe') -Algorithm SHA256 | Format-List | Out-File (Join-Path $destination 'SHA256.txt')
}
Copy-Item README.md -Destination $out
Write-Host "Verified releases: $out"



