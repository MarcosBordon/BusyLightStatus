#define AppName "BusyLightStatus"
#define AppVersion "1.4.0"
#define AppExe "BusyLightStatus.exe"

[Setup]
AppId={{8F3C2A41-6B7E-4D9A-9C55-2E1F0B7A4D21}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Marcos Bordon
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
; Per-user install by default: no administrator rights needed
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=Output
OutputBaseFilename=BusyLightStatus-Setup-{#AppVersion}
SetupIconFile=..\\BusyLightPilot\src\BusyLight.Tray\BusyLightStatus.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes

[Tasks]
Name: "autostart"; Description: "Start BusyLightStatus with Windows"
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "..\BusyLightPilot\publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\\BusyLightPilot\src\BusyLight.Tray\BusyLightStatus.ico"; DestDir: "{app}"; Flags: ignoreversion
; Keeps the user's configuration on upgrades
Source: "..\\BusyLightPilot\src\BusyLight.Tray\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon
Name: "{autostartup}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: autostart

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch BusyLightStatus"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "taskkill"; Parameters: "/im {#AppExe} /f"; Flags: runhidden; RunOnceId: "StopApp"