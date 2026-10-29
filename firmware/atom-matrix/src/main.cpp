#include <M5Atom.h>
#include <ArduinoJson.h>

static const char* DEVICE_ID  = "BUSYLIGHT-ATOM";
static const char* FW_VERSION = "1.3.1";
static const unsigned long WATCHDOG_MS = 60000UL;
static const int MATRIX_LEDS = 25;

// Text
static const int TEXT_MAX_CHARS = 15;
static const unsigned long TEXT_SLOT_MS  = 500;   // total time per letter
static const unsigned long TEXT_ON_MS    = 400;   // letter on (then 100 ms off)
static const unsigned long TEXT_PAUSE_MS = 1000;  // pause between repetitions
static const uint8_t CHAR_ENYE = 0x01;            // internal code for Ñ

enum Pattern { PATTERN_FULL, PATTERN_LIGHTS, PATTERN_ROUNDED };
enum DisplayMode { BL_MODE_STATUS, BL_MODE_TEXT };

// ---------- 5x5 font (bit 4 = left column) ----------

struct Glyph { uint8_t code; uint8_t rows[5]; };

static const Glyph BL_FONT[] = {
    {'A', {0b01110, 0b10001, 0b11111, 0b10001, 0b10001}},
    {'B', {0b11110, 0b10001, 0b11110, 0b10001, 0b11110}},
    {'C', {0b01111, 0b10000, 0b10000, 0b10000, 0b01111}},
    {'D', {0b11110, 0b10001, 0b10001, 0b10001, 0b11110}},
    {'E', {0b11111, 0b10000, 0b11110, 0b10000, 0b11111}},
    {'F', {0b11111, 0b10000, 0b11110, 0b10000, 0b10000}},
    {'G', {0b01111, 0b10000, 0b10011, 0b10001, 0b01111}},
    {'H', {0b10001, 0b10001, 0b11111, 0b10001, 0b10001}},
    {'I', {0b11111, 0b00100, 0b00100, 0b00100, 0b11111}},
    {'J', {0b00111, 0b00001, 0b00001, 0b10001, 0b01110}},
    {'K', {0b10001, 0b10010, 0b11100, 0b10010, 0b10001}},
    {'L', {0b10000, 0b10000, 0b10000, 0b10000, 0b11111}},
    {'M', {0b10001, 0b11011, 0b10101, 0b10001, 0b10001}},
    {'N', {0b10001, 0b11001, 0b10101, 0b10011, 0b10001}},
    {CHAR_ENYE, {0b01110, 0b10001, 0b11001, 0b10101, 0b10011}},
    {'O', {0b01110, 0b10001, 0b10001, 0b10001, 0b01110}},
    {'P', {0b11110, 0b10001, 0b11110, 0b10000, 0b10000}},
    {'Q', {0b01110, 0b10001, 0b10101, 0b10010, 0b01101}},
    {'R', {0b11110, 0b10001, 0b11110, 0b10010, 0b10001}},
    {'S', {0b01111, 0b10000, 0b01110, 0b00001, 0b11110}},
    {'T', {0b11111, 0b00100, 0b00100, 0b00100, 0b00100}},
    {'U', {0b10001, 0b10001, 0b10001, 0b10001, 0b01110}},
    {'V', {0b10001, 0b10001, 0b10001, 0b01010, 0b00100}},
    {'W', {0b10001, 0b10001, 0b10101, 0b11011, 0b10001}},
    {'X', {0b10001, 0b01010, 0b00100, 0b01010, 0b10001}},
    {'Y', {0b10001, 0b01010, 0b00100, 0b00100, 0b00100}},
    {'Z', {0b11111, 0b00010, 0b00100, 0b01000, 0b11111}},
    {'0', {0b01110, 0b10011, 0b10101, 0b11001, 0b01110}},
    {'1', {0b00100, 0b01100, 0b00100, 0b00100, 0b01110}},
    {'2', {0b11110, 0b00001, 0b01110, 0b10000, 0b11111}},
    {'3', {0b11110, 0b00001, 0b00110, 0b00001, 0b11110}},
    {'4', {0b10010, 0b10010, 0b11111, 0b00010, 0b00010}},
    {'5', {0b11111, 0b10000, 0b11110, 0b00001, 0b11110}},
    {'6', {0b01110, 0b10000, 0b11110, 0b10001, 0b01110}},
    {'7', {0b11111, 0b00001, 0b00010, 0b00100, 0b00100}},
    {'8', {0b01110, 0b10001, 0b01110, 0b10001, 0b01110}},
    {'9', {0b01110, 0b10001, 0b01111, 0b00001, 0b01110}},
    {'!', {0b00100, 0b00100, 0b00100, 0b00000, 0b00100}},
    {'?', {0b01110, 0b10001, 0b00110, 0b00000, 0b00100}},
    {'.', {0b00000, 0b00000, 0b00000, 0b00000, 0b00100}},
    {'-', {0b00000, 0b00000, 0b01110, 0b00000, 0b00000}},
    {':', {0b00000, 0b00100, 0b00000, 0b00100, 0b00000}},
};

