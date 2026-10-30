namespace BusyLight.Tray;

// All user-facing text in one place.
public static class Strings
{
    public const string AppName = "BusyLightStatus";

    // ---------- Tray menu ----------
    public const string MenuMode            = "Mode";
    public const string MenuAuto            = "Automatic (Teams)";
    public const string MenuAutoUnavailable = "Automatic (Teams) - unavailable";
    public const string MenuManual          = "Manual Use";
    public const string MenuManualStatus    = "Manual status";
    public const string MenuCustomColor     = "Custom color…";
    public const string MenuTextMessage     = "Text message…";
    public const string MenuFirmware        = "Firmware";
    public const string MenuFirmwareCheck   = "Check device compatibility…";
    public const string MenuRefresh         = "Refresh now";
    public const string MenuReconnect       = "Reconnect device";
    public const string MenuStatus          = "Status";
    public const string MenuExit            = "Exit";

    // ---------- Manual states ----------
    public const string StateAvailable    = "Available";
    public const string StateBusy         = "Busy";
    public const string StateInAMeeting   = "In a meeting";
    public const string StateDoNotDisturb = "Do not disturb";
    public const string StatePresenting   = "Presenting";
    public const string StateAway         = "Away";
    public const string StateCustom       = "Custom color";
    public static string StateText(string message) => $"Message \"{message}\"";

    // ---------- Status and errors ----------
    public const string Starting          = "Starting";
    public const string DeviceNotFound    = "Device not found";
    public const string DeviceNotFoundUsb = "Busy Light not found on USB.";
    public const string EmptyPresence     = "Microsoft Graph returned an empty presence.";
    public const string NoEntraConfig     = "No Entra ID configuration";
    public const string NotAuthorized     = "Not authorized to read Teams presence";

    public static string StatusAuto(string availability, string activity, string? port) =>
        $"Automatic: {availability} / {activity} | {port}";
    public static string StatusManual(string label, string? port) => $"Manual: {label} | {port}";
    public static string Error(string message) => $"Error: {message}";
    public static string SignInError(string message) => $"Sign-in error: {message}";
    public static string ManualActiveStatus(string reason) => $"{reason}. Manual Use mode active.";
    public static string ManualEnabledNotice(string reason) => $"{reason}. Manual Use mode enabled.";

    // ---------- Shared buttons ----------
    public const string Apply  = "Apply";
    public const string Cancel = "Cancel";
    public const string Send   = "Send";
    public const string Close  = "Close";

    // ---------- Custom color window ----------
    public const string CustomTitle = "Custom color";
    public const string PickColor   = "Pick color…";
    public const string Brightness  = "Brightness (10-100)";
    public const string LivePreview = "Live preview on the ATOM";

    // ---------- Text message window ----------
    public const string TextTitle          = "Text message";
    public const string FixedColor         = "Fixed color";
    public const string RandomColors       = "Random colors";
    public const string TextHint           = "Each letter shows for 500 ms and the message repeats.";
    public const string PreviewEmpty       = "Preview: (empty)";
    public const string TextRequired       = "Type at least one letter or number.";
    public const string DefaultTextMessage = "IN A MEETING";
    public static string TextLabel(int max) => $"Message (max. {max} characters)";
    public static string Preview(string text) => $"Preview: {text}";

    // ---------- Snake game (hidden) ----------
    public const string EasterEggWord = "SNAKE";
    public const string GameTitle     = "Snake";
    public const string GameHelp      = "Arrow keys or WASD to play.\nEat the green apples and fill all 25 LEDs.";
    public const string GameTilt      = "Tilt control (tilt the ATOM to turn)";
    public const string GameStart     = "Start";
    public const string GameRestart   = "Restart";
    public const string GameReady     = "Press Start to play.";
    public const string GameRunning   = "Playing…";
    public const string GameOver      = "Game over!";
    public const string GameWin       = "You win! All 25 LEDs are on.";
    public const string GameNoTilt    = "Tilt sensor not available: using keys.";
    public static string GameLength(int n) => $"Length: {n} / 25";
    public static string GameBest(int n) => $"Best: {n}";

    // ---------- Firmware ----------
    public const string FirmwareCheckTitle       = "Firmware compatibility";
    public const string FirmwareChecking         = "Checking device compatibility…";
    public const string FirmwareCheckBusy         = "A firmware operation is already running.";
    public const string FirmwareNoDevice          = "No compatible ATOM was found.";
    public const string FirmwareCompatible        = "Compatible device detected";
    public const string FirmwareNotCompatible     = "Device is not compatible";
    public const string FirmwareValidationFailed  = "Firmware compatibility check failed.";
    public const string FirmwareReady             = "Ready for firmware installation.";
    public const string FirmwareInstallLater      = "Firmware installation will be added in the next stage.";
}
