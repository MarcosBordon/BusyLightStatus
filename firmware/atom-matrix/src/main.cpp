#include <M5Atom.h>
#include <ArduinoJson.h>

static const char* DEVICE_ID  = "BUSYLIGHT-ATOM";
static const char* FW_VERSION = "1.1.0";
static const unsigned long WATCHDOG_MS = 60000UL;

uint32_t targetColor   = 0x0000FF;   // azul al arrancar
String   effect        = "blink";
uint8_t  brightnessPct = 100;        // 10-100 % sobre el brillo seguro de M5Atom
unsigned long lastCommandMs = 0;
uint32_t lastShown = 0xFFFFFFFF;
String   rxBuffer;

uint32_t scaleColor(uint32_t c, uint8_t pct)
{
    uint32_t r = ((c >> 16) & 0xFF) * pct / 100;
    uint32_t g = ((c >> 8)  & 0xFF) * pct / 100;
    uint32_t b = ( c        & 0xFF) * pct / 100;
    return (r << 16) | (g << 8) | b;
}

void show(uint32_t c)
{
    if (c == lastShown) return;      // evita refrescos innecesarios
    M5.dis.fillpix(c);
    lastShown = c;
}

uint32_t parseHex(const char* hex)
{
    if (!hex || hex[0] != '#') return 0x0000FF;
    return strtoul(hex + 1, nullptr, 16);
}

void identify()
{
    Serial.printf("{\"status\":\"ok\",\"device\":\"%s\",\"firmware\":\"%s\"}\n",
                  DEVICE_ID, FW_VERSION);
}

void processLine(const String& line)
{
    JsonDocument doc;
    if (deserializeJson(doc, line))
    {
        Serial.println("{\"status\":\"error\",\"reason\":\"invalid_json\"}");
        return;
    }

    const char* cmd = doc["Cmd"] | "";
    if (!cmd[0]) cmd = doc["cmd"] | "";

    if (!strcmp(cmd, "identify"))
    {
        identify();
        return;
    }

    if (!strcmp(cmd, "setStatus"))
    {
        targetColor   = parseHex(doc["Color"] | "#0000FF");
        effect        = String((const char*)(doc["Effect"] | "solid"));
        brightnessPct = constrain((int)(doc["Brightness"] | 100), 10, 100);
        lastCommandMs = millis();
        Serial.println("{\"status\":\"ok\"}");
        return;
    }

    Serial.println("{\"status\":\"error\",\"reason\":\"unknown_command\"}");
}

void readSerial()
{
    while (Serial.available())
    {
        char ch = (char)Serial.read();
        if (ch == '\n')
        {
            rxBuffer.trim();
            if (rxBuffer.length()) processLine(rxBuffer);
            rxBuffer = "";
        }
        else if (ch != '\r' && rxBuffer.length() < 512)
        {
            rxBuffer += ch;
        }
    }
}

void render()
{
    unsigned long now = millis();

    // Watchdog: sin comandos por 60 s -> azul intermitente tenue
    if (lastCommandMs != 0 && now - lastCommandMs > WATCHDOG_MS)
    {
        bool on = (now / 500) % 2 == 0;
        show(on ? scaleColor(0x0000FF, 40) : 0x000000);
        return;
    }

    uint32_t full = scaleColor(targetColor, brightnessPct);

    if (effect == "solid")
    {
        show(full);
    }
    else if (effect == "blink")
    {
        show(((now / 500) % 2 == 0) ? full : 0x000000);
    }
    else if (effect == "doublepulse")
    {
        unsigned long t = now % 1600;
        bool on = (t < 140) || (t > 260 && t < 400);
        show(on ? full : 0x000000);
    }
    else // "pulse": respiración suave entre 15 % y 100 %
    {
        unsigned long t = now % 2000;
        uint8_t pct = (t < 1000) ? 15 + t * 85 / 1000
                                 : 15 + (2000 - t) * 85 / 1000;
        pct = (pct / 5) * 5;          // escalones de 5 % para no saturar la matriz
        show(scaleColor(full, pct));
    }
}

void setup()
{
    M5.begin(true, false, true);      // Serial, sin I2C, matriz LED
    delay(50);
    identify();
}

void loop()
{
    M5.update();
    readSerial();
    render();
    delay(5);
}