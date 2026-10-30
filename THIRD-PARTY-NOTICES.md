# Third-party notices

BusyLightStatus is distributed under the MIT license. The installer also includes the following third-party components, each under its own license.

## Bundled tool

| Component | Version | License | Source |
|---|---|---|---|
| esptool (Espressif Systems) | 4.8.1 | GPL-2.0 | https://github.com/espressif/esptool |

esptool is distributed **unmodified, as a separate program** (`tools\esptool\esptool.exe`). BusyLightStatus only runs it as an external process to flash the ATOM Matrix. Its full license is in `tools\esptool\LICENSE.txt`, and its source code is available at the link above.

## Libraries compiled into the firmware

| Component | License | Source |
|---|---|---|
| Arduino core for ESP32 (Espressif) | LGPL-2.1 | https://github.com/espressif/arduino-esp32 |
| M5Atom (M5Stack) | MIT | https://github.com/m5stack/M5Atom |
| FastLED | MIT | https://github.com/FastLED/FastLED |
| ArduinoJson (Benoit Blanchon) | MIT | https://github.com/bblanchon/ArduinoJson |

The complete source code of the BusyLightStatus firmware, and the build configuration needed to rebuild it with PlatformIO, is available at https://github.com/MarcosBordon/BusyLightStatus.
