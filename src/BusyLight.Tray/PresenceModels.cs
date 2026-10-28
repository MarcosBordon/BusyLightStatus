namespace BusyLight.Tray;

public sealed record GraphPresence(string? Id, string Availability, string Activity);

// Comando existente: matriz completa (modo automático)
public sealed record DeviceCommand(string Cmd, string Availability, string Activity,
    string Color, string Effect, byte Brightness);

// Comando nuevo: patrón semáforo (FW 1.2.0+)
public sealed record LightsCommand(string Cmd, string Pattern, string Top, string Middle,
    string Bottom, string Effect, byte Brightness);

// Matriz con patrón opcional ("rounded" = esquinas apagadas), FW 1.3.1+
public sealed record PatternStatusCommand(string Cmd, string Pattern, string Color,
    string Effect, byte Brightness);

// Comando nuevo: renueva el watchdog sin cambiar los LED (FW 1.2.0+)
public sealed record PingCommand(string Cmd);

public enum AppMode { Auto, Manual }

public enum ManualState { Available, Busy, InAMeeting, DoNotDisturb, Presenting, Away }