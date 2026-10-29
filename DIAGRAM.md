# Technical diagram

```mermaid
flowchart LR
  T[Microsoft Teams] --> G[Microsoft Graph<br/>GET /me/presence]
  G -->|HTTPS + OAuth 2.0<br/>delegated Presence.Read| A[Windows tray app .NET 8<br/>MSAL]
  M[Manual Use<br/>status, custom color, text] --> A
  A -->|JSON over USB serial<br/>115200 8N1| U[USB-C / COM port]
  U --> E[M5Stack ATOM Matrix<br/>ESP32-PICO-D4, FW 1.3.1]
  E --> L[25 RGB LEDs<br/>GPIO 27]
  E --> W[Watchdog 60 s<br/>blinking blue]
```

## Flow

1. **Automatic:** the app gets a delegated token with MSAL and polls `GET /me/presence`.
2. **Manual:** the user picks a status, a custom color or a text message from the tray menu.
3. The app converts the choice into a JSON command and sends it over USB.
4. In manual mode, the app sends `ping` every 20 s to keep the watchdog from firing.
5. The firmware draws the pattern and runs the effect or text animation on its own timer.