; Lyra Studio one-step x64 installer.
; .NET is bundled in the self-contained application.
; WebView2 Evergreen Standalone is installed silently only when missing.

#define MyAppName "Lyra Studio"
#define MyAppVersion "0.4.0"
#define MyAppPublisher "Lyra"
#define MyAppExeName "LyraStudio.exe"

[Setup]
AppId={{B1C56C36-3E1A-4B44-A1D3-43B86BDAA3E2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Lyra Studio
DefaultGroupName=Lyra Studio
OutputDir=..\artifacts\installer
OutputBaseFilename=LyraStudio-Setup
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\artifacts\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\artifacts\prerequisites\MicrosoftEdgeWebView2RuntimeInstallerX64.exe"; Flags: dontcopy

[Icons]
Name: "{group}\Lyra Studio"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Lyra Studio"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Lyra Studio"; Flags: postinstall nowait skipifsilent

[Code]
function WebView2InstalledInRoot(RootKey: Integer): Boolean;
var
  SubKeys: TArrayOfString;
  I: Integer;
  KeyName: string;
  DisplayName: string;
  ProductVersion: string;
begin
  Result := False;
  if not RegGetSubkeyNames(RootKey, 'SOFTWARE\Microsoft\EdgeUpdate\Clients', SubKeys) then
    Exit;
  for I := 0 to GetArrayLength(SubKeys) - 1 do
  begin
    KeyName := 'SOFTWARE\Microsoft\EdgeUpdate\Clients\' + SubKeys[I];
    if RegQueryStringValue(RootKey, KeyName, 'name', DisplayName) and
       (CompareText(DisplayName, 'Microsoft Edge WebView2 Runtime') = 0) and
       RegQueryStringValue(RootKey, KeyName, 'pv', ProductVersion) and
       (ProductVersion <> '') and (ProductVersion <> '0.0.0.0') then
    begin
      Result := True;
      Exit;
    end;
  end;
end;

function IsWebView2Installed: Boolean;
begin
  Result :=
    WebView2InstalledInRoot(HKLM64) or
    WebView2InstalledInRoot(HKCU64) or
    WebView2InstalledInRoot(HKLM32) or
    WebView2InstalledInRoot(HKCU32);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
  InstallerPath: string;
begin
  Result := '';
  if IsWebView2Installed then
    Exit;

  ExtractTemporaryFile('MicrosoftEdgeWebView2RuntimeInstallerX64.exe');
  InstallerPath := ExpandConstant('{tmp}\MicrosoftEdgeWebView2RuntimeInstallerX64.exe');

  if not Exec(InstallerPath, '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := 'WebView2 Runtime could not be started. Lyra Studio cannot start without its bundled preview engine.';
    Exit;
  end;

  if (ResultCode <> 0) and (ResultCode <> 3010) then
    Result := 'WebView2 Runtime installation failed with exit code ' + IntToStr(ResultCode) + '.';
end;
