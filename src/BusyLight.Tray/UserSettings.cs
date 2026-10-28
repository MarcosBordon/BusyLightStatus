using System.Text.Json;
using System.Text.Json.Serialization;

namespace BusyLight.Tray;

public sealed class UserSettings
{
    public AppMode Mode { get; set; } = AppMode.Auto;
    public ManualState ManualState { get; set; } = ManualState.Available;

    public string CustomColor { get; set; } = "#00A0FF";
    public int CustomBrightness { get; set; } = 80;
    public string TextMessage { get; set; } = "EN REUNION";
    public string TextColor { get; set; } = "#00A0FF";
    public bool TextRandom { get; set; } = false;
    public int TextBrightness { get; set; } = 80;
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BusyLightStatus");

    private static readonly string FilePath = Path.Combine(Folder, "usersettings.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static UserSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(FilePath), Options)
                       ?? new UserSettings();
        }
        catch { /* archivo dañado: se usan valores por defecto */ }

        return new UserSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Options));
        }
        catch { /* no bloquear la app si no se puede guardar */ }
    }
}