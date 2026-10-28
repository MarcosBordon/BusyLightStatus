# Busy Light Pilot: M5Stack ATOM Matrix + Microsoft Teams

Prototipo de una sola unidad. Incluye aplicación de bandeja para Windows en .NET 8, consulta de presencia propia mediante Microsoft Graph, conexión USB serial y firmware PlatformIO/Arduino.

## 1. Preparación en Microsoft Entra ID

1. Registre una aplicación de tipo cliente público para una sola organización.
2. Copie `Directory (tenant) ID` y `Application (client) ID`.
3. Agregue Microsoft Graph, permiso delegado `Presence.Read`.
4. En autenticación, habilite el escenario de aplicación móvil y de escritorio y configure un redirect URI compatible con cliente público.
5. Aplique el proceso de consentimiento y asignación definido por Gobierno de IT.
6. Reemplace `YOUR_TENANT_ID` y `YOUR_CLIENT_ID` en `src/BusyLight.Tray/appsettings.json`.

No cree ni coloque un client secret en esta aplicación de escritorio.

## 2. Compilar la aplicación Windows

Requisitos: Windows 10/11, SDK .NET 8 y Visual Studio 2022 o CLI de .NET.

```powershell
dotnet restore .\BusyLightPilot.sln
dotnet build .\BusyLightPilot.sln -c Release
dotnet run --project .\src\BusyLight.Tray\BusyLight.Tray.csproj
```

Al primer inicio aparecerá el login corporativo. Después, la app queda en la bandeja y consulta presencia cada 20 segundos.

## 3. Grabar el firmware

Requisitos: VS Code + PlatformIO, o PlatformIO Core.

```powershell
cd .irmwaretom-matrix
pio run
pio run --target upload
pio device monitor
```

## 4. Prueba rápida sin Microsoft Graph

Abra un terminal serial a 115200 y envíe una línea como:

```json
{"Cmd":"setStatus","Availability":"Busy","Activity":"InAMeeting","Color":"#FF0000","Effect":"pulse","Brightness":20}
```

El ATOM devuelve `{"status":"ok"}`. Para descubrirlo, la app envía `{"cmd":"identify"}` y espera una respuesta que contenga `BUSYLIGHT-ATOM`.

## 5. Estados básicos

- Disponible: verde fijo.
- Ocupado: rojo fijo.
- En reunión o llamada: rojo pulsante.
- No molestar: doble pulso rojo.
- Presentando: magenta pulsante.
- Ausente: amarillo.
- Sin conexión con la app por 60 segundos: azul intermitente.

## 6. Notas de prototipo

- El firmware limita brillo por seguridad y fija un máximo de potencia de 300 mA.
- La app prueba los puertos COM y reconoce el dispositivo mediante handshake.
- El caché de MSAL de esta base es de proceso. Para despliegue corporativo conviene agregar persistencia protegida del token y firma del instalador.
- Antes del piloto real, revise permisos, consentimiento, distribución del ejecutable y tratamiento de telemetría con Gobierno de IT y Ciberseguridad.
