using System.Text.Json;

namespace BusyLight.Tray;

public sealed record AppConfig(string TenantId = "", string ClientId = "", int PollSeconds = 20,
    int SerialBaudRate = 115200, byte Brightness = 100)
{
    // Solo se usa Teams si ambos IDs son GUID válidos (no "YOUR_CLIENT_ID")
    public bool IsGraphConfigured =>
        Guid.TryParse(TenantId, out _) && Guid.TryParse(ClientId, out _);

    public static AppConfig Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path)) return new AppConfig();

        try
        {
            return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path),
                       new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }
}