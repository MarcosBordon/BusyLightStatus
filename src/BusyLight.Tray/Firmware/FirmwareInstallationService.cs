using System.Diagnostics;
using System.IO.Ports;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BusyLight.Tray.Firmware;

/// <summary>
/// Installs the packaged merged firmware at address 0x0 and verifies it by
/// reconnecting over serial and checking BUSYLIGHT-ATOM plus the expected version.
/// The caller must execute this service inside SerialDeviceService.WithPortReleasedAsync.
/// </summary>
public sealed partial class FirmwareInstallationService
{
    private const int VerifyAttempts = 15;

    private readonly string _appDirectory;
    private readonly string _esptoolPath;
    private readonly string _manifestPath;
    private readonly FirmwareCompatibilityService _compatibility;
    private readonly TimeSpan _flashTimeout;

    public FirmwareInstallationService(
        FirmwareCompatibilityService compatibility,
        string? appDirectory = null,
        TimeSpan? flashTimeout = null)
    {
        _compatibility = compatibility;
        _appDirectory = appDirectory ?? AppContext.BaseDirectory;
        _esptoolPath = Path.Combine(_appDirectory, "tools", "esptool", "esptool.exe");
        _manifestPath = Path.Combine(_appDirectory, "firmware", "manifest.json");
        _flashTimeout = flashTimeout ?? TimeSpan.FromMinutes(3);
    }

    public async Task<FirmwareInstallResult> InstallAsync(
        FirmwareDeviceInfo previouslyValidated,
        IProgress<FirmwareInstallProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(previouslyValidated);
        EnsureAssets();

        var manifest = FirmwareManifest.Load(_manifestPath);
        var port = previouslyValidated.PortName;
        if (string.IsNullOrWhiteSpace(port))
            return FirmwareInstallResult.Failed("", "The validated device does not have a COM port.");

        progress?.Report(new(2, "Validating the device again…"));

        // Security: validate again immediately before write_flash and require the
        // same device (MAC) that was shown in the confirmation dialog.
        var current = await _compatibility.CheckPortAsync(port, manifest, ct);
        if (!current.IsCompatible)
            return FirmwareInstallResult.Failed(port,
                "The device is no longer compatible. Firmware installation was blocked.");

        if (!string.IsNullOrWhiteSpace(previouslyValidated.MacAddress) &&
            !string.Equals(previouslyValidated.MacAddress, current.MacAddress,
                StringComparison.OrdinalIgnoreCase))
        {
            return FirmwareInstallResult.Failed(port,
                "The device connected to the port changed after validation. Firmware installation was blocked.");
        }

        var firmwarePath = Path.Combine(_appDirectory, "firmware", manifest.FirmwareFile);
        if (!File.Exists(firmwarePath))
            return FirmwareInstallResult.Failed(port, "The packaged firmware file was not found.");

        if (!string.IsNullOrWhiteSpace(manifest.Sha256))
        {
            progress?.Report(new(5, "Checking firmware integrity…"));
            var actualHash = await ComputeSha256Async(firmwarePath, ct);
            if (!actualHash.Equals(NormalizeHash(manifest.Sha256), StringComparison.OrdinalIgnoreCase))
                return FirmwareInstallResult.Failed(port,
                    "Firmware integrity validation failed. Installation was blocked.");
        }

        progress?.Report(new(8, "Preparing firmware installation…"));
        var flash = await RunWriteFlashAsync(port, firmwarePath, manifest.FlashOffset, manifest.FlashBaud, progress, ct);

        if (flash.ExitCode != 0)
            return FirmwareInstallResult.Failed(port,
                $"Firmware installation failed: {ShortError(flash.Output)}", flash.Output);

        progress?.Report(new(94, "Restarting the ATOM…"));
        var installedVersion = await VerifyFirmwareAsync(port, manifest.FirmwareVersion, progress, ct);
        if (installedVersion is null)
        {
            return new FirmwareInstallResult
            {
                Success = false,
                PortName = port,
                ExpectedVersion = manifest.FirmwareVersion,
                Message = "The firmware was written, but BusyLightStatus could not verify the device after restart.",
                EsptoolOutput = flash.Output
            };
        }

        progress?.Report(new(100, "Firmware installation completed."));
        return new FirmwareInstallResult
        {
            Success = true,
            PortName = port,
            ExpectedVersion = manifest.FirmwareVersion,
            InstalledVersion = installedVersion,
            Message = $"BusyLightStatus firmware {installedVersion} was installed and verified successfully.",
            EsptoolOutput = flash.Output
        };
    }

