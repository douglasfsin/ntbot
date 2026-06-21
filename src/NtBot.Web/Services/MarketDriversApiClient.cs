using NtBot.Web.Models;

namespace NtBot.Web.Services;

public class MarketDriversApiClient : AuthenticatedApiClient
{
    public MarketDriversApiClient(IHttpClientFactory httpClientFactory, AuthSession session)
        : base(httpClientFactory, session) { }

    public Task<MarketDriversSnapshotModel?> GetSnapshotAsync(string symbol) =>
        GetAsync<MarketDriversSnapshotModel>($"api/market-drivers/{Uri.EscapeDataString(symbol)}", authenticated: true);

    public Task<List<MarketDriversDashboardItemModel>?> GetDashboardAsync() =>
        GetAsync<List<MarketDriversDashboardItemModel>>("api/market-drivers/dashboard", authenticated: true);

    public async Task ForceSyncAsync()
    {
        var client = CreateClient(authenticated: true);
        await client.PostAsync("api/market-drivers/sync", null);
    }
}
