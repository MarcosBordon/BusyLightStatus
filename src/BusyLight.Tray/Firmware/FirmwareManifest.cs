using System.Text.Json;

namespace BusyLight.Tray.Firmware;

/// <summary>
/// Describes the firmware package installed beside BusyLightStatus.
/// manifest.json is the source of truth for version, chip, flash requirements,
/// binary name, write offset, baud rate and SHA-256 integrity validation.
/// </summary>
public sealed record FirmwareManifest
{
    public string FirmwareVersion { get; init; } = "";
    public string FirmwareFile { get; init; } = "";
    public IReadOnlyList<string> CompatibleChips { get; init; } = Array.Empty<string>();
    public int MinimumFlashBytes { get; init; } = 4 * 1024 * 1024;
    public IReadOnlyList<string> UsbIds { get; init; } = Array.Empty<string>();
    public string? Sha256 { get; init; }
    public string FlashOffset { get; init; } = "0x0";
    public int FlashBaud { get; init; } = 115200;
    public long? FirmwareSizeBytes { get; init; }

    public static FirmwareManifest Load(string manifestPath)
    {
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("Firmware manifest was not found.", manifestPath);

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = document.RootElement;

        // Current package names plus compatible aliases for older manifests.
        var version = String(root, "firmwareVersion", "version", "fwVersion");
        var file = String(root, "firmwareFile", "file", "binary", "mergedBinary");
        var chips = StringArray(root, "compatibleChips", "chips", "chipModels", "chip");
        var usbIds = StringArray(root, "usbIds", "supportedUsbIds", "vidPid");
        var sha256 = String(root, "sha256", "firmwareSha256");
        var offset = String(root, "offset", "flashOffset");
        var minimumFlashBytes = FlashBytes(root);
        var baud = Int(root, 115200, "baud", "flashBaud");
        var firmwareSize = NullableLong(root, "size", "firmwareSize", "firmwareSizeBytes");

        if (string.IsNullOrWhiteSpace(version))
            throw new InvalidDataException("manifest.json does not contain a firmware version.");
        if (string.IsNullOrWhiteSpace(file))
            throw new InvalidDataException("manifest.json does not contain a firmware file name.");
        if (chips.Count == 0)
            chips = new[] { "ESP32-PICO-D4" };
        if (baud <= 0)
            throw new InvalidDataException("manifest.json contains an invalid flashing baud rate.");
        if (!IsValidOffset(offset))
            throw new InvalidDataException($"manifest.json contains an invalid flash offset: {offset}");

        var manifest = new FirmwareManifest
        {
            FirmwareVersion = version,
            FirmwareFile = file,
            CompatibleChips = chips,
            MinimumFlashBytes = minimumFlashBytes,
            UsbIds = usbIds,
            Sha256 = sha256,
            FlashOffset = string.IsNullOrWhiteSpace(offset) ? "0x0" : offset,
            FlashBaud = baud,
            FirmwareSizeBytes = firmwareSize
        };

        var firmwarePath = Path.Combine(Path.GetDirectoryName(manifestPath)!, manifest.FirmwareFile);
        if (!File.Exists(firmwarePath))
            throw new FileNotFoundException(
                "The firmware file referenced by manifest.json was not found.", firmwarePath);

        if (manifest.FirmwareSizeBytes is long expectedSize)
        {
            var actualSize = new FileInfo(firmwarePath).Length;
            if (actualSize != expectedSize)
                throw new InvalidDataException(
                    $"Firmware size does not match manifest.json. Expected {expectedSize} bytes; found {actualSize} bytes.");
        }

        return manifest;
    }

    public bool SupportsChip(string? chip) =>
        !string.IsNullOrWhiteSpace(chip) && CompatibleChips.Any(expected =>
            chip.Contains(expected, StringComparison.OrdinalIgnoreCase) ||
            expected.Contains(chip, StringComparison.OrdinalIgnoreCase));

    private static string String(JsonElement root, params string[] names)
    {
        foreach (var name in names)
            if (TryProperty(root, name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString()?.Trim() ?? "";
        return "";
    }

    private static IReadOnlyList<string> StringArray(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryProperty(root, name, out var value)) continue;

            if (value.ValueKind == JsonValueKind.String)
            {
                var item = value.GetString();
                return string.IsNullOrWhiteSpace(item) ? Array.Empty<string>() : new[] { item };
            }

            if (value.ValueKind == JsonValueKind.Array)
                return value.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString()!)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToArray();
        }

        return Array.Empty<string>();
    }

    private static int FlashBytes(JsonElement root)
    {
        if (TryProperty(root, "minimumFlashBytes", out var bytes) && bytes.TryGetInt32(out var value))
            return value;
        if (TryProperty(root, "minFlashBytes", out bytes) && bytes.TryGetInt32(out value))
            return value;

        // Current manifest uses minFlashMB.
        foreach (var name in new[] { "minimumFlashMb", "minFlashMb", "minFlashMB" })
            if (TryProperty(root, name, out var mb) && mb.TryGetInt32(out var sizeMb))
                return checked(sizeMb * 1024 * 1024);

        if (TryProperty(root, "minimumFlash", out var text) && text.ValueKind == JsonValueKind.String)
        {
            var raw = text.GetString()!.Trim().ToUpperInvariant().Replace(" ", "");
            if (raw.EndsWith("MB") && int.TryParse(raw[..^2], out var sizeMb))
                return checked(sizeMb * 1024 * 1024);
        }

        return 4 * 1024 * 1024;
    }

    private static int Int(JsonElement root, int fallback, params string[] names)
    {
        foreach (var name in names)
            if (TryProperty(root, name, out var value) && value.TryGetInt32(out var result))
                return result;
        return fallback;
    }

    private static long? NullableLong(JsonElement root, params string[] names)
    {
        foreach (var name in names)
            if (TryProperty(root, name, out var value) && value.TryGetInt64(out var result))
                return result;
        return null;
    }

    private static bool IsValidOffset(string offset)
    {
        if (string.IsNullOrWhiteSpace(offset)) return true;
        var text = offset.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? offset[2..] : offset;
        return text.Length > 0 && text.All(Uri.IsHexDigit);
    }

    private static bool TryProperty(JsonElement root, string name, out JsonElement value)
    {
        foreach (var property in root.EnumerateObject())
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }

        value = default;
        return false;
    }
}
