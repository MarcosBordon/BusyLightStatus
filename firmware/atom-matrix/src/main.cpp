#include <M5Atom.h>
#include <ArduinoJson.h>

static const char* DEVICE_ID  = "BUSYLIGHT-ATOM";
static const char* FW_VERSION = "1.2.0";
static const unsigned long WATCHDOG_MS = 60000UL;
static const int MATRIX_LEDS = 25;

enum Pattern { PATTERN_FULL, PATTERN_LIGHTS };

Pattern  pattern       = PATTERN_FULL;
uint32_t targetColor   = 0x0000FF;   // azul al arrancar
uint32_t topColor      = 0;
uint32_t middleColor   = 0;
uint32_t bottomColor   = 0;
String   effect        = "blink";
uint8_t  brightnessPct = 100;        // 10-100 % sobre el brillo seguro de M5Atom
unsigned long lastCommandMs = 0;

uint32_t shown[MATRIX_LEDS];
bool     shownValid = false;
String   rxBuffer;

// ---------- Utilidades de color ----------

uint32_t scaleColor(uint32_t c, uint8_t pct)
{
    uint32_t r = ((c >> 16) & 0xFF) * pct / 100;
    uint32_t g = ((c >> 8)  & 0xFF) * pct / 100;
    uint32_t b = ( c        & 0xFF) * pct / 100;
    return (r << 16) | (g << 8) | b;
}

uint32_t parseHex(const char* hex, uint32_t fallback)
{
    if (!hex || hex[0] != '#') return fallback;
    return strtoul(hex + 1, nullptr, 16);
}

// Envía el cuadro a la matriz solo si cambió
void pushFrame(const uint32_t frame[MATRIX_LEDS])
{
    if (shownValid && memcmp(frame, shown, sizeof(shown)) == 0) return;
    for (int i = 0; i < MATRIX_LEDS; i++)
    {
        M5.dis.drawpix(i, CRGB(frame[i]));
        shown[i] = frame[i];
    }
    shownValid = true;
}

// Construye el cuadro según el patrón, con un brillo dado
void buildFrame(uint32_t frame[MATRIX_LEDS], uint8_t pct)
{
    for (int row = 0; row < 5; row++)
    {
        uint32_t c;
        if (pattern == PATTERN_FULL)
        {
            c = targetColor;
        }
        else
        {
            // Filas 0, 2 y 4 son las luces; 1 y 3 quedan apagadas
            switch (row)
            {
                case 0:  c = topColor;    break;
                case 2:  c = middleColor; break;
                case 4:  c = bottomColor; break;
                default: c = 0;           break;
            }
        }
        uint32_t scaled = scaleColor(c, pct);
        for (int col = 0; col < 5; col++) frame[row * 5 + col] = scaled;
    }
}

// ---------- Protocolo serial ----------

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

    if (!strcmp(cmd, "ping"))
    {
        lastCommandMs = millis();   // renueva el watchdog sin cambiar los LED
        Serial.println("{\"status\":\"ok\"}");
        return;
    }

    if (!strcmp(cmd, "setStatus"))
    {
        const char* pat = doc["Pattern"] | "full";

        if (!strcmp(pat, "lights"))
        {
            pattern     = PATTERN_LIGHTS;
            topColor    = parseHex(doc["Top"]    | "#000000", 0);
            middleColor = parseHex(doc["Middle"] | "#000000", 0);
            bottomColor = parseHex(doc["Bottom"] | "#000000", 0);
        }
        else
        {
            pattern     = PATTERN_FULL;
            targetColor = parseHex(doc["Color"] | "#0000FF", 0x0000FF);
        }

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

// ---------- Animación ----------

void render()
{
    unsigned long now = millis();
    uint32_t frame[MATRIX_LEDS];

    // Watchdog: sin comandos por 60 s -> azul intermitente tenue en toda la matriz
    if (lastCommandMs != 0 && now - lastCommandMs > WATCHDOG_MS)
    {
        bool on = (now / 500) % 2 == 0;
        uint32_t c = on ? scaleColor(0x0000FF, 40) : 0;
        for (int i = 0; i < MATRIX_LEDS; i++) frame[i] = c;
        pushFrame(frame);
        return;
    }

    // Nivel del efecto (0-100 %)
    uint8_t level = 100;

    if (effect == "blink")
    {
        level = ((now / 500) % 2 == 0) ? 100 : 0;
    }
    else if (effect == "doublepulse")
    {
        unsigned long t = now % 1600;
        level = ((t < 140) || (t > 260 && t < 400)) ? 100 : 0;
    }
    else if (effect == "pulse")
    {
        unsigned long t = now % 2000;
        level = (t < 1000) ? 15 + t * 85 / 1000
                           : 15 + (2000 - t) * 85 / 1000;
        level = (level / 5) * 5;   // escalones de 5 % para no saturar la matriz
    }
    // "solid" u otro valor: level = 100

    buildFrame(frame, (uint8_t)((uint16_t)brightnessPct * level / 100));
    pushFrame(frame);
}

void setup()
{
    M5.begin(true, false, true);   // Serial, sin I2C, matriz LED
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