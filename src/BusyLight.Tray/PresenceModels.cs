namespace BusyLight.Tray;

public sealed record GraphPresence(string? Id, string Availability, string Activity);
public sealed record DeviceCommand(string Cmd, string Availability, string Activity,
    string Color, string Effect, byte Brightness);
