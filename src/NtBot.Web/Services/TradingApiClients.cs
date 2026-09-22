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

    protected async Task<T?> GetAsync<T>(string path, bool authenticated = false, TimeSpan? timeout = null)
    {
        var client = CreateClient(authenticated);
        using var timeoutCts = timeout is { } t ? new CancellationTokenSource(t) : null;
        var ct = timeoutCts?.Token ?? CancellationToken.None;
        try
        {
            var response = await client.GetAsync(path, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return default;
            return await response.Content.ReadFromJsonAsync<T>(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return default;
        }
    }
}

public class QuantStrategyApiClient : AuthenticatedApiClient
{
    public QuantStrategyApiClient(IHttpClientFactory httpClientFactory, AuthSession session)
        : base(httpClientFactory, session) { }

    public Task<QuantDashboardModel?> GetDashboardAsync(string symbol = "WINFUT", string leaderSymbol = "NQ") =>
        GetAsync<QuantDashboardModel>(
            $"api/quantstrategy/dashboard?symbol={Uri.EscapeDataString(symbol)}&leaderSymbol={Uri.EscapeDataString(leaderSymbol)}");

    public async Task<List<string>> GetAvailableSymbolsAsync(int minimum = 50)
    {
        var symbols = await GetAsync<List<string>>($"api/quantstrategy/symbols?minimum={minimum}");
        return symbols ?? [];
    }
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
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        try
        {
            var response = await client.GetAsync("api/profitchart/tickers", cts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return new Dictionary<string, TickerStatusModel>();
            return await response.Content.ReadFromJsonAsync<Dictionary<string, TickerStatusModel>>(cts.Token)
                   ?? new Dictionary<string, TickerStatusModel>();
        }
        catch (OperationCanceledException)
        {
            return new Dictionary<string, TickerStatusModel>();
        }
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

    public Task<HealthModel?> GetHealthAsync() =>
        GetAsync<HealthModel>("api/health", timeout: TimeSpan.FromSeconds(3));
}
