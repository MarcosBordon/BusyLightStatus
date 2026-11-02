# Build and installer

This guide explains how to build BusyLightStatus 1.5.0 as a self-contained Windows executable, package the production ATOM firmware and esptool, and create the Inno Setup installer.

## Requirements

| Tool | Purpose |
|---|---|
| .NET 8 SDK | Builds and publishes the Windows tray app |
| Inno Setup 6 | Compiles `BusyLightStatus.iss` into the Windows installer |
| PlatformIO (VS Code) | Developer-only: builds the ATOM firmware source |
| PowerShell | Runs the firmware and installer packaging scripts |

Target PCs do **not** need .NET, VS Code, PlatformIO, Python or Git installed. The `.NET` runtime, production firmware and the standalone esptool executable are packaged for end users.

## Project layout

```text
BusyLightPilot/
├── BusyLightStatus.iss               Inno Setup script
├── BUILD.md                           This build guide
├── Output/                            Generated installer (not committed)
│   └── BusyLightStatus-Setup-1.5.0.exe
├── publish/                           Published app (not committed)
├── scripts/
│   ├── build-firmware.ps1
│   └── prepare-installer-assets.ps1
├── dist/
│   ├── firmware/                      Production firmware package
│   │   ├── busylight-atom-matrix-1.5.0-merged.bin
│   │   └── manifest.json
│   └── installer-assets/              Generated staging area (not committed)
├── src/BusyLight.Tray/                .NET 8 tray app
└── firmware/atom-matrix/              PlatformIO firmware source
```

Paths inside `BusyLightStatus.iss` are relative to the `.iss` file. If it is moved, update `Source:` and `SetupIconFile` paths.

## 1. Publish settings

`src/BusyLight.Tray/BusyLight.Tray.csproj` contains, inside `<PropertyGroup>`:

```xml
<Version>1.5.0</Version>
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
<SelfContained>true</SelfContained>
<PublishSingleFile>true</PublishSingleFile>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
```

## 2. Firmware package

BusyLightStatus 1.5.0 includes a production merged firmware image that can be installed by the application itself.

The authoritative package is stored in:

```text
dist/firmware/
├── busylight-atom-matrix-1.5.0-merged.bin
└── manifest.json
```

### manifest.json

`manifest.json` is the source of truth for firmware installation. BusyLightStatus reads the firmware version, target chip, minimum flash size, file name, write offset, baud rate, expected size and SHA-256 directly from the manifest.

Current package:

```json
{
  "name": "BusyLightStatus",
  "version": "1.5.0",
  "chip": "ESP32",
  "minFlashMB": 4,
  "file": "busylight-atom-matrix-1.5.0-merged.bin",
  "offset": "0x0",
  "baud": 115200,
  "size": 396720,
  "usbIds": [
    "0403:6001"
  ]
}
```

The production manifest also contains the generated SHA-256, build date and source Git commit.

## 3. Prepare installer assets

Run from the repository root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\prepare-installer-assets.ps1
```

The script:

1. Obtains or reuses the approved **esptool 4.8.1** Windows package.
2. Validates the packaged `esptool.exe` version.
3. Includes the esptool GPLv2 license and source/reference files.
4. Copies the production merged firmware.
5. Validates the firmware SHA-256 checksum.
6. Creates the staging structure consumed by the application and installer.

Generated structure:

```text
dist/installer-assets/
├── firmware/
│   ├── busylight-atom-matrix-1.5.0-merged.bin
│   └── manifest.json
└── tools/
    └── esptool/
        ├── esptool.exe
        ├── LICENSE
        ├── LICENSE.txt
        ├── README.md
        └── SOURCE.txt
```

`dist/installer-assets/` is generated locally and must remain excluded from Git with `.gitignore`.

If corporate PowerShell execution policy blocks unsigned local scripts, use the one-process command above rather than weakening the machine-wide execution policy.

## 4. Publish the app

Remove an old publish directory to avoid mixing versions:

```powershell
Remove-Item .\publish -Recurse -Force -ErrorAction SilentlyContinue
```

Publish:

```powershell
dotnet publish .\src\BusyLight.Tray\BusyLight.Tray.csproj `
  -c Release `
  -o .\publish
```

Copy the generated firmware and tool assets:

```powershell
Copy-Item .\dist\installer-assets\tools `
  .\publish\tools `
  -Recurse -Force

Copy-Item .\dist\installer-assets\firmware `
  .\publish\firmware `
  -Recurse -Force
```

The publish folder must contain at least:

```text
publish/
├── BusyLightStatus.exe
├── BusyLightStatus.ico
├── appsettings.json
├── firmware/
│   ├── busylight-atom-matrix-1.5.0-merged.bin
│   └── manifest.json
└── tools/
    └── esptool/
        └── esptool.exe
```

The exact self-contained executable size varies with the .NET runtime and compression.

Before corporate publishing, make sure `appsettings.json` has the correct Tenant ID and Client ID. A Client Secret is **not required** and must not be embedded in a desktop/public-client build.

## 5. Build the installer

Compile the root-level Inno Setup script:

```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" `
  .\BusyLightStatus.iss
