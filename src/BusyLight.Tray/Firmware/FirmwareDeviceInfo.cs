namespace BusyLight.Tray.Firmware;

/// <summary>
/// Result of validating one serial device with the packaged copy of esptool.
/// This model contains diagnostics only. It never authorizes a flash by itself;
/// callers must check IsCompatible before enabling firmware installation.
/// </summary>
public sealed record FirmwareDeviceInfo
{
    public string PortName { get; init; } = "";
    public string? UsbId { get; init; }
    public string? Chip { get; init; }
    public string? Revision { get; init; }
    public string? MacAddress { get; init; }
    public int? FlashSizeBytes { get; init; }
    public string? FlashManufacturer { get; init; }
    public string? FlashDeviceId { get; init; }
    public string? AvailableFirmwareVersion { get; init; }
    public string? FirmwareFile { get; init; }
    public bool ChipMatches { get; init; }
    public bool FlashMatches { get; init; }
    public bool IsCompatible => ChipMatches && FlashMatches && Errors.Count == 0;
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public string ChipIdOutput { get; init; } = "";
    public string FlashIdOutput { get; init; } = "";

    public double? FlashSizeMb => FlashSizeBytes is int bytes
        ? Math.Round(bytes / 1024d / 1024d, 1)
        : null;

    public string FlashSizeDisplay => FlashSizeMb is double mb ? $"{mb:0.#} MB" : "Unknown";

    public string ToDisplayText()
    {
        if (!IsCompatible)
        {
            var details = Errors.Count == 0 ? "Unknown validation error." : string.Join(Environment.NewLine, Errors);
            return $"Device is not compatible{Environment.NewLine}{Environment.NewLine}" +
                   $"Port: {PortName}{Environment.NewLine}" +
                   $"Detected chip: {Chip ?? "Unknown"}{Environment.NewLine}" +
                   $"Flash: {FlashSizeDisplay}{Environment.NewLine}{Environment.NewLine}" + details;
        }

        return $"Compatible device detected{Environment.NewLine}{Environment.NewLine}" +
               $"Port: {PortName}{Environment.NewLine}" +
               $"Chip: {Chip}{Environment.NewLine}" +
               $"Revision: {Revision ?? "Unknown"}{Environment.NewLine}" +
               $"Flash: {FlashSizeDisplay}{Environment.NewLine}" +
               $"MAC: {MacAddress ?? "Unknown"}{Environment.NewLine}{Environment.NewLine}" +
               $"Firmware available: {AvailableFirmwareVersion ?? "Unknown"}{Environment.NewLine}{Environment.NewLine}" +
               "Ready for firmware installation.";
    }
}
