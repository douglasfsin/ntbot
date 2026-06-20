using System.Net.Http.Headers;
using System.Net.Http.Json;
using NtBot.Web.Models;

namespace NtBot.Web.Services;

public abstract class AuthenticatedApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthSession _session;

    protected AuthenticatedApiClient(IHttpClientFactory httpClientFactory, AuthSession session)
    {
        _httpClientFactory = httpClientFactory;
        _session = session;
    }

    protected HttpClient CreateClient(bool authenticated = false)
    {
        var client = _httpClientFactory.CreateClient("NtBotApi");
        if (authenticated && !string.IsNullOrEmpty(_session.Token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _session.Token);
        return client;
    }

    protected async Task<T?> GetAsync<T>(string path, bool authenticated = false)
    {
        var client = CreateClient(authenticated);
        var response = await client.GetAsync(path);
        if (!response.IsSuccessStatusCode)
            return default;
        return await response.Content.ReadFromJsonAsync<T>();
    }
}

public class QuantStrategyApiClient : AuthenticatedApiClient
{
    public QuantStrategyApiClient(IHttpClientFactory httpClientFactory, AuthSession session)
        : base(httpClientFactory, session) { }

    public Task<QuantDashboardModel?> GetDashboardAsync(string symbol = "WINFUT", string leaderSymbol = "NQ") =>
        GetAsync<QuantDashboardModel>(
            $"api/quantstrategy/dashboard?symbol={Uri.EscapeDataString(symbol)}&leaderSymbol={Uri.EscapeDataString(leaderSymbol)}");
}

public class ProfitChartApiClient : AuthenticatedApiClient
{
    public ProfitChartApiClient(IHttpClientFactory httpClientFactory, AuthSession session)
        : base(httpClientFactory, session) { }

    public Task<RtdStatisticsModel?> GetStatisticsAsync() =>
        GetAsync<RtdStatisticsModel>("api/profitchart/statistics", authenticated: true);

    public async Task<Dictionary<string, TickerStatusModel>> GetAllTickersAsync()
    {
        var client = CreateClient(authenticated: true);
        var response = await client.GetAsync("api/profitchart/tickers");
        if (!response.IsSuccessStatusCode)
            return new Dictionary<string, TickerStatusModel>();
        return await response.Content.ReadFromJsonAsync<Dictionary<string, TickerStatusModel>>()
               ?? new Dictionary<string, TickerStatusModel>();
    }

    public Task<ProfitChartHealthModel?> GetHealthAsync() =>
        GetAsync<ProfitChartHealthModel>("api/profitchart/health", authenticated: true);

    public Task<BookDataModel?> GetBookAsync(string ticker, int levels = 5) =>
        GetAsync<BookDataModel>($"api/profitchart/book/{Uri.EscapeDataString(ticker)}?levels={levels}", authenticated: true);
}

public class AnalysisApiClient : AuthenticatedApiClient
{
    public AnalysisApiClient(IHttpClientFactory httpClientFactory, AuthSession session)
        : base(httpClientFactory, session) { }

    public Task<WyckoffAnalysisModel?> GetWyckoffAsync(string symbol, string timeframe = "5m") =>
        GetAsync<WyckoffAnalysisModel>(
            $"api/analysis/wyckoff/{Uri.EscapeDataString(symbol)}?timeframe={Uri.EscapeDataString(timeframe)}");

    public Task<MacroContextModel?> GetMacroAsync(string symbol = "MNQ") =>
        GetAsync<MacroContextModel>($"api/analysis/macro/{Uri.EscapeDataString(symbol)}");
}

public class HealthApiClient : AuthenticatedApiClient
{
    public HealthApiClient(IHttpClientFactory httpClientFactory, AuthSession session)
        : base(httpClientFactory, session) { }

    public Task<HealthModel?> GetHealthAsync() => GetAsync<HealthModel>("api/health");
}
