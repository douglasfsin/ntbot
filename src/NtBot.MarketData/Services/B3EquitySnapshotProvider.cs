using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NtBot.MarketData.Clients;
using NtBot.MarketData.Configuration;

namespace NtBot.MarketData.Services;

public interface IB3EquitySnapshotProvider
{
    Task<IReadOnlyList<B3ProfitSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default);
}

public sealed class B3ProfitSnapshot
{
    public string Symbol { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public static class B3EquityCatalog
{
    public static IReadOnlyList<(string Symbol, string Name)> Symbols { get; } =
    [
        ("PETR4", "Petrobras"),
        ("VALE3", "Vale"),
        ("ITUB4", "Itaú"),
        ("BBDC4", "Bradesco"),
        ("WEGE3", "WEG"),
        ("ABEV3", "Ambev"),
        ("WIN", "Mini Índice"),
        ("WDO", "Mini Dólar")
    ];
}

public sealed class B3EquitySnapshotProvider : IB3EquitySnapshotProvider
{
    private readonly IMarketDataApiClient _client;
    private readonly MarketDataApiOptions _options;
    private readonly ILogger<B3EquitySnapshotProvider> _logger;
    private readonly Dictionary<string, (B3ProfitSnapshot Snapshot, DateTime ExpiresUtc)> _cache = new(StringComparer.OrdinalIgnoreCase);

    public B3EquitySnapshotProvider(
        IMarketDataApiClient client,
        IOptions<MarketDataApiOptions> options,
        ILogger<B3EquitySnapshotProvider> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<B3ProfitSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.BaseUrl))
            return [];

        var results = new List<B3ProfitSnapshot>();
        foreach (var (symbol, name) in B3EquityCatalog.Symbols)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (_cache.TryGetValue(symbol, out var cached) && cached.ExpiresUtc > DateTime.UtcNow)
            {
                results.Add(cached.Snapshot);
                continue;
            }

            try
            {
                using var symbolCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                symbolCts.CancelAfter(TimeSpan.FromSeconds(3));

                var price = await _client.GetPriceAsync(symbol, symbolCts.Token);
                if (price is null || price.LastPrice <= 0)
                {
                    _logger.LogDebug("B3 snapshot indisponível para {Symbol} via Profit", symbol);
                    continue;
                }

                var snapshot = new B3ProfitSnapshot
                {
                    Symbol = symbol,
                    Name = name,
                    Price = (decimal)price.LastPrice,
                    Timestamp = price.Timestamp == default ? DateTime.UtcNow : price.Timestamp
                };

                _cache[symbol] = (snapshot, DateTime.UtcNow.AddSeconds(30));
                results.Add(snapshot);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("B3 snapshot timeout para {Symbol}", symbol);
            }
            catch (OperationCanceledException)
            {
                // Caller cancelou — devolve o que já temos.
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "B3 snapshot falhou para {Symbol}", symbol);
            }
        }

        return results;
    }
}
