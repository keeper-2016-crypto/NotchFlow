param([string]$Dotnet='dotnet')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
Set-Location $root
foreach($build in @(@{Version='0.3.6';Folder='release-0_3_6/Portable';Local='false';Name='NotchFlow-0.3.6-Setup';Output='Installer'},@{Version='0.3.6';Folder='update-e2e/0.3.6';Local='true';Name='NotchFlow-0.3.6-LOCAL-TEST-Setup';Output='dist/update-e2e'},@{Version='0.3.7';Folder='update-e2e/0.3.7';Local='true';Name='NotchFlow-0.3.7-LOCAL-TEST-Setup';Output='dist/update-e2e'})){
 $payload=Join-Path $root ('dist/'+$build.Folder)
 & $Dotnet publish NotchFlow/NotchFlow.csproj -c Release -r win-x64 --self-contained true -p:PublishProfile=Portable "-p:Version=$($build.Version)" "-p:LocalUpdateTest=$($build.Local)" -o $payload '-bl:.build/036-package-{}.binlog'
 if($LASTEXITCODE -ne 0){throw 'Publish failed'}
 Copy-Item NotchFlow/THIRD-PARTY-NOTICES.md,NotchFlow/NAudio-LICENSE.txt $payload
 Copy-Item NotchFlow/Assets/Fonts/OFL.txt (Join-Path $payload GEIST-OFL.txt)
 $sdk=Split-Path (Get-Command $Dotnet).Source
 foreach($name in @('LICENSE.txt','ThirdPartyNotices.txt')){if(Test-Path (Join-Path $sdk $name)){Copy-Item (Join-Path $sdk $name) (Join-Path $payload ('DOTNET-'+$name))}}
 $output=Join-Path $root $build.Output
 & work/inno/portable/tools/ISCC.exe "/DReleaseVersion=$($build.Version)" "/DPayloadDir=$payload" "/O$output" "/F$($build.Name)" scripts/NotchFlow.iss
 if($LASTEXITCODE -ne 0){throw 'Installer compile failed'}
}
