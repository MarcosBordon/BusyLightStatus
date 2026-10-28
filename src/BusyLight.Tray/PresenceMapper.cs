namespace BusyLight.Tray;

public static class PresenceMapper
{
    // Modo automático (Teams) - sin cambios
    public static DeviceCommand Map(GraphPresence p, byte brightness)
    {
        var a = p.Activity ?? "Unknown";
        var v = p.Availability ?? "Unknown";
        var (color, effect) = a switch
        {
            "Presenting" or "UrgentInterruptionsOnly" => ("#FF00FF", "pulse"),
            "InAMeeting" or "InACall" => ("#FF0000", "pulse"),
            _ when v == "DoNotDisturb" => ("#FF0000", "doublepulse"),
            _ when v == "Busy" => ("#FF0000", "solid"),
            _ when v == "Available" => ("#00FF00", "solid"),
            _ when v is "Away" or "BeRightBack" => ("#FFC000", "solid"),
            _ when v == "Offline" => ("#101010", "solid"),
            _ => ("#0000FF", "blink")
        };
        return new DeviceCommand("setStatus", v, a, color, effect, brightness);
    }

     // Modo manual: mismos colores y efectos que Teams, brillo por estado, esquinas apagadas
    public static PatternStatusCommand MapManual(ManualState state)
    {
        var (color, effect, brightness) = state switch
        {
            ManualState.Busy         => ("#FF0000", "solid",        95),
            ManualState.InAMeeting   => ("#FF0000", "pulse",        95),
            ManualState.DoNotDisturb => ("#FF0000", "doublepulse", 100),
            ManualState.Presenting   => ("#FF00FF", "pulse",       100),
            ManualState.Away         => ("#FFC000", "solid",        70),
            _                        => ("#00FF00", "solid",        95)   // Available
        };

        return new PatternStatusCommand("setStatus", "rounded", color, effect, (byte)brightness);
    }
        // Color personalizado: esquinas apagadas, efecto fijo
    public static PatternStatusCommand MapCustom(string color, int brightness) =>
        new("setStatus", "rounded", color, "solid", (byte)Math.Clamp(brightness, 10, 100));

    // Mensaje de texto: el firmware lo muestra letra por letra y lo repite
    public static TextCommand MapText(string text, string color, bool random, int brightness) =>
        new("setText", text, color, random, (byte)Math.Clamp(brightness, 10, 100));

}