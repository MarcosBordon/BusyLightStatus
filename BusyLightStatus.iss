; BusyLightStatus installer (Inno Setup 6)
; Build order:
;   1. .\scripts\build-firmware.ps1 -Version 1.5.0 -UsbIds "0403:6001"
;   2. .\scripts\prepare-installer-assets.ps1
;   3. dotnet publish (output in .\publish)
;   4. ISCC.exe .\BusyLightStatus.iss
; All paths are relative to this file (project root).

#define AppName "BusyLightStatus"
#define AppVersion "1.5.0"
#define AppExe "BusyLightStatus.exe"

[Setup]
AppId={{8F3C2A41-6B7E-4D9A-9C55-2E1F0B7A4D21}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Marcos Bordon
AppPublisherURL=https://github.com/MarcosBordon/BusyLightStatus
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
; Per-user install by default: no administrator rights needed
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=Output
OutputBaseFilename=BusyLightStatus-Setup-{#AppVersion}
SetupIconFile=src\BusyLight.Tray\BusyLightStatus.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes

[Tasks]
Name: "autostart"; Description: "Start BusyLightStatus with Windows"
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[InstallDelete]
; Removes firmware from previous versions on upgrade
Type: files; Name: "{app}\firmware\*.bin"
Type: files; Name: "{app}\firmware\manifest.json"

[Files]
; --- Application ---
Source: "publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "src\BusyLight.Tray\BusyLightStatus.ico"; DestDir: "{app}"; Flags: ignoreversion
; Keeps the user's configuration on upgrades
Source: "src\BusyLight.Tray\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall

; --- Flashing tool (esptool, GPLv2, unmodified, separate program) ---
Source: "dist\installer-assets\tools\esptool\*"; DestDir: "{app}\tools\esptool"; Flags: ignoreversion recursesubdirs createallsubdirs

; --- Packaged firmware + manifest ---
Source: "dist\installer-assets\firmware\*"; DestDir: "{app}\firmware"; Flags: ignoreversion

; --- Licenses ---
Source: "LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon
Name: "{autostartup}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: autostart

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch BusyLightStatus"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "taskkill"; Parameters: "/im {#AppExe} /f"; Flags: runhidden; RunOnceId: "StopApp"
Filename: "taskkill"; Parameters: "/im esptool.exe /f"; Flags: runhidden; RunOnceId: "StopEsptool"
