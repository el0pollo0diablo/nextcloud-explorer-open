#define AppName "Nextcloud Explorer Open"
#ifndef AppVersion
  #define AppVersion "0.3.0"
#endif
#define AppPublisher "el0pollo0diablo"
#define AppUrl "https://github.com/el0pollo0diablo/nextcloud-explorer-open"
#define HostName "io.github.el0pollo0diablo.nextcloud_explorer_open"
#define ExtensionId "nextcloud-explorer-open@covasala.org"

[Setup]
AppId={{CF15F4E2-8A39-4C14-9687-B7852830C0D8}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
DefaultDirName={localappdata}\Programs\Nextcloud Explorer Open
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
OutputDir=..\dist\releases
OutputBaseFilename=nextcloud-explorer-open-setup-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\NextcloudExplorerHost.exe
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Windows helper setup
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}
VersionInfoCopyright=Copyright (c) 2026 {#AppPublisher}
SetupLogging=yes

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\dist\installer\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
Root: HKCU; Subkey: "Software\Mozilla\NativeMessagingHosts\{#HostName}"; ValueType: string; ValueName: ""; ValueData: "{app}\{#HostName}.json"; Flags: uninsdeletekey

[Icons]
Name: "{group}\Nextcloud Explorer Open einrichten"; Filename: "{app}\NextcloudExplorerHost.exe"; Parameters: "--configure"; WorkingDir: "{app}"
Name: "{group}\Nextcloud Explorer Open deinstallieren"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\NextcloudExplorerHost.exe"; Parameters: "--configure"; Description: "Sichere Nextcloud-Verbindung jetzt einrichten"; Flags: postinstall nowait skipifsilent

[UninstallRun]
Filename: "{app}\NextcloudExplorerHost.exe"; Parameters: "--remove-user-data"; Flags: runhidden waituntilterminated skipifdoesntexist; RunOnceId: "RemoveUserData"

[UninstallDelete]
Type: files; Name: "{app}\{#HostName}.json"
Type: dirifempty; Name: "{app}"

[Code]
function JsonEscape(Value: String): String;
begin
  Result := Value;
  StringChangeEx(Result, '\', '\\', True);
  StringChangeEx(Result, '"', '\"', True);
end;

procedure WriteNativeManifest;
var
  Lines: TArrayOfString;
  ManifestPath: String;
  ExecutablePath: String;
begin
  ManifestPath := ExpandConstant('{app}\{#HostName}.json');
  ExecutablePath := JsonEscape(ExpandConstant('{app}\NextcloudExplorerHost.exe'));

  SetArrayLength(Lines, 8);
  Lines[0] := '{';
  Lines[1] := '  "name": "{#HostName}",';
  Lines[2] := '  "description": "Opens Nextcloud WebDAV folders in Windows Explorer.",';
  Lines[3] := '  "path": "' + ExecutablePath + '",';
  Lines[4] := '  "type": "stdio",';
  Lines[5] := '  "allowed_extensions": ["{#ExtensionId}"]';
  Lines[6] := '}';
  Lines[7] := '';

  if not SaveStringsToUTF8FileWithoutBOM(ManifestPath, Lines, False) then
    RaiseException('Das Firefox Native-Messaging-Manifest konnte nicht geschrieben werden.');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    WriteNativeManifest;
end;
