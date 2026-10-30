using System.Text.Json;

namespace BusyLight.Tray.Firmware;

/// <summary>
/// Compatibility information packaged beside the merged firmware binary.
/// The loader accepts the current manifest and a few common aliases so a
/// packaging-script naming change does not silently bypass validation.
/// </summary>
public sealed record FirmwareManifest
{
    public string FirmwareVersion { get; init; } = "";
    public string FirmwareFile { get; init; } = "";
    public IReadOnlyList<string> CompatibleChips { get; init; } = Array.Empty<string>();
    public int MinimumFlashBytes { get; init; } = 4 * 1024 * 1024;
    public IReadOnlyList<string> UsbIds { get; init; } = Array.Empty<string>();
    public string? Sha256 { get; init; }

    public static FirmwareManifest Load(string manifestPath)
    {
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("Firmware manifest was not found.", manifestPath);

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = document.RootElement;

        var version = String(root, "firmwareVersion", "version", "fwVersion");
        var file = String(root, "firmwareFile", "file", "binary", "mergedBinary");
        var chips = StringArray(root, "compatibleChips", "chips", "chipModels", "chip");
        var usbIds = StringArray(root, "usbIds", "supportedUsbIds", "vidPid");
        var sha256 = String(root, "sha256", "firmwareSha256");
        var minimum = FlashBytes(root);

        if (string.IsNullOrWhiteSpace(version))
            throw new InvalidDataException("manifest.json does not contain a firmware version.");
        if (string.IsNullOrWhiteSpace(file))
            throw new InvalidDataException("manifest.json does not contain a firmware file name.");
        if (chips.Count == 0)
            chips = new[] { "ESP32-PICO-D4" };

        var manifest = new FirmwareManifest
        {
            FirmwareVersion = version,
            FirmwareFile = file,
            CompatibleChips = chips,
            MinimumFlashBytes = minimum,
            UsbIds = usbIds,
            Sha256 = sha256
        };

        var firmwarePath = Path.Combine(Path.GetDirectoryName(manifestPath)!, manifest.FirmwareFile);
        if (!File.Exists(firmwarePath))
            throw new FileNotFoundException("The firmware file referenced by manifest.json was not found.", firmwarePath);

        return manifest;
    }

    public bool SupportsChip(string? chip) =>
        !string.IsNullOrWhiteSpace(chip) && CompatibleChips.Any(expected =>
            chip.Contains(expected, StringComparison.OrdinalIgnoreCase));

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
                return new[] { value.GetString()! }.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
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
        if (TryProperty(root, "minimumFlashBytes", out var bytes) && bytes.TryGetInt32(out var b)) return b;
        if (TryProperty(root, "minFlashBytes", out bytes) && bytes.TryGetInt32(out b)) return b;
        if (TryProperty(root, "minimumFlashMb", out var mb) && mb.TryGetInt32(out var m)) return m * 1024 * 1024;
        if (TryProperty(root, "minFlashMb", out mb) && mb.TryGetInt32(out m)) return m * 1024 * 1024;
        if (TryProperty(root, "minimumFlash", out var text) && text.ValueKind == JsonValueKind.String)
        {
            var raw = text.GetString()!.Trim().ToUpperInvariant().Replace(" ", "");
            if (raw.EndsWith("MB") && int.TryParse(raw[..^2], out m)) return m * 1024 * 1024;
        }
        return 4 * 1024 * 1024;
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
