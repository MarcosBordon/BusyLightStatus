# Diagrama técnico

```mermaid
flowchart LR
  T[Microsoft Teams] --> G[Microsoft Graph
GET /me/presence]
  G -->|HTTPS + OAuth 2.0
Presence.Read delegado| A[Aplicación Windows .NET 8
MSAL + bandeja]
  A -->|JSON por USB Serial
115200 8N1| U[USB-C / puerto COM]
  U --> E[M5Stack ATOM Matrix
ESP32-PICO-D4]
  E --> L[25 LED RGB integrados
GPIO 27]
  E --> W[Watchdog 60 s
Azul intermitente]
```

## Flujo
1. La app adquiere un token delegado con MSAL.
2. Consulta `GET https://graph.microsoft.com/v1.0/me/presence`.
3. Convierte `availability` y `activity` a color y efecto.
4. Detecta el ATOM por handshake serial.
5. Envía una línea JSON y el firmware actualiza la matriz.
