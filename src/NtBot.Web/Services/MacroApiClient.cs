using System.Net.Http.Json;
using System.Text.Json.Serialization;
using NtBot.Web.Models;

namespace NtBot.Web.Services;

public class MacroApiClient : AuthenticatedApiClient
{
    public MacroApiClient(IHttpClientFactory httpClientFactory, AuthSession session)
        : base(httpClientFactory, session) { }

    public Task<MacroSnapshotModel?> GetCurrentAsync(string? symbol = null) =>
        GetAsync<MacroSnapshotModel>(
            string.IsNullOrWhiteSpace(symbol)
                ? "api/macro/current"
                : $"api/macro/current?symbol={Uri.EscapeDataString(symbol)}",
            authenticated: true);

    public Task<List<MacroRecommendationModel>?> GetRecommendationsAsync(string? symbol = null) =>
        GetAsync<List<MacroRecommendationModel>>(
            string.IsNullOrWhiteSpace(symbol)
                ? "api/macro/recommendations"
                : $"api/macro/recommendations?symbol={Uri.EscapeDataString(symbol)}",
            authenticated: true);

    public Task<List<MacroProviderStatusModel>?> GetProvidersAsync() =>
        GetAsync<List<MacroProviderStatusModel>>("api/macro/providers", authenticated: true);

    public Task<List<MacroCalendarEventModel>?> GetCalendarAsync() =>
        GetAsync<List<MacroCalendarEventModel>>("api/macro/calendar", authenticated: true);

    public async Task EnableProviderAsync(Guid id)
    {
        var client = CreateClient(authenticated: true);
        await client.PostAsync($"api/macro/provider/{id}/enable", null);
    }

    public async Task DisableProviderAsync(Guid id)
    {
        var client = CreateClient(authenticated: true);
        await client.PostAsync($"api/macro/provider/{id}/disable", null);
    }

    public async Task ConfigureProviderAsync(Guid id, MacroProviderConfigureModel request)
    {
        var client = CreateClient(authenticated: true);
        await client.PostAsJsonAsync($"api/macro/provider/{id}/configure", request);
    }

    public async Task<int> SyncCalendarAsync()
    {
        var client = CreateClient(authenticated: true);
        var response = await client.PostAsync("api/macro/sync-calendar", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CalendarSyncResult>();
        return result?.Synced ?? 0;
    }

    private sealed class CalendarSyncResult
    {
        [JsonPropertyName("synced")]
        public int Synced { get; set; }

        [JsonPropertyName("total")]
        public int Total { get; set; }
    }
}
