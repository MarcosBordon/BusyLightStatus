namespace BusyLight.Tray;

public sealed record GraphPresence(string? Id, string Availability, string Activity);

// Full matrix (automatic mode)
public sealed record DeviceCommand(string Cmd, string Availability, string Activity,
    string Color, string Effect, byte Brightness);

// Traffic-light pattern (FW 1.2.0+)
public sealed record LightsCommand(string Cmd, string Pattern, string Top, string Middle,
    string Bottom, string Effect, byte Brightness);

// Renews the watchdog without changing the LEDs (FW 1.2.0+)
public sealed record PingCommand(string Cmd);

// Matrix with optional pattern ("rounded" = corners off), FW 1.3.1+
public sealed record PatternStatusCommand(string Cmd, string Pattern, string Color,
    string Effect, byte Brightness);

// Scrolls text letter by letter (FW 1.3.0+)
public sealed record TextCommand(string Cmd, string Text, string Color, bool Random, byte Brightness);

public enum AppMode { Auto, Manual }

public enum ManualState { Available, Busy, InAMeeting, DoNotDisturb, Presenting, Away, Custom, Text }