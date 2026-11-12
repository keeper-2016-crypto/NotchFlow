param([string]$Dotnet = 'dotnet', [string]$ReleaseName = ('test-' + (Get-Date -Format 'yyyyMMdd-HHmmss')))
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
Set-Location $root
if ($ReleaseName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Use a simple release directory name.' }
$out = Join-Path $root "dist\$ReleaseName"
if (Test-Path -LiteralPath $out) { throw "Existing release will not be overwritten: $out" }
$sdk = (Get-Command $Dotnet).Source
function Invoke-Dotnet { & $sdk @args; if ($LASTEXITCODE -ne 0) { throw "dotnet failed: $LASTEXITCODE" } }
Invoke-Dotnet restore NotchFlow.slnx --configfile NotchFlow/NuGet.Config --disable-parallel '-m:1'
Invoke-Dotnet build NotchFlow.slnx -c Release --no-restore '-m:1' '-p:UseSharedCompilation=false'
Invoke-Dotnet NotchFlow.Tests/bin/Release/net10.0-windows10.0.22621.0/NotchFlow.Tests.dll
# Stage A only: framework-dependent Release build. No publish profiles or installer tools.
Invoke-Dotnet build NotchFlow/NotchFlow.csproj -c Release --no-restore '-m:1' '-p:UseSharedCompilation=false' -o $out
Copy-Item NotchFlow/THIRD-PARTY-NOTICES.md,NotchFlow/NAudio-LICENSE.txt -Destination $out
Copy-Item NotchFlow/Assets/Fonts/OFL.txt -Destination (Join-Path $out 'GEIST-OFL.txt')
$runtime = Split-Path $sdk
# The launcher uses the SDK already present on this test machine, without modifying system settings.
$launcher = @"
@echo off
setlocal
set "DOTNET_ROOT=$runtime"
set "DOTNET_ROOT_X64=$runtime"
if not exist "%DOTNET_ROOT%\dotnet.exe" (
  echo Test runtime not found. Rebuild using scripts\build-test.ps1 -Dotnet PATH_TO_DOTNET.
  pause
  exit /b 1
)
start "" "%~dp0NotchFlow.exe" %*
"@
Set-Content -LiteralPath (Join-Path $out 'Launch-NotchFlow.cmd') -Value $launcher -Encoding ascii
if (Test-Path -LiteralPath docs/STAGE3-TESTING.md) { Copy-Item docs/STAGE3-TESTING.md -Destination $out }
Get-FileHash (Join-Path $out 'NotchFlow.exe'),(Join-Path $out 'NotchFlow.dll') -Algorithm SHA256 | Format-List | Out-File (Join-Path $out 'SHA256.txt')
Write-Host "Test Release ready: $out\Launch-NotchFlow.cmd"
