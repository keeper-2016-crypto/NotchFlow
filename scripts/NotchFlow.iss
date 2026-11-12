[Setup]
AppId={{F936FCEB-1C54-40C5-9E75-B5492A088745}
AppName=NotchFlow
AppVersion=0.3.4
DefaultDirName={localappdata}\Programs\NotchFlow
DisableDirPage=no
DefaultGroupName=NotchFlow
PrivilegesRequired=lowest
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
Name: "desktopicon"; Description: "Ярлык на рабочем столе"; Flags: unchecked
[Files]
Source: "..\dist\release-0_3_4\Portable\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\NotchFlow"; Filename: "{app}\NotchFlow.exe"
Name: "{autodesktop}\NotchFlow"; Filename: "{app}\NotchFlow.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\NotchFlow.exe"; Description: "Запустить NotchFlow"; Flags: nowait postinstall skipifsilent
[Code]
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
