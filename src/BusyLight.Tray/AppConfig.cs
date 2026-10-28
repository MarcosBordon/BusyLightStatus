using System.Text.Json;

namespace BusyLight.Tray;

public sealed record AppConfig(string TenantId, string ClientId, int PollSeconds = 20,
    int SerialBaudRate = 115200, byte Brightness = 20)
{
    public static AppConfig Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return config ?? throw new InvalidOperationException("No se pudo leer appsettings.json");
    }
}
