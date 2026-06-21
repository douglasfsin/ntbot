using NtBot.Web.Models;

namespace NtBot.Web.Services;

public class MarketApiClient : AuthenticatedApiClient
{
    public MarketApiClient(IHttpClientFactory httpClientFactory, AuthSession session)
        : base(httpClientFactory, session) { }

    public Task<MarketOverviewModel?> GetOverviewAsync() =>
        GetAsync<MarketOverviewModel>("api/market/overview", authenticated: true);

    public Task<List<MarketSnapshotModel>?> GetCommoditiesAsync() =>
        GetAsync<List<MarketSnapshotModel>>("api/market/commodities", authenticated: true);

    public Task<CorrelationResultModel?> GetCorrelationAsync() =>
        GetAsync<CorrelationResultModel>("api/market/correlation", authenticated: true);

    public Task<QuantScoreModel?> GetQuantScoreAsync() =>
        GetAsync<QuantScoreModel>("api/market/quantscore", authenticated: true);

    public Task<List<MarketProviderStatusModel>?> GetProvidersAsync() =>
        GetAsync<List<MarketProviderStatusModel>>("api/market/providers", authenticated: true);

    public async Task ForceSyncAsync()
    {
        var client = CreateClient(authenticated: true);
        await client.PostAsync("api/market/sync", null);
    }

    public async Task EnableProviderAsync(Guid id)
    {
        var client = CreateClient(authenticated: true);
        await client.PostAsync($"api/market/provider/{id}/enable", null);
    }

    public async Task DisableProviderAsync(Guid id)
    {
        var client = CreateClient(authenticated: true);
        await client.PostAsync($"api/market/provider/{id}/disable", null);
    }
}
