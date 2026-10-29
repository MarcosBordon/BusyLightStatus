# BusyLightStatus

Busy light for Microsoft Teams built on an **M5Stack ATOM Matrix** (ESP32, 5×5 RGB LEDs) and a **Windows tray app in .NET 8**. It helps people in meetings avoid interruptions.

<p align="center">
  <img src="busylightstatus-v2.gif" alt="BUSY LIGHT STATUS on the ATOM Matrix" width="240">
</p>

## Features

- **Automatic (Teams):** reads your own presence through Microsoft Graph and shows it on the ATOM.
- **Manual Use:** six statuses (Available, Busy, In a meeting, Do not disturb, Presenting, Away). Enabled automatically when Entra ID is not configured or not authorized.
- **Custom color:** any RGB color with brightness from 10 to 100.
- **Text message:** up to 15 characters, shown letter by letter (500 ms each), with a fixed color or random colors.
- **Watchdog:** if the app stops sending data for 60 s, the ATOM blinks blue.

## Hardware

| Item | Details |
|---|---|
| Board | M5Stack ATOM Matrix (ESP32-PICO-D4, 25 WS2812C LEDs on GPIO 27) |
| Cable | USB-C with data support |
| Extra wiring | None |

M5STACK page: `https://docs.m5stack.com/en/core/Atom-Matrix`

See [CONNECTIONS.md](CONNECTIONS.md) and [DIAGRAM.md](DIAGRAM.md).

## Repository structure

```text
firmware/atom-matrix/   PlatformIO firmware (FW 1.3.1)
src/BusyLight.Tray/     .NET 8 tray app
tests/                  PowerShell serial tests
```

## 1. Flash the firmware

Requirements: VS Code with PlatformIO.

```powershell
cd firmware\atom-matrix
pio run
pio run --target upload
```

Set `upload_port` and `monitor_port` in `platformio.ini` to your COM port.

## 2. Configure the app

Copy `src/BusyLight.Tray/appsettings.example.json` to `appsettings.json` in the same folder.

| Setting | Description |
|---|---|
| `TenantId` | Directory (tenant) ID |
| `ClientId` | Application (client) ID. Leave the placeholder to run in Manual Use only |
| `PollSeconds` | Presence polling interval (10-30 s) |
| `SerialBaudRate` | 115200 |
| `Brightness` | Brightness in automatic mode (10-100) |

`appsettings.json` is excluded by `.gitignore`. Never commit it.

### Entra ID app registration (for automatic mode)

- Single-tenant, public client (mobile and desktop), redirect URI `http://localhost`
- Microsoft Graph delegated permission `Presence.Read`, with admin consent if required by the tenant
- No client secret or certificate

## 3. Build and run

Requirements: .NET 8 SDK.

```powershell
dotnet build .\BusyLightPilot.sln -c Release
dotnet run --project .\src\BusyLight.Tray\BusyLight.Tray.csproj
```

The app finds the ATOM automatically on any COM port.

## Serial protocol

One JSON object per line, 115200 baud, 8N1.

| Command | Example |
|---|---|
| `identify` | `{"cmd":"identify"}` |
| `ping` | `{"Cmd":"ping"}` |
| `setStatus` (full) | `{"Cmd":"setStatus","Color":"#FF0000","Effect":"pulse","Brightness":95}` |
| `setStatus` (corners off) | `{"Cmd":"setStatus","Pattern":"rounded","Color":"#00FF00","Effect":"solid","Brightness":95}` |
| `setStatus` (traffic light) | `{"Cmd":"setStatus","Pattern":"lights","Top":"#FF0000","Middle":"#FFC000","Bottom":"#00FF00","Effect":"solid","Brightness":80}` |
| `setText` | `{"Cmd":"setText","Text":"IN A MEETING","Color":"#00A0FF","Random":false,"Brightness":80}` |

Effects: `solid`, `pulse`, `doublepulse`, `blink`.

## Status colors

| Status | Color | Effect |
|---|---|---|
| Available | Green | Solid |
| Busy | Red | Solid |
| In a meeting / In a call | Red | Pulse |
| Do not disturb | Red | Double pulse |
| Presenting | Magenta | Pulse |
| Away / Be right back | Yellow | Solid |
| No data for 60 s | Blue | Blink |

## Tests

Close the app first, so COM3 is free.

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\test_regression.ps1
```

| Script | Checks |
|---|---|
| `test_regression.ps1` | Identify and the six full-matrix statuses |
| `test_lights_ping.ps1` | Traffic-light pattern and watchdog renewal |
| `test_text.ps1` | Text, Ñ, accents, 15-character limit |
| `test_rounded.ps1` | Manual statuses with corners off |

## Privacy and security

- Reads only the signed-in user's own presence.
- No credentials are stored on the device.
- No presence history is recorded.

## License

MIT. See [LICENSE](LICENSE).