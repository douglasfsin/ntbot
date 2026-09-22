using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Marketdata.Database.Models;
using Marketdata.Database.Services;
using MarketData.API.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProfitDLLClient;

namespace MarketData.API.Services;

public class ProfitHistoricalTradeProvider : IMarketDataHistoryProvider
{
    private static readonly SemaphoreSlim HistoryGate = new(1, 1);

    private readonly ILogger<ProfitHistoricalTradeProvider> _logger;
    private readonly ProfitDllSettings _settings;
    private readonly ConcurrentDictionary<string, PendingHistoryRequest> _pendingRequests = new();

    public ProfitHistoricalTradeProvider(
        ILogger<ProfitHistoricalTradeProvider> logger,
        IOptions<ProfitDllSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    public Task<List<HistoricalTradeDto>> GetHistoricalTradesAsync(string ticker, DateTime date, CancellationToken cancellationToken = default) =>
        GetHistoricalTradesAsync(ticker, date, date, cancellationToken);

    public async Task<List<HistoricalTradeDto>> GetHistoricalTradesAsync(
        string ticker,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var from = startDate.Date;
        var to = endDate.Date;
        if (to < from)
            (from, to) = (to, from);

        if (to > DateTime.Today)
            to = DateTime.Today;
        if (from > to)
            from = to;

        var timeout = ResolveTimeout(ticker);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        await HistoryGate.WaitAsync(timeoutCts.Token);
        try
        {
            return await FetchHistoryCoreAsync(ticker, from, to, timeoutCts.Token);
        }
        finally
        {
            HistoryGate.Release();
        }
    }

    private async Task<List<HistoricalTradeDto>> FetchHistoryCoreAsync(
        string ticker,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        var exchange = ResolveExchange(ticker);
        var startString = from.ToString("dd/MM/yyyy");
        var endString = to.ToString("dd/MM/yyyy");

        _logger.LogInformation(
            "Requesting History Trades from ProfitDLL for {Ticker}@{Exchange} from {Start} to {End}",
            ticker,
            exchange,
            startString,
            endString);

        var request = new PendingHistoryRequest();

        if (_pendingRequests.TryRemove(ticker, out var oldRequest))
            oldRequest.Tcs.TrySetCanceled(CancellationToken.None);

        _pendingRequests[ticker] = request;

        var result = ProfitDLL.GetHistoryTrades(ticker, exchange, startString, endString);

        if (result != (int)NResult.NL_OK)
        {
            _pendingRequests.TryRemove(ticker, out _);

            if (IsRecoverableError(result))
            {
                _logger.LogWarning(
                    "GetHistoryTrades sem dados para {Ticker}@{Exchange}: {Result}",
                    ticker,
                    exchange,
                    (NResult)result);
                return [];
            }

            _logger.LogError(
                "GetHistoryTrades returned an error for {Ticker}@{Exchange}: {Result}",
                ticker,
                exchange,
                (NResult)result);
            return [];
        }

        try
        {
            var completedTask = await Task.WhenAny(
                request.Tcs.Task,
                Task.Delay(Timeout.Infinite, cancellationToken));

            if (completedTask != request.Tcs.Task)
            {
                _logger.LogWarning(
                    "Timeout/cancel waiting for History Trades callback for {Ticker} ({Start} - {End})",
                    ticker,
                    startString,
                    endString);
                request.Tcs.TrySetCanceled(CancellationToken.None);
                return [];
            }

            return await request.Tcs.Task;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("History Trades request cancelled for {Ticker}", ticker);
            return [];
        }
        finally
        {
            _pendingRequests.TryRemove(ticker, out _);
        }
    }

    public void OnHistoryTradeCallback(TConnectorAssetIdentifier a_Asset, nint a_pTrade, TConnectorTradeCallbackFlags a_nFlags)
    {
        var ticker = a_Asset.Ticker?.Trim() ?? "";

        if (!_pendingRequests.TryGetValue(ticker, out var request))
            return;

        if (a_pTrade != IntPtr.Zero)
        {
            var trade = Marshal.PtrToStructure<TConnectorTrade>(a_pTrade);

            var dto = new HistoricalTradeDto(
                Ticker: ticker,
                Side: trade.TradeType == 2 ? "S" : "B",
                BuyAgent: trade.BuyAgent.ToString(),
                SellAgent: trade.SellAgent.ToString(),
                Price: trade.Price,
                Quantity: trade.Quantity,
                TradeType: trade.TradeType,
                TradeNumber: trade.TradeNumber,
                IsAuction: trade.TradeType == 3,
                Timestamp: SystemTime.ToDateTime(trade.TradeDate)
            );

            request.Trades.Add(dto);
        }

        if (a_nFlags.HasFlag(TConnectorTradeCallbackFlags.TC_LAST_PACKET))
        {
            _logger.LogInformation(
                "Recebido TC_LAST_PACKET para {Ticker} via History Callback. {Count} registros recebidos.",
                ticker,
                request.Trades.Count);
            request.Tcs.TrySetResult(request.Trades);
        }
    }

    private string ResolveExchange(string ticker)
    {
        var configured = _settings.Tickers.FirstOrDefault(t =>
            string.Equals(t.Code, ticker, StringComparison.OrdinalIgnoreCase));

        if (configured is not null && !string.IsNullOrWhiteSpace(configured.Bag))
            return configured.Bag;

        return ResolveExchangeFromSymbol(ticker);
    }

    private static string ResolveExchangeFromSymbol(string ticker)
    {
        var t = ticker.Trim().ToUpperInvariant();
        if (t.EndsWith("FUT", StringComparison.Ordinal))
            return "F";
        if (t.Length >= 5 && (t.EndsWith('3') || t.EndsWith('4')))
            return "B";
        return "F";
    }

    private static bool IsRecoverableError(int result) =>
        result is (int)NResult.NL_SERIE_NO_HISTORY
            or (int)NResult.NL_INVALID_ARGS
            or (int)NResult.NL_INVALID_TICKER
            or (int)NResult.NL_INVALID_SERIE;

    private static TimeSpan ResolveTimeout(string ticker)
    {
        var t = ticker.Trim().ToUpperInvariant();
        return t is "WINFUT" or "WDOFUT" or "DOLFUT" or "INDFUT"
            ? TimeSpan.FromSeconds(90)
            : TimeSpan.FromSeconds(35);
    }

    private sealed class PendingHistoryRequest
    {
        public List<HistoricalTradeDto> Trades { get; } = [];
        public TaskCompletionSource<List<HistoricalTradeDto>> Tcs { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
