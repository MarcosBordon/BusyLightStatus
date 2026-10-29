using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Identity.Client;

namespace BusyLight.Tray;

public sealed class GraphPresenceService
{
    private static readonly string[] Scopes = ["Presence.Read"];
    private readonly IPublicClientApplication _app;
    private readonly HttpClient _http = new() { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };

    public GraphPresenceService(AppConfig config)
    {
        _app = PublicClientApplicationBuilder.Create(config.ClientId)
            .WithTenantId(config.TenantId)
            .WithDefaultRedirectUri()
            .Build();
    }

    public async Task SignInAsync(CancellationToken ct = default)
    {
        var accounts = await _app.GetAccountsAsync();
        try
        {
            await _app.AcquireTokenSilent(Scopes, accounts.FirstOrDefault()).ExecuteAsync(ct);
        }
        catch (MsalUiRequiredException)
        {
            await _app.AcquireTokenInteractive(Scopes)
                .WithAccount(accounts.FirstOrDefault())
                .WithPrompt(Prompt.SelectAccount)
                .ExecuteAsync(ct);
        }
    }

    public async Task<GraphPresence> GetMyPresenceAsync(CancellationToken ct = default)
    {
        var accounts = await _app.GetAccountsAsync();
        AuthenticationResult token;
        try
        {
            token = await _app.AcquireTokenSilent(Scopes, accounts.FirstOrDefault()).ExecuteAsync(ct);
        }
        catch (MsalUiRequiredException)
        {
            token = await _app.AcquireTokenInteractive(Scopes).ExecuteAsync(ct);
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, "me/presence");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        using var response = await _http.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<GraphPresence>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException(Strings.EmptyPresence);
    }
}