// ---------- State ----------

DisplayMode mode       = BL_MODE_STATUS;
Pattern  pattern       = PATTERN_FULL;
uint32_t targetColor   = 0x0000FF;   // blue at boot
uint32_t topColor      = 0;
uint32_t middleColor   = 0;
uint32_t bottomColor   = 0;
String   effect        = "blink";
uint8_t  brightnessPct = 100;        // 10-100 % of M5Atom's safe brightness
unsigned long lastCommandMs = 0;

uint8_t  textCodes[TEXT_MAX_CHARS];
uint32_t textColors[TEXT_MAX_CHARS];
int      textLen     = 0;
uint32_t textColor   = 0xFFFFFF;
bool     textRandom  = false;
unsigned long textStartMs = 0;
long     textCycle   = -1;

uint32_t shown[MATRIX_LEDS];
bool     shownValid = false;
String   rxBuffer;

// ---------- Color helpers ----------

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

uint32_t randomVividColor()
{
    CRGB c;
    hsv2rgb_rainbow(CHSV((uint8_t)random(0, 256), 255, 255), c);
    return ((uint32_t)c.r << 16) | ((uint32_t)c.g << 8) | c.b;
}

// Sends the frame to the matrix only if it changed
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

// ---------- Text ----------

const uint8_t* findGlyph(uint8_t code)
{
    for (size_t i = 0; i < sizeof(BL_FONT) / sizeof(BL_FONT[0]); i++)
    {
        if (BL_FONT[i].code == code) return BL_FONT[i].rows;
    }
    return nullptr;   // space or character without a glyph
}

// Converts UTF-8 to font codes: uppercase, no accents, keeps Ñ
int decodeText(const char* s, uint8_t* out, int maxLen)
{
    const uint8_t* p = (const uint8_t*)s;
    int n = 0;

    while (*p && n < maxLen)
    {
        uint8_t c = *p;
        uint8_t code = ' ';

        if (c < 0x80)
        {
            code = (c >= 'a' && c <= 'z') ? (uint8_t)(c - 32) : c;
            p++;
        }
        else if (c == 0xC3 && p[1])
        {
            switch (p[1])
            {
                case 0x91: case 0xB1:                       code = CHAR_ENYE; break; // Ñ ñ
                case 0x80: case 0x81: case 0xA0: case 0xA1: code = 'A'; break;       // À Á à á
                case 0x88: case 0x89: case 0xA8: case 0xA9: code = 'E'; break;       // È É è é
                case 0x8C: case 0x8D: case 0xAC: case 0xAD: code = 'I'; break;       // Ì Í ì í
                case 0x92: case 0x93: case 0xB2: case 0xB3: code = 'O'; break;       // Ò Ó ò ó
                case 0x99: case 0x9A: case 0x9C:
                case 0xB9: case 0xBA: case 0xBC:            code = 'U'; break;       // Ù Ú Ü ù ú ü
                case 0x87: case 0xA7:                       code = 'C'; break;       // Ç ç
                default:                                    code = ' '; break;
            }
            p += 2;
        }
        else
        {
            // Other multibyte character: skipped and shown as a space
            p++;
            while ((*p & 0xC0) == 0x80) p++;
        }

        if (code != ' ' && !findGlyph(code)) code = ' ';
        out[n++] = code;
    }
    return n;
}

