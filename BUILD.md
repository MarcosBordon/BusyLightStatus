# Build and installer

This guide explains how to build BusyLightStatus as a self-contained Windows executable and how to package it with the Inno Setup installer.

## Requirements

| Tool | Purpose |
|---|---|
| .NET 8 SDK | Builds and publishes the tray app |
| Inno Setup 6 | Compiles the installer from `BusyLightStatus.iss` |
| PlatformIO (VS Code) | Only needed to flash the ATOM firmware |

The target PCs do **not** need .NET installed: the published executable includes the runtime.

## Project layout

```text
BusyLightPilot/
├── BusyLightStatus.iss          Inno Setup script
├── Output/                      Generated installer (not committed)
│   └── BusyLightStatus-Setup-1.4.0.exe
├── publish/                     Published app (not committed)
├── src/BusyLight.Tray/          .NET 8 tray app
└── firmware/atom-matrix/        PlatformIO firmware
```

Paths inside `BusyLightStatus.iss` are relative to the `.iss` file. If you move it, update the `Source:` and `SetupIconFile` paths.

## 1. Publish settings

`src/BusyLight.Tray/BusyLight.Tray.csproj` contains, inside `<PropertyGroup>`:

```xml
<Version>1.4.0</Version>
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
<SelfContained>true</SelfContained>
<PublishSingleFile>true</PublishSingleFile>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
```

## 2. Publish the app

From the repository root:

```powershell
dotnet publish .\src\BusyLight.Tray\BusyLight.Tray.csproj -c Release -o .\publish
```

The `publish` folder must contain:

| File | Description |
|---|---|
| `BusyLightStatus.exe` | Single-file app with the .NET runtime included (about 70 MB) |
| `BusyLightStatus.ico` | Tray icon, loaded from the app folder |
| `appsettings.json` | Tenant ID, Client ID and polling settings |

Before publishing, make sure `src/BusyLight.Tray/appsettings.json` has the right values for the target tenant. Without a valid Client ID, the app runs in Manual Use mode only.

## 3. Build the installer

```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" .\BusyLightStatus.iss
```

Result: `Output\BusyLightStatus-Setup-1.4.0.exe`

## 4. What the installer does

| Feature | Details |
|---|---|
| Install mode | **Current user** by default, no administrator rights needed. Optional **all users** install (requires administrator) |
| Install folder | Current user: `%LOCALAPPDATA%\Programs\BusyLightStatus`. All users: `C:\Program Files\BusyLightStatus` |
| Shortcuts | Start menu. Desktop shortcut is optional |
| Auto start | Optional: starts BusyLightStatus with Windows |
| Upgrades | Keeps the existing `appsettings.json` |
| Uninstall | From **Settings > Apps**. Closes the app before removing it |
| User settings | Stored in `%LOCALAPPDATA%\BusyLightStatus\usersettings.json`, separate from the install folder |

### Silent install

For deployment tools (for example Intune):

```powershell
BusyLightStatus-Setup-1.4.0.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER
```

Use `/ALLUSERS` instead of `/CURRENTUSER` for a machine-wide install (requires administrator).

## 5. USB driver

Windows 10 and 11 usually install the ATOM's FTDI USB driver automatically. If the ATOM does not appear as a COM port, install the FTDI VCP driver from the FTDI website, as linked in the M5Stack ATOM Matrix documentation.

The installer can optionally include the driver (all-users install only). FTDI driver files are **not** stored in this repository: download them into `drivers/ftdi/` before compiling the installer, and review the FTDI license before redistributing them.

## 6. Releasing a new version

Update the version number in three places:

| File | Setting |
|---|---|
| `src/BusyLight.Tray/BusyLight.Tray.csproj` | `<Version>` |
| `BusyLightStatus.iss` | `#define AppVersion` |
| `firmware/atom-matrix/src/main.cpp` | `FW_VERSION` (only if the firmware changed) |

Then publish (step 2) and build the installer (step 3).

## Security notes

- **Do not publish the installer or the `publish` folder in public locations** (for example GitHub Releases) when they include an `appsettings.json` with real tenant values. Build a separate package with `appsettings.example.json` for public distribution.
- **Code signing:** unsigned executables trigger Windows SmartScreen warnings. For corporate distribution, sign `BusyLightStatus.exe` and the installer with a code-signing certificate.
- **Corporate policies:** some environments block executables in user folders (AppLocker, Intune). Test on a standard managed PC before rolling out.
