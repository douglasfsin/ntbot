using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using NtBot.Api.Configuration;
using NtBot.Api.Hubs;

namespace NtBot.Api.Services.Boletagem;

public interface IMt5TradeGateway
{
    Task<Mt5TradeResult> SendMarketOrderAsync(
        string symbol,
        string direction,
        decimal volume,
        decimal? stopLoss,
        decimal? takeProfit,
        string comment,
        CancellationToken ct = default);

    Task<Mt5TradeResult> CloseSymbolPositionsAsync(
        string symbol,
        decimal? volume = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<Mt5PositionSnapshot>> GetPositionsAsync(
        string? symbol = null,
        CancellationToken ct = default);

    Task<decimal?> GetAccountBalanceAsync(CancellationToken ct = default);

    Task<decimal?> GetLastPriceAsync(string symbol, CancellationToken ct = default);

    /// <summary>Tick size do símbolo via MT5 (trade_tick_size ou point).</summary>
    Task<decimal?> GetTickSizeAsync(string symbol, CancellationToken ct = default);

    Task<Mt5TradeResult> ModifyPositionStopAsync(
        long ticket,
        string symbol,
        decimal stopLoss,
        decimal? takeProfit = null,
        CancellationToken ct = default);

    /// <summary>Verifica se o MT5 está pronto para receber ordens (conexão + AutoTrading).</summary>
    Task<Mt5Readiness> GetReadinessAsync(CancellationToken ct = default);
}

public sealed record Mt5Readiness(bool Ready, string Message);

/// <summary>
/// Gateway de execução MT5: tenta HTTP no host Python (:8228);
/// se indisponível, enfileira comando via SignalR para o EA TradeAssistant.
/// </summary>
public sealed class Mt5TradeGateway : IMt5TradeGateway
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHubContext<TradingHub> _tradingHub;
    private readonly QuantOptions _options;
    private readonly ILogger<Mt5TradeGateway> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Mt5TradeGateway(
        IHttpClientFactory httpClientFactory,
        IHubContext<TradingHub> tradingHub,
        IOptions<QuantOptions> options,
        ILogger<Mt5TradeGateway> logger)
    {
        _httpClientFactory = httpClientFactory;
        _tradingHub = tradingHub;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Mt5TradeResult> SendMarketOrderAsync(
        string symbol,
        string direction,
        decimal volume,
        decimal? stopLoss,
        decimal? takeProfit,
        string comment,
        CancellationToken ct = default)
    {
        var payload = new
        {
            symbol,
            side = direction.Equals("Sell", StringComparison.OrdinalIgnoreCase) ? "sell" : "buy",
            volume = (double)volume,
            sl = stopLoss.HasValue ? (double?)stopLoss.Value : null,
            tp = takeProfit.HasValue ? (double?)takeProfit.Value : null,
            comment = comment ?? "NTBot-boleta"
        };

        _logger.LogInformation("Boletagem → MT5 {Direction} {Volume} {Symbol} sl={Sl} tp={Tp}",
            direction, volume, symbol, stopLoss, takeProfit);

        var http = await TryPostAsync("/api/trade/order", payload, ct);
        if (http is not null)
            return http;

        _logger.LogWarning("MT5 Python indisponível — enviando OPEN {Symbol} ao EA via SignalR", symbol);

        await BroadcastEaCommandAsync("OPEN", new
        {
            symbol,
            direction,
            volume,
            stopLoss,
            takeProfit,
            comment
        });

        return new Mt5TradeResult
        {
            Success = true,
            QueuedForEa = true,
            Message = "MT5 Python indisponível — ordem enfileirada para o EA via SignalR.",
            OrderId = Guid.NewGuid().ToString("N")[..12]
        };
    }

    public async Task<Mt5TradeResult> CloseSymbolPositionsAsync(
        string symbol,
        decimal? volume = null,
        CancellationToken ct = default)
    {
        var payload = new
        {
            symbol,
            volume = volume.HasValue ? (double?)volume.Value : null
        };

        var http = await TryPostAsync("/api/trade/close", payload, ct);
        if (http is not null)
            return http;

        await BroadcastEaCommandAsync("CLOSE", new { symbol, volume });

        return new Mt5TradeResult
        {
            Success = true,
            QueuedForEa = true,
            Message = "MT5 Python indisponível — fechamento enfileirado para o EA via SignalR."
        };
    }

    public async Task<IReadOnlyList<Mt5PositionSnapshot>> GetPositionsAsync(
        string? symbol = null,
        CancellationToken ct = default)
    {
        var baseUrl = _options.Mt5ApiUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            return [];

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(8);
            var url = string.IsNullOrWhiteSpace(symbol)
                ? $"{baseUrl}/api/trade/positions"
                : $"{baseUrl}/api/trade/positions?symbol={Uri.EscapeDataString(symbol)}";
            var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return [];

            var doc = await response.Content.ReadFromJsonAsync<Mt5PositionsResponse>(JsonOpts, ct);
            return doc?.Positions ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Falha ao listar posições MT5");
            return [];
        }
    }

