namespace BusyLight.Tray;

public static class PresenceMapper
{
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
}
