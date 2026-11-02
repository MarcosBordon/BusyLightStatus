namespace BusyLight.Tray.Firmware;

public sealed record FirmwareInstallProgress(int Percent, string Message, string? Detail = null);

public sealed record FirmwareInstallResult
{
    public bool Success { get; init; }
    public string PortName { get; init; } = "";
    public string? ExpectedVersion { get; init; }
    public string? InstalledVersion { get; init; }
    public string Message { get; init; } = "";
    public string EsptoolOutput { get; init; } = "";

    public static FirmwareInstallResult Failed(string port, string message, string output = "") => new()
    {
        Success = false,
        PortName = port,
        Message = message,
        EsptoolOutput = output
    };
}
