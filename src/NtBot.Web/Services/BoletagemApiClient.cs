using NtBot.Web.Models;

namespace NtBot.Web.Services;

public class BoletagemApiClient : AuthenticatedApiClient
{
    public BoletagemApiClient(IHttpClientFactory httpClientFactory, AuthSession session)
        : base(httpClientFactory, session) { }

    public Task<BoletaSessionModel?> GetAsync(string symbol) =>
        GetAsync<BoletaSessionModel>(
            $"api/boletagem/{Uri.EscapeDataString(symbol)}",
            authenticated: true,
            timeout: TimeSpan.FromSeconds(8));

    public Task<List<string>?> GetStrategiesAsync() =>
        GetAsync<List<string>>("api/boletagem/strategies", authenticated: true);

    public async Task<BoletaSessionModel?> UpsertAsync(UpsertBoletaModel request)
    {
        var client = CreateClient(authenticated: true);
        var response = await client.PostAsJsonAsync("api/boletagem", request);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<BoletaSessionModel>();
    }

    public async Task<List<BoletaStrategyPlanModel>?> PreviewAsync(UpsertBoletaModel request)
    {
        var client = CreateClient(authenticated: true);
        var response = await client.PostAsJsonAsync("api/boletagem/preview", request);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<List<BoletaStrategyPlanModel>>();
    }

    public async Task<(BoletaSessionModel? Session, string? Error)> ExecuteAsync(ExecuteBoletaModel request)
    {
        var client = CreateClient(authenticated: true);
        var response = await client.PostAsJsonAsync("api/boletagem/execute", request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            return (null, ExtractMessage(body) ?? $"Erro {(int)response.StatusCode}");
        }
        var session = await response.Content.ReadFromJsonAsync<BoletaSessionModel>();
        return (session, null);
    }

    public async Task<(BoletaSessionModel? Session, string? Error)> CloseAsync(string symbol, string reason = "Fechamento manual")
    {
        var client = CreateClient(authenticated: true);
        var response = await client.PostAsync(
            $"api/boletagem/{Uri.EscapeDataString(symbol)}/close?reason={Uri.EscapeDataString(reason)}",
            null);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            return (null, ExtractMessage(body) ?? "Falha ao fechar");
        }
        return (await response.Content.ReadFromJsonAsync<BoletaSessionModel>(), null);
    }

    public async Task<BoletaSessionModel?> MonitorAsync(string symbol)
    {
        var client = CreateClient(authenticated: true);
        var response = await client.PostAsync($"api/boletagem/{Uri.EscapeDataString(symbol)}/monitor", null);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<BoletaSessionModel>();
    }

    private static string? ExtractMessage(string body)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var msg))
                return msg.GetString();
        }
        catch { /* ignore */ }
        return null;
    }
}
