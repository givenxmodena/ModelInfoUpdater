; ModelInfoUpdater Inno Setup Installer Script
; This script creates an installer for the initial deployment of the Revit add-in.
; Subsequent updates are handled by Velopack.

#define MyAppName "Model Info Updater"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Modena AEC"
#define MyAppURL "https://github.com/givenxmodena/ModelInfoUpdater"

[Setup]
AppId={{B8E7F3C1-5D2A-4B9E-8F1C-6A3D7E9B0C2F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=
OutputDir=..\releases
OutputBaseFilename=ModelInfoUpdater-Setup-{#MyAppVersion}
SetupIconFile=
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Revit 2026 files (.NET 8.0)
Source: "..\build-output\net8.0-windows\ModelInfoUpdater.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026"; Flags: ignoreversion; Check: IsRevit2026Installed
Source: "..\build-output\net8.0-windows\ModelInfoUpdater.Updater.exe"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026"; Flags: ignoreversion; Check: IsRevit2026Installed
Source: "..\ModelInfoUpdater.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026"; Flags: ignoreversion; Check: IsRevit2026Installed

; Revit 2024 files (.NET Framework 4.8)
Source: "..\build-output\net48\ModelInfoUpdater.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024"; Flags: ignoreversion; Check: IsRevit2024Installed
Source: "..\ModelInfoUpdater.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024"; Flags: ignoreversion; Check: IsRevit2024Installed

; Copy Velopack dependencies for 2026
Source: "..\build-output\net8.0-windows\Velopack.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026"; Flags: ignoreversion skipifsourcedoesntexist; Check: IsRevit2026Installed

; Copy Velopack dependencies for 2024
Source: "..\build-output\net48\Velopack.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024"; Flags: ignoreversion skipifsourcedoesntexist; Check: IsRevit2024Installed

[Code]
function IsRevit2026Installed: Boolean;
begin
  Result := DirExists(ExpandConstant('{pf}\Autodesk\Revit 2026'));
end;

function IsRevit2024Installed: Boolean;
begin
  Result := DirExists(ExpandConstant('{pf}\Autodesk\Revit 2024'));
end;

function InitializeSetup: Boolean;
var
  Revit2024: Boolean;
  Revit2026: Boolean;
begin
  Revit2024 := IsRevit2024Installed;
  Revit2026 := IsRevit2026Installed;
  
  if not Revit2024 and not Revit2026 then
  begin
    MsgBox('No supported version of Revit was found.' + #13#10 + #13#10 +
           'This add-in requires Revit 2024 or Revit 2026.' + #13#10 +
           'Please install Revit first.', mbError, MB_OK);
    Result := False;
  end
  else
  begin
    Result := True;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  InstalledVersions: String;
begin
  if CurStep = ssPostInstall then
  begin
    InstalledVersions := '';
    if IsRevit2026Installed then
      InstalledVersions := InstalledVersions + '- Revit 2026' + #13#10;
    if IsRevit2024Installed then
      InstalledVersions := InstalledVersions + '- Revit 2024' + #13#10;
    
    MsgBox('Model Info Updater has been installed for:' + #13#10 + #13#10 +
           InstalledVersions + #13#10 +
           'Start Revit and look for the TESTER tab to use the add-in.',
           mbInformation, MB_OK);
  end;
end;

[UninstallDelete]
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2026\ModelInfoUpdater.dll"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2026\ModelInfoUpdater.Updater.exe"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2026\ModelInfoUpdater.addin"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2026\Velopack.dll"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2024\ModelInfoUpdater.dll"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2024\ModelInfoUpdater.addin"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2024\Velopack.dll"

