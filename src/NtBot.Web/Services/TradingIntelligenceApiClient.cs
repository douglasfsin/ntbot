using NtBot.Web.Models;

namespace NtBot.Web.Services;

public class TradingIntelligenceApiClient : AuthenticatedApiClient
{
    public TradingIntelligenceApiClient(IHttpClientFactory httpClientFactory, AuthSession session)
        : base(httpClientFactory, session) { }

    public Task<TradingIntelligenceSnapshotModel?> GetSnapshotAsync(string symbol) =>
        GetAsync<TradingIntelligenceSnapshotModel>($"api/trading-intelligence/{Uri.EscapeDataString(symbol)}", authenticated: true);

    public Task<List<TradingIntelligenceDashboardItemModel>?> GetDashboardAsync() =>
        GetAsync<List<TradingIntelligenceDashboardItemModel>>("api/trading-intelligence/dashboard", authenticated: true);

    public Task<TradingIntelligenceStatusModel?> GetStatusAsync() =>
        GetAsync<TradingIntelligenceStatusModel>("api/trading-intelligence/status", authenticated: true);

    public async Task<List<ChartCandleModel>> GetChartCandlesAsync(string symbol, string timeframe, int count = 80)
    {
        var response = await GetAsync<ChartCandlesResponse>(
            $"api/trading-intelligence/{Uri.EscapeDataString(symbol)}/candles?timeframe={Uri.EscapeDataString(timeframe)}&count={count}",
            authenticated: true);
        return response?.Candles ?? [];
    }

    public async Task<List<SmcChartZoneModel>> GetSmcOverlaysAsync(string symbol, string timeframe, int count = 120)
    {
        var response = await GetAsync<SmcOverlaysResponse>(
            $"api/trading-intelligence/{Uri.EscapeDataString(symbol)}/smc-overlays?timeframe={Uri.EscapeDataString(timeframe)}&count={count}",
            authenticated: true);
        return response?.Overlays ?? [];
    }

    public async Task<bool> RefreshAsync(string? symbol = null)
    {
        var url = string.IsNullOrWhiteSpace(symbol)
            ? "api/trading-intelligence/refresh"
            : $"api/trading-intelligence/refresh?symbol={Uri.EscapeDataString(symbol)}";
        var client = CreateClient(authenticated: true);
        var response = await client.PostAsync(url, null);
        return response.IsSuccessStatusCode;
    }
}

public class DriverCompositionApiClient : AuthenticatedApiClient
{
    public DriverCompositionApiClient(IHttpClientFactory httpClientFactory, AuthSession session)
        : base(httpClientFactory, session) { }

    public Task<List<DriverCompositionModel>?> ListAsync(string targetAsset) =>
        GetAsync<List<DriverCompositionModel>>($"api/driver-compositions/{Uri.EscapeDataString(targetAsset)}", authenticated: true);

    public async Task<DriverCompositionModel?> CreateAsync(DriverCompositionUpsertModel request)
    {
        var client = CreateClient(authenticated: true);
        var response = await client.PostAsJsonAsync("api/driver-compositions", request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<DriverCompositionModel>()
            : null;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var client = CreateClient(authenticated: true);
        var response = await client.DeleteAsync($"api/driver-compositions/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<int> DuplicateAsync(string sourceAsset, string targetAsset)
    {
        var client = CreateClient(authenticated: true);
        var response = await client.PostAsJsonAsync("api/driver-compositions/duplicate",
            new { sourceAsset, targetAsset });
        if (!response.IsSuccessStatusCode) return 0;
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>();
        return payload?.GetValueOrDefault("copied") ?? 0;
    }

    public async Task<bool> ReorderAsync(string targetAsset, IReadOnlyList<Guid> orderedIds)
    {
        var client = CreateClient(authenticated: true);
        var response = await client.PostAsJsonAsync("api/driver-compositions/reorder",
            new { targetAsset, orderedIds });
        return response.IsSuccessStatusCode;
    }

    public async Task<DriverCompositionModel?> UpdateAsync(Guid id, DriverCompositionUpsertModel request)
    {
        var client = CreateClient(authenticated: true);
        var response = await client.PutAsJsonAsync($"api/driver-compositions/{id}", request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<DriverCompositionModel>()
            : null;
    }
}
