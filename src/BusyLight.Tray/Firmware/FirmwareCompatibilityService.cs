using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;

namespace BusyLight.Tray.Firmware;

/// <summary>
/// Read-only device validation. This service runs chip_id and flash_id only;
/// it deliberately contains no write_flash command.
/// The caller must release SerialDeviceService before calling CheckAsync.
/// </summary>
public sealed partial class FirmwareCompatibilityService
{
    private readonly string _appDirectory;
    private readonly string _esptoolPath;
    private readonly string _manifestPath;
    private readonly TimeSpan _commandTimeout;

    public FirmwareCompatibilityService(string? appDirectory = null, TimeSpan? commandTimeout = null)
    {
        _appDirectory = appDirectory ?? AppContext.BaseDirectory;
        _esptoolPath = Path.Combine(_appDirectory, "tools", "esptool", "esptool.exe");
        _manifestPath = Path.Combine(_appDirectory, "firmware", "manifest.json");
        _commandTimeout = commandTimeout ?? TimeSpan.FromSeconds(25);
    }

    public FirmwareManifest LoadManifest() => FirmwareManifest.Load(_manifestPath);

    public IReadOnlyList<string> GetCandidatePorts(string? preferredPort = null)
    {
        var ports = SerialPort.GetPortNames()
            .OrderBy(PortNumber)
            .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrWhiteSpace(preferredPort))
        {
            ports.RemoveAll(x => x.Equals(preferredPort, StringComparison.OrdinalIgnoreCase));
            ports.Insert(0, preferredPort);
        }
        return ports;
    }

    /// <summary>
    /// Tries the preferred port first, then all other COM ports. A port is
    /// accepted only if esptool succeeds and the chip and flash match manifest.json.
    /// </summary>
    public async Task<FirmwareDeviceInfo> CheckAsync(string? preferredPort = null, CancellationToken ct = default)
    {
        EnsureAssets();
        var manifest = LoadManifest();
        var ports = GetCandidatePorts(preferredPort);

        if (ports.Count == 0)
            return Failure("", manifest, "No serial ports were found.");

        var attempts = new List<string>();
        FirmwareDeviceInfo? bestDiagnostic = null;

        foreach (var port in ports)
        {
            ct.ThrowIfCancellationRequested();
            var info = await CheckPortAsync(port, manifest, ct);
            if (info.IsCompatible) return info;

            bestDiagnostic ??= info;
            attempts.Add($"{port}: {string.Join("; ", info.Errors)}");
        }

        return (bestDiagnostic ?? Failure("", manifest, "No compatible device was found.")) with
        {
            Errors = new[] { "No compatible ATOM was found." }.Concat(attempts).ToArray()
        };
    }

    public async Task<FirmwareDeviceInfo> CheckPortAsync(
        string portName, FirmwareManifest? manifest = null, CancellationToken ct = default)
    {
        EnsureAssets();
        manifest ??= LoadManifest();

        var chipResult = await RunEsptoolAsync(portName, "chip_id", ct);
        if (chipResult.ExitCode != 0)
            return Failure(portName, manifest,
                $"esptool could not identify the device ({ShortError(chipResult.Output)}).") with
                { ChipIdOutput = chipResult.Output };

        var chip = Match(ChipRegex(), chipResult.Output, "chip");
        var revision = Match(RevisionRegex(), chipResult.Output, "revision");
        var mac = Match(MacRegex(), chipResult.Output, "mac");
        var chipMatches = manifest.SupportsChip(chip);

        if (!chipMatches)
            return Failure(portName, manifest,
                $"Unsupported chip. Detected: {chip ?? "Unknown"}. Expected: {string.Join(", ", manifest.CompatibleChips)}.") with
                {
                    Chip = chip,
                    Revision = revision,
                    MacAddress = mac,
                    ChipMatches = false,
                    ChipIdOutput = chipResult.Output
                };

        var flashResult = await RunEsptoolAsync(portName, "flash_id", ct);
        if (flashResult.ExitCode != 0)
            return Failure(portName, manifest,
                $"esptool could not read the flash ({ShortError(flashResult.Output)}).") with
                {
                    Chip = chip,
                    Revision = revision,
                    MacAddress = mac,
                    ChipMatches = true,
                    ChipIdOutput = chipResult.Output,
                    FlashIdOutput = flashResult.Output
                };

        var flashBytes = ParseFlashBytes(flashResult.Output);
        var flashMatches = flashBytes is int size && size >= manifest.MinimumFlashBytes;
        var errors = flashMatches
            ? Array.Empty<string>()
            : new[] { $"Flash is too small or unknown. Required: {BytesToMb(manifest.MinimumFlashBytes):0.#} MB; detected: " +
                      (flashBytes is int b ? $"{BytesToMb(b):0.#} MB." : "Unknown.") };

        return new FirmwareDeviceInfo
        {
            PortName = portName,
            Chip = chip,
            Revision = revision,
            MacAddress = mac,
            FlashSizeBytes = flashBytes,
            FlashManufacturer = Match(ManufacturerRegex(), flashResult.Output, "value"),
            FlashDeviceId = Match(DeviceIdRegex(), flashResult.Output, "value"),
            AvailableFirmwareVersion = manifest.FirmwareVersion,
            FirmwareFile = manifest.FirmwareFile,
            ChipMatches = true,
            FlashMatches = flashMatches,
            Errors = errors,
            ChipIdOutput = chipResult.Output,
            FlashIdOutput = flashResult.Output
        };
    }

    private void EnsureAssets()
    {
        if (!File.Exists(_esptoolPath))
            throw new FileNotFoundException("The packaged esptool.exe was not found.", _esptoolPath);
        if (!File.Exists(_manifestPath))
            throw new FileNotFoundException("The packaged firmware manifest was not found.", _manifestPath);
    }

    private async Task<ProcessResult> RunEsptoolAsync(string port, string command, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_commandTimeout);

        var start = new ProcessStartInfo
        {
            FileName = _esptoolPath,
            WorkingDirectory = Path.GetDirectoryName(_esptoolPath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("--chip");
        start.ArgumentList.Add("esp32");
        start.ArgumentList.Add("--port");
        start.ArgumentList.Add(port);
        start.ArgumentList.Add("--no-stub");
        start.ArgumentList.Add(command);

        using var process = new Process { StartInfo = start };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        try
        {
            if (!process.Start()) return new ProcessResult(-1, "esptool.exe could not be started.");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(timeout.Token);
            process.WaitForExit(); // flush async output handlers
            return new ProcessResult(process.ExitCode, Combine(stdout, stderr));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            TryKill(process);
            return new ProcessResult(-2, $"esptool timed out after {_commandTimeout.TotalSeconds:0} seconds.");
        }
        catch
        {
            TryKill(process);
            throw;
        }
    }

    private static FirmwareDeviceInfo Failure(string port, FirmwareManifest manifest, string error) => new()
    {
        PortName = port,
        AvailableFirmwareVersion = manifest.FirmwareVersion,
        FirmwareFile = manifest.FirmwareFile,
        Errors = new[] { error }
    };

    private static int? ParseFlashBytes(string output)
    {
        var match = FlashSizeRegex().Match(output);
        if (!match.Success || !double.TryParse(match.Groups["size"].Value,
                NumberStyles.Float, CultureInfo.InvariantCulture, out var size)) return null;

        var unit = match.Groups["unit"].Value.ToUpperInvariant();
        return unit switch
        {
            "MB" => checked((int)(size * 1024 * 1024)),
            "KB" => checked((int)(size * 1024)),
            _ => null
        };
    }

    private static string? Match(Regex regex, string value, string group) =>
        regex.Match(value) is { Success: true } match ? match.Groups[group].Value.Trim() : null;

    private static string Combine(StringBuilder stdout, StringBuilder stderr) =>
        (stdout.ToString() + stderr.ToString()).Trim();

    private static string ShortError(string output)
    {
        var line = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault(x => x.Contains("error", StringComparison.OrdinalIgnoreCase)
                             || x.Contains("failed", StringComparison.OrdinalIgnoreCase)
                             || x.Contains("fatal", StringComparison.OrdinalIgnoreCase));
        return line?.Trim() ?? output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault()?.Trim() ?? "no output";
    }

    private static double BytesToMb(int bytes) => bytes / 1024d / 1024d;
    private static int PortNumber(string port) =>
        int.TryParse(new string(port.Where(char.IsDigit).ToArray()), out var n) ? n : int.MaxValue;

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
    }

    private sealed record ProcessResult(int ExitCode, string Output);

    [GeneratedRegex(@"Chip is\s+(?<chip>[^\r\n(]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ChipRegex();

    [GeneratedRegex(@"revision\s+(?<revision>v?[0-9.]+)", RegexOptions.IgnoreCase)]
    private static partial Regex RevisionRegex();

    [GeneratedRegex(@"MAC:\s*(?<mac>[0-9a-f:]{17})", RegexOptions.IgnoreCase)]
    private static partial Regex MacRegex();

    [GeneratedRegex(@"Detected flash size:\s*(?<size>[0-9.]+)\s*(?<unit>MB|KB)", RegexOptions.IgnoreCase)]
    private static partial Regex FlashSizeRegex();

    [GeneratedRegex(@"Manufacturer:\s*(?<value>0x[0-9a-f]+|[0-9a-f]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ManufacturerRegex();

    [GeneratedRegex(@"Device:\s*(?<value>0x[0-9a-f]+|[0-9a-f]+)", RegexOptions.IgnoreCase)]
    private static partial Regex DeviceIdRegex();
}