void renderText(unsigned long now, uint32_t frame[MATRIX_LEDS])
{
    for (int i = 0; i < MATRIX_LEDS; i++) frame[i] = 0;

    unsigned long textMs  = (unsigned long)textLen * TEXT_SLOT_MS;
    unsigned long cycleMs = textMs + TEXT_PAUSE_MS;
    unsigned long elapsed = now - textStartMs;
    long cycle = (long)(elapsed / cycleMs);

    // New colors on every repetition
    if (cycle != textCycle)
    {
        textCycle = cycle;
        for (int i = 0; i < textLen; i++)
            textColors[i] = textRandom ? randomVividColor() : textColor;
    }

    unsigned long t = elapsed % cycleMs;
    if (t >= textMs) return;                         // pause between repetitions
    if (t % TEXT_SLOT_MS >= TEXT_ON_MS) return;      // off between letters

    int idx = (int)(t / TEXT_SLOT_MS);
    const uint8_t* g = findGlyph(textCodes[idx]);
    if (!g) return;                                  // space

    uint32_t c = scaleColor(textColors[idx], brightnessPct);
    for (int row = 0; row < 5; row++)
        for (int col = 0; col < 5; col++)
            if (g[row] & (0x10 >> col)) frame[row * 5 + col] = c;
}

// ---------- Status / traffic light ----------

void buildFrame(uint32_t frame[MATRIX_LEDS], uint8_t pct)
{
    for (int i = 0; i < MATRIX_LEDS; i++)
    {
        int row = i / 5;
        int col = i % 5;
        uint32_t c;

        if (pattern == PATTERN_LIGHTS)
        {
            // Traffic light: rows 0, 2 and 4 on
            c = (row == 0) ? topColor
              : (row == 2) ? middleColor
              : (row == 4) ? bottomColor
              : 0;
        }
        else
        {
            c = targetColor;

            // Rounded: corners off
            if (pattern == PATTERN_ROUNDED &&
                (row == 0 || row == 4) && (col == 0 || col == 4))
            {
                c = 0;
            }
        }

        frame[i] = scaleColor(c, pct);
    }
}

// ---------- Serial protocol ----------

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
        lastCommandMs = millis();
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
            pattern     = !strcmp(pat, "rounded") ? PATTERN_ROUNDED : PATTERN_FULL;
            targetColor = parseHex(doc["Color"] | "#0000FF", 0x0000FF);
        }

        effect        = String((const char*)(doc["Effect"] | "solid"));
        brightnessPct = constrain((int)(doc["Brightness"] | 100), 10, 100);
        mode          = BL_MODE_STATUS;
        lastCommandMs = millis();
        Serial.println("{\"status\":\"ok\"}");
        return;
    }

    if (!strcmp(cmd, "setText"))
    {
        const char* txt = doc["Text"] | "";
        int n = decodeText(txt, textCodes, TEXT_MAX_CHARS);
        if (n == 0)
        {
            Serial.println("{\"status\":\"error\",\"reason\":\"empty_text\"}");
            return;
        }

        textLen       = n;
        textRandom    = doc["Random"] | false;
        textColor     = parseHex(doc["Color"] | "#FFFFFF", 0xFFFFFF);
        brightnessPct = constrain((int)(doc["Brightness"] | 100), 10, 100);
        textStartMs   = millis();
        textCycle     = -1;
        mode          = BL_MODE_TEXT;
        lastCommandMs = millis();
        Serial.printf("{\"status\":\"ok\",\"chars\":%d}\n", n);
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

// ---------- Animation ----------

void render()
{
    unsigned long now = millis();
    uint32_t frame[MATRIX_LEDS];

    // Watchdog: no commands for 60 s -> dim blinking blue
    if (lastCommandMs != 0 && now - lastCommandMs > WATCHDOG_MS)
    {
        bool on = (now / 500) % 2 == 0;
        uint32_t c = on ? scaleColor(0x0000FF, 40) : 0;
        for (int i = 0; i < MATRIX_LEDS; i++) frame[i] = c;
        pushFrame(frame);
        return;
    }

    if (mode == BL_MODE_TEXT)
    {
        renderText(now, frame);
        pushFrame(frame);
        return;
    }

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
        level = (level / 5) * 5;
    }

    buildFrame(frame, (uint8_t)((uint16_t)brightnessPct * level / 100));
    pushFrame(frame);
}

void setup()
{
    M5.begin(true, false, true);   // Serial, no I2C, LED matrix
    delay(50);
    randomSeed(esp_random());
    identify();
}

void loop()
{
    M5.update();
    readSerial();
    render();
    delay(5);
}
