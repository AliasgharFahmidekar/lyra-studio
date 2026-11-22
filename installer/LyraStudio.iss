; Lyra Studio one-step x64 installer.
; .NET is bundled in the self-contained application.
; WebView2 Evergreen Standalone is installed silently only when missing.

#define MyAppName "Lyra Studio"
#define MyAppVersion "0.5.0-preview.1"
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
function RuntimeInstalledAt(RootKey: Integer; KeyPath: string): Boolean;
var
  ProductVersion: string;
begin
  Result :=
    RegQueryStringValue(RootKey, KeyPath, 'pv', ProductVersion) and
    (ProductVersion <> '') and
    (ProductVersion <> '0.0.0.0');
end;

function IsWebView2Installed: Boolean;
begin
  { On 64-bit Windows the machine-wide client registration is under
    WOW6432Node; HKLM32 maps this path to that registry view. }
  Result :=
    RuntimeInstalledAt(HKLM32, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}') or
    RuntimeInstalledAt(HKCU64, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}') or
    RuntimeInstalledAt(HKCU32, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}');
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