```

Expected result:

```text
Output\BusyLightStatus-Setup-1.5.0.exe
```

Verify:

```powershell
Get-Item .\Output\BusyLightStatus-Setup-1.5.0.exe |
  Select-Object Name, Length, LastWriteTime
```

## 6. What the installer does

| Feature | Details |
|---|---|
| Install mode | **Current user** by default, no administrator rights needed. Optional all-users mode requires administrator rights |
| Install folder | Current user: `%LOCALAPPDATA%\Programs\BusyLightStatus`; all users: `C:\Program Files\BusyLightStatus` |
| Application | Installs the self-contained BusyLightStatus executable |
| Firmware | Installs the merged firmware and `manifest.json` under the application folder |
| Firmware tool | Installs esptool 4.8.1 and its required licensing information |
| Shortcuts | Start menu; desktop shortcut optional |
| Auto start | Optional: starts BusyLightStatus with Windows |
| Upgrades | Keeps the existing `appsettings.json` |
| Uninstall | Available from **Settings > Apps**; closes BusyLightStatus and esptool before removing files |
| User settings | Stored separately in `%LOCALAPPDATA%\BusyLightStatus\usersettings.json` |

### Silent install

For deployment tools such as Microsoft Intune:

```powershell
BusyLightStatus-Setup-1.5.0.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER
```

Use `/ALLUSERS` instead of `/CURRENTUSER` for a machine-wide installation. This requires administrator rights.

## 7. Integrated device setup and firmware installation

The installed application provides:

```text
Firmware
├── Check device compatibility...
└── Install firmware...
```

### Compatibility check

The read-only compatibility check:

1. Releases the application's serial port.
2. Runs packaged esptool commands against candidate COM ports.
3. Identifies the ESP32 chip and revision.
4. Reads the MAC address.
5. Reads flash manufacturer/device information and flash size.
6. Confirms compatibility with the packaged manifest.
7. Returns the serial port to BusyLightStatus.

No `write_flash` operation occurs during compatibility checking.

### Firmware installation safeguards

Before `write_flash`, BusyLightStatus:

1. Revalidates the device immediately before flashing.
2. Confirms compatibility with the same manifest.
3. Confirms that the MAC address matches the device shown in the confirmation dialog.
4. Verifies firmware file size when supplied by the manifest.
5. Verifies the packaged SHA-256 checksum.
6. Uses the **offset and baud rate from the manifest**, not hard-coded values.

Current production values:

```text
Offset: 0x0
Baud: 115200
```

The merged firmware is written with esptool and the ATOM is hard-reset afterwards.

### Post-flash verification

A successful esptool exit code is not sufficient on its own. BusyLightStatus reconnects over USB serial and sends:

```json
{"cmd":"identify"}
```

Installation is reported as successful only when the ATOM responds with both:

```text
device = BUSYLIGHT-ATOM
firmware = 1.5.0
```

The tray application then reconnects normally and restores the previous automatic/manual state.

## 8. USB driver

Windows 10 and 11 commonly install the ATOM's FTDI USB driver automatically. If the ATOM does not appear as a COM port, install the FTDI VCP driver referenced by the M5Stack ATOM Matrix documentation.

An optional all-users installer may include the driver, but driver installation requires administrator rights. FTDI driver binaries should not be stored in this public repository unless their redistribution terms have been reviewed.

## 9. Entra ID configuration for Automatic Teams mode

BusyLightStatus is a native Windows desktop/public client application.

Required Microsoft Entra ID configuration:

```text
Platform: Mobile and desktop applications
Redirect URI: http://localhost
Allow public client flows: Yes
Microsoft Graph delegated permission: Presence.Read
Client Secret: Not required
```

Do not configure the redirect URI exclusively as a Single-Page Application (SPA). The desktop MSAL flow requires the Mobile and desktop applications platform.

## 10. Releasing a new version

Update the version in the relevant release sources:

| File | Setting |
|---|---|
| `src/BusyLight.Tray/BusyLight.Tray.csproj` | `<Version>` |
| `BusyLightStatus.iss` | `#define AppVersion` |
| `dist/firmware/manifest.json` | `version`, firmware file, size and SHA-256 |
| `firmware/atom-matrix/src/main.cpp` | `FW_VERSION` when firmware changes |

For a firmware-changing release:

1. Build the firmware.
2. Generate the merged binary.
3. Update `dist/firmware/manifest.json` including SHA-256.
4. Run `prepare-installer-assets.ps1`.
5. Publish the .NET app.
6. Copy installer assets into `publish/`.
7. Compile `BusyLightStatus.iss`.
8. Test Teams authentication, compatibility checking, flashing and post-flash verification on the final installer.
9. Merge the release branch and tag the stable version.

## Security notes

- Never commit or publicly distribute a real `appsettings.json` containing tenant-specific configuration.
- Never embed a Client Secret in BusyLightStatus. A desktop/public client does not need one.
- `publish/`, `Output/` and `dist/installer-assets/` are generated outputs and should stay out of Git.
- A corporate installer containing tenant-specific configuration should be distributed internally (for example through Microsoft Intune), not through a public GitHub Release.
- Unsigned executables may trigger Windows SmartScreen warnings. Corporate distribution should use a trusted code-signing certificate.
- Some corporate environments block executables installed under user profiles with AppLocker or Intune policies. Test the final installer on a representative managed PC.
