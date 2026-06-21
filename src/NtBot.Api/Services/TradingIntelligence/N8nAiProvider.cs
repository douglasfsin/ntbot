using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NtBot.TradingIntelligence.Configuration;
using NtBot.TradingIntelligence.Engine;
using NtBot.TradingIntelligence.Models;

namespace NtBot.Api.Services.TradingIntelligence;

public sealed class N8nAiProvider : IN8nAiProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<TradingIntelligenceOptions> _options;
    private readonly N8nAiProviderStub _fallback;
    private readonly ILogger<N8nAiProvider> _logger;

    public N8nAiProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<TradingIntelligenceOptions> options,
        N8nAiProviderStub fallback,
        ILogger<N8nAiProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _fallback = fallback;
        _logger = logger;
    }

    public async Task<MasterAgentSummary?> GetMasterSummaryAsync(
        string asset,
        TradingIntelligenceSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        var webhook = ResolveWebhook(asset);
        if (string.IsNullOrWhiteSpace(webhook))
            return await _fallback.GetMasterSummaryAsync(asset, snapshot, cancellationToken);

        try
        {
            var client = _httpClientFactory.CreateClient("N8nAi");
            var payload = new
            {
                agent = "master",
                asset,
                specialization = ResolveSpecialization(asset),
                confluenceScore = snapshot.Confluence.Score,
                classification = snapshot.Confluence.Classification,
                recommendation = snapshot.Confluence.Recommendation,
                explanation = snapshot.Confluence.Explanation,
                positive = snapshot.Confluence.PositiveFactors,
                negative = snapshot.Confluence.NegativeFactors,
                zones = snapshot.OperationalZones.Select(z => new { z.Label, z.Type, z.PriceLow, z.PriceHigh }).ToList(),
                intersections = snapshot.Intersections.Where(i => i.HighConfluence).ToList(),
                engines = snapshot.HeatMap.Select(h => new { h.Engine, h.Score, h.Weight }).ToList()
            };

            var response = await client.PostAsJsonAsync(webhook, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return await _fallback.GetMasterSummaryAsync(asset, snapshot, cancellationToken);

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var remote = JsonSerializer.Deserialize<MasterAgentSummary>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return remote ?? await _fallback.GetMasterSummaryAsync(asset, snapshot, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "n8n AI webhook failed for {Asset}, using fallback", asset);
            return await _fallback.GetMasterSummaryAsync(asset, snapshot, cancellationToken);
        }
    }

    private string? ResolveWebhook(string asset)
    {
        var opts = _options.Value;
        if (opts.N8nAssetWebhookUrls.TryGetValue(asset, out var assetUrl) && !string.IsNullOrWhiteSpace(assetUrl))
            return assetUrl;

        return opts.N8nWebhookUrl;
    }

    private static string ResolveSpecialization(string asset) => asset.ToUpperInvariant() switch
    {
        "WIN" or "WDO" => "Índices/Dólar Brasil",
        "PETR4" or "VALE3" => "Equities Brasil",
        "XAUUSD" => "Ouro / Safe Haven",
        "SP500" or "NASDAQ" => "Índices EUA",
        "BTCUSD" => "Cripto / Risk-on",
        _ => "Multi-asset"
    };
}
