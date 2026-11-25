#ifndef ReleaseVersion
 #define ReleaseVersion "0.3.8"
#endif
#ifndef PayloadDir
 #define PayloadDir "..\dist\release-0_3_8\Portable"
#endif
[Setup]
AppId={{F936FCEB-1C54-40C5-9E75-B5492A088745}
AppName=NotchFlow
AppVersion={#ReleaseVersion}
DefaultDirName={localappdata}\Programs\NotchFlow
DisableDirPage=no
DefaultGroupName=NotchFlow
PrivilegesRequired=lowest
UsePreviousAppDir=yes
UsePreviousPrivileges=yes
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.22621
OutputDir=..\Installer
OutputBaseFilename=NotchFlow-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\NotchFlow\Assets\NotchFlow.ico
UninstallDisplayIcon={app}\NotchFlow.exe
CloseApplications=yes
CloseApplicationsFilter=*.exe
RestartApplications=no
[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
[Tasks]
Name: "desktopicon"; Description: "РЇСЂР»С‹Рє РЅР° СЂР°Р±РѕС‡РµРј СЃС‚РѕР»Рµ"; Flags: unchecked
[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\NotchFlow"; Filename: "{app}\NotchFlow.exe"
Name: "{autodesktop}\NotchFlow"; Filename: "{app}\NotchFlow.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\NotchFlow.exe"; Description: "Р—Р°РїСѓСЃС‚РёС‚СЊ NotchFlow"; Flags: nowait postinstall skipifsilent; Check: not IsUpdateLaunch
Filename: "{app}\NotchFlow.exe"; Flags: nowait; Check: IsUpdateLaunch
[Code]
function IsUpdateLaunch: Boolean;
begin
  Result := ExpandConstant('{param:UPDATEAPP|0}') <> '0';
end;
procedure CurStepChanged(CurStep: TSetupStep);
var OldValue: String;
begin
  if CurStep = ssPostInstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'NotchFlow', OldValue) then
      RegWriteStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'NotchFlow', '"' + ExpandConstant('{app}\NotchFlow.exe') + '"');
end;
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Value: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'NotchFlow', Value) then
      if CompareText(Value, '"' + ExpandConstant('{app}\NotchFlow.exe') + '"') = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'NotchFlow');
end;