    private async Task<ProcessResult> RunWriteFlashAsync(
        string port,
        string firmwarePath,
        string flashOffset,
        int flashBaud,
        IProgress<FirmwareInstallProgress>? progress,
        CancellationToken ct)

    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_flashTimeout);

        var start = new ProcessStartInfo
        {
            FileName = _esptoolPath,
            WorkingDirectory = Path.GetDirectoryName(_esptoolPath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in new[]
        {
            "--chip", "esp32",
            "--port", port,
            "--baud", flashBaud.ToString(),
            "--before", "default_reset",
            "--after", "hard_reset",
            "write_flash", "-z", flashOffset, firmwarePath
        }) start.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = start };
        var output = new StringBuilder();

        void Capture(string? line)
        {
            if (line is null) return;
            lock (output) output.AppendLine(line);

            var match = WritePercentRegex().Match(line);
            if (match.Success && int.TryParse(match.Groups["percent"].Value, out var written))
            {
                // Keep 0-93 for esptool; 94-100 is restart and verification.
                var mapped = 10 + (int)Math.Round(written * 0.83);
                progress?.Report(new(Math.Clamp(mapped, 10, 93), "Writing firmware…", line));
            }
            else if (line.Contains("Connecting", StringComparison.OrdinalIgnoreCase))
                progress?.Report(new(10, "Connecting to the ATOM…", line));
            else if (line.Contains("Hash of data verified", StringComparison.OrdinalIgnoreCase))
                progress?.Report(new(93, "Firmware data verified.", line));
        }

        process.OutputDataReceived += (_, e) => Capture(e.Data);
        process.ErrorDataReceived += (_, e) => Capture(e.Data);

        try
        {
            if (!process.Start()) return new(-1, "esptool.exe could not be started.");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(timeout.Token);
            process.WaitForExit();
            return new(process.ExitCode, output.ToString().Trim());
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            TryKill(process);
            return new(-2, $"Firmware installation timed out after {_flashTimeout.TotalSeconds:0} seconds.");
        }
        catch
        {
            TryKill(process);
            throw;
        }
    }

    private static async Task<string?> VerifyFirmwareAsync(
        string portName,
        string expectedVersion,
        IProgress<FirmwareInstallProgress>? progress,
        CancellationToken ct)
    {
        progress?.Report(new(96, "Verifying the installed firmware…"));

        for (var attempt = 1; attempt <= VerifyAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(attempt == 1 ? 2000 : 800, ct);

            try
            {
                using var port = new SerialPort(portName, 115200)
                {
                    NewLine = "\n",
                    ReadTimeout = 1200,
                    WriteTimeout = 1200,
                    DtrEnable = false,
                    RtsEnable = false
                };

                port.Open();
                await Task.Delay(250, ct);
                port.DiscardInBuffer();
                port.WriteLine("{\"cmd\":\"identify\"}");

                for (var lineNumber = 0; lineNumber < 4; lineNumber++)
                {
                    string line;
                    try { line = await Task.Run(port.ReadLine, ct); }
                    catch (TimeoutException) { break; }

                    var version = ParseIdentify(line);
                    if (version is null) continue;
                    if (version.Equals(expectedVersion, StringComparison.OrdinalIgnoreCase)) return version;

                    // A BusyLight device answered, but it did not boot the expected image.
                    return null;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { /* USB may still be re-enumerating; retry */ }

            progress?.Report(new(96 + Math.Min(3, attempt / 5),
                $"Waiting for the ATOM to restart ({attempt}/{VerifyAttempts})…"));
        }

        return null;
    }

    private static string? ParseIdentify(string line)
    {
        try
        {
            using var json = JsonDocument.Parse(line);
            var root = json.RootElement;
            if (!root.TryGetProperty("device", out var device) ||
                !string.Equals(device.GetString(), "BUSYLIGHT-ATOM", StringComparison.OrdinalIgnoreCase))
                return null;

            return root.TryGetProperty("firmware", out var firmware)
                ? firmware.GetString()
                : null;
        }
        catch { return null; }
    }

    private void EnsureAssets()
    {
        if (!File.Exists(_esptoolPath))
            throw new FileNotFoundException("The packaged esptool.exe was not found.", _esptoolPath);
        if (!File.Exists(_manifestPath))
            throw new FileNotFoundException("The packaged firmware manifest was not found.", _manifestPath);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash);
    }

    private static string NormalizeHash(string hash) =>
        hash.Replace(" ", "").Replace("-", "").Trim();

    private static string ShortError(string output)
    {
        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        return lines.LastOrDefault(x => x.Contains("error", StringComparison.OrdinalIgnoreCase)
                                     || x.Contains("failed", StringComparison.OrdinalIgnoreCase)
                                     || x.Contains("fatal", StringComparison.OrdinalIgnoreCase))
            ?? lines.LastOrDefault()
            ?? "No output from esptool.";
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
    }

    private sealed record ProcessResult(int ExitCode, string Output);

    [GeneratedRegex(@"Writing at .*\((?<percent>\d+)\s*%\)", RegexOptions.IgnoreCase)]
    private static partial Regex WritePercentRegex();
}