    public async Task<decimal?> GetAccountBalanceAsync(CancellationToken ct = default)
    {
        var baseUrl = _options.Mt5ApiUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            return null;

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var response = await client.GetAsync($"{baseUrl}/api/status", ct);
            if (!response.IsSuccessStatusCode)
                return null;

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (doc.RootElement.TryGetProperty("account", out var account) &&
                account.ValueKind == JsonValueKind.Object &&
                account.TryGetProperty("balance", out var bal) &&
                bal.TryGetDecimal(out var balance))
                return balance;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Falha ao obter balance MT5");
        }

        return null;
    }

    public async Task<decimal?> GetLastPriceAsync(string symbol, CancellationToken ct = default)
    {
        var baseUrl = _options.Mt5ApiUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(symbol))
            return null;

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var response = await client.GetAsync($"{baseUrl}/api/ticker/{Uri.EscapeDataString(symbol)}", ct);
            if (!response.IsSuccessStatusCode)
                return null;

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            foreach (var field in new[] { "last", "bid", "ask" })
            {
                if (doc.RootElement.TryGetProperty(field, out var value) &&
                    value.TryGetDecimal(out var price) &&
                    price > 0)
                    return price;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Falha ao obter cotação MT5 de {Symbol}", symbol);
        }

        return null;
    }

    public async Task<decimal?> GetTickSizeAsync(string symbol, CancellationToken ct = default)
    {
        var baseUrl = _options.Mt5ApiUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(symbol))
            return null;

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var response = await client.GetAsync($"{baseUrl}/api/symbols/{Uri.EscapeDataString(symbol)}", ct);
            if (!response.IsSuccessStatusCode)
                return null;

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            foreach (var field in new[] { "tick_size", "trade_tick_size", "point" })
            {
                if (doc.RootElement.TryGetProperty(field, out var value) &&
                    value.TryGetDecimal(out var tick) &&
                    tick > 0)
                    return tick;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Falha ao obter tick size MT5 de {Symbol}", symbol);
        }

        return null;
    }

    public async Task<Mt5TradeResult> ModifyPositionStopAsync(
        long ticket,
        string symbol,
        decimal stopLoss,
        decimal? takeProfit = null,
        CancellationToken ct = default)
    {
        var payload = new
        {
            ticket,
            symbol,
            sl = (double)stopLoss,
            tp = takeProfit.HasValue ? (double?)takeProfit.Value : null
        };

        _logger.LogInformation("Boletagem → MT5 modify SL ticket={Ticket} {Symbol} sl={Sl}",
            ticket, symbol, stopLoss);

        var http = await TryPostAsync("/api/trade/modify", payload, ct);
        if (http is not null)
            return http;

        await BroadcastEaCommandAsync("MODIFY", new { ticket, symbol, stopLoss, takeProfit });
        return new Mt5TradeResult
        {
            Success = true,
            QueuedForEa = true,
            Message = "MT5 Python indisponível — modify SL enfileirado para o EA via SignalR.",
            Ticket = ticket.ToString()
        };
    }

    private async Task<Mt5TradeResult?> TryPostAsync(string path, object payload, CancellationToken ct)
    {
        var baseUrl = _options.Mt5ApiUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            return null;

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            var response = await client.PostAsJsonAsync($"{baseUrl}{path}", payload, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("MT5 trade HTTP {Status} em {Path}: {Body}", (int)response.StatusCode, path, body);

                // 404/501 = host antigo sem os endpoints de trade → vale tentar o EA.
                if (response.StatusCode is System.Net.HttpStatusCode.NotFound
                    or System.Net.HttpStatusCode.NotImplemented
                    or System.Net.HttpStatusCode.MethodNotAllowed)
                    return null;

                // Demais status = MT5 respondeu e recusou; não mascarar com fallback.
                return new Mt5TradeResult
                {
                    Success = false,
                    Message = $"MT5 recusou ({(int)response.StatusCode}): {Truncate(body)}"
                };
            }

            var parsed = JsonSerializer.Deserialize<Mt5HttpTradeResponse>(body, JsonOpts);
            var ok = parsed?.Ok == true || parsed?.Success == true;
            var result = new Mt5TradeResult
            {
                Success = ok,
                Message = parsed?.Message ?? parsed?.Error ?? (ok ? "Ordem enviada ao MT5" : $"MT5 retornou falha: {Truncate(body)}"),
                Ticket = parsed?.Ticket?.ToString(),
                OrderId = parsed?.Order?.ToString() ?? parsed?.Deal?.ToString(),
                ExecutedPrice = parsed?.Price,
                ExecutedVolume = parsed?.Volume
            };

            _logger.LogInformation("MT5 {Path} → success={Success} ticket={Ticket} msg={Message}",
                path, result.Success, result.Ticket, result.Message);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "MT5 Python trade endpoint indisponível em {Url}", baseUrl);
            return null;
        }
    }

    public async Task<Mt5Readiness> GetReadinessAsync(CancellationToken ct = default)
    {
        var baseUrl = _options.Mt5ApiUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            return new Mt5Readiness(false, "Quant:Mt5ApiUrl não configurado — ordens irão para o EA via SignalR.");

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var response = await client.GetAsync($"{baseUrl}/api/status", ct);
            if (!response.IsSuccessStatusCode)
                return new Mt5Readiness(false, $"Ponte MT5 respondeu {(int)response.StatusCode}.");

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = doc.RootElement;

            if (!root.TryGetProperty("terminal", out var terminal) || terminal.ValueKind != JsonValueKind.Object)
                return new Mt5Readiness(false, "Ponte MT5 sem informação do terminal.");

            if (terminal.TryGetProperty("connected", out var connected) && !connected.GetBoolean())
                return new Mt5Readiness(false, "Terminal MT5 desconectado da corretora.");

            if (terminal.TryGetProperty("trade_allowed", out var allowed) && !allowed.GetBoolean())
                return new Mt5Readiness(false, "AutoTrading desligado no MT5 — clique em 'Algo Trading' na barra do terminal.");

            return new Mt5Readiness(true, "MT5 pronto para operar.");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Falha ao consultar readiness MT5");
            return new Mt5Readiness(false, "Ponte MT5 indisponível — ordens irão para o EA via SignalR.");
        }
    }

    private static string Truncate(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "sem detalhes"
        : value.Length <= 220 ? value.Trim()
        : value.Trim()[..220] + "…";

    private Task BroadcastEaCommandAsync(string command, object parameters) =>
        _tradingHub.Clients.Group("mt5_all").SendAsync("TradeCommand", new
        {
            command,
            parameters,
            timestamp = DateTime.UtcNow,
            source = "boletagem"
        });

    private sealed class Mt5HttpTradeResponse
    {
        public bool Ok { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Error { get; set; }
        public long? Ticket { get; set; }
        public long? Order { get; set; }
        public long? Deal { get; set; }
        public decimal? Price { get; set; }
        public decimal? Volume { get; set; }
    }

    private sealed class Mt5PositionsResponse
    {
        public List<Mt5PositionSnapshot> Positions { get; set; } = [];
    }
}
