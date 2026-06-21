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

    public async Task<TradingIntelligenceAiResult> GetAiResultAsync(
        string asset,
        TradingIntelligenceSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        var webhook = ResolveWebhook(asset);
        if (string.IsNullOrWhiteSpace(webhook))
            return await _fallback.GetAiResultAsync(asset, snapshot, cancellationToken);

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
                engines = snapshot.HeatMap.Select(h => new { h.Engine, h.Score, h.Weight }).ToList(),
                agentInsights = SpecialistAgentEngine.BuildInsights(asset, snapshot)
            };

            var response = await client.PostAsJsonAsync(webhook, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return await _fallback.GetAiResultAsync(asset, snapshot, cancellationToken);

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseAiResponse(json, asset, snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "n8n AI webhook failed for {Asset}, using fallback", asset);
            return await _fallback.GetAiResultAsync(asset, snapshot, cancellationToken);
        }
    }

    private TradingIntelligenceAiResult ParseAiResponse(string json, string asset, TradingIntelligenceSnapshot snapshot)
    {
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var wrapped = JsonSerializer.Deserialize<N8nAiResponseDto>(json, opts);
        if (wrapped is not null)
        {
            var master = wrapped.Master ?? wrapped.Summary;
            var agents = wrapped.AgentInsights ?? wrapped.Agents;
            if (master is not null || agents?.Count > 0)
            {
                return new TradingIntelligenceAiResult
                {
                    Master = master ?? BuildMasterFromSnapshot(snapshot),
                    AgentInsights = agents?.Count > 0
                        ? agents
                        : SpecialistAgentEngine.BuildInsights(asset, snapshot)
                };
            }
        }

        var masterOnly = JsonSerializer.Deserialize<MasterAgentSummary>(json, opts);
        if (masterOnly is not null && !string.IsNullOrWhiteSpace(masterOnly.Summary))
        {
            return new TradingIntelligenceAiResult
            {
                Master = masterOnly,
                AgentInsights = SpecialistAgentEngine.BuildInsights(asset, snapshot)
            };
        }

        return new TradingIntelligenceAiResult
        {
            Master = BuildMasterFromSnapshot(snapshot),
            AgentInsights = SpecialistAgentEngine.BuildInsights(asset, snapshot)
        };
    }

    private static MasterAgentSummary BuildMasterFromSnapshot(TradingIntelligenceSnapshot snapshot) =>
        new()
        {
            Summary = snapshot.Confluence.Explanation,
            Strengths = snapshot.Confluence.PositiveFactors.ToList(),
            Weaknesses = snapshot.Confluence.NegativeFactors.ToList(),
            Probability = snapshot.Confluence.Score >= 70 ? "Elevada" : snapshot.Confluence.Score <= 30 ? "Baixa" : "Moderada",
            Risk = snapshot.Confluence.Score >= 80 ? "Controlado" : "Monitorar"
        };

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

    private sealed class N8nAiResponseDto
    {
        public MasterAgentSummary? Summary { get; set; }
        public MasterAgentSummary? Master { get; set; }
        public List<AiAgentInsight>? Agents { get; set; }
        public List<AiAgentInsight>? AgentInsights { get; set; }
    }
}
