# Connections

## Pilot with no external electronics

| From | To | Purpose |
|---|---|---|
| Windows PC, USB-A or USB-C | ATOM Matrix USB-C | 5 V power, flashing and serial port |
| ESP32 GPIO 27 | Built-in WS2812C matrix | RGB data (factory wiring) |
| Internal 5 V | Built-in RGB matrix | Power (factory wiring) |
| Internal GND | Built-in RGB matrix | Ground (factory wiring) |

No jumpers, breadboard, resistors or external power supply are needed. Use a USB-C cable that supports data, not charge-only.

## Serial settings

- 115200 baud
- 8 data bits, no parity, 1 stop bit
- Line ending: `\n`
- One UTF-8 JSON command per line
- DTR and RTS disabled, so the ATOM does not reset when the port opens

## Libraries (firmware)

- `m5stack/M5Atom`
- `fastled/FastLED@3.6.0` (pinned: newer versions do not drive this matrix)
- `bblanchon/ArduinoJson@^7.2.1`
