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
        var webhook = _options.Value.N8nWebhookUrl;
        if (string.IsNullOrWhiteSpace(webhook))
            return await _fallback.GetMasterSummaryAsync(asset, snapshot, cancellationToken);

        try
        {
            var client = _httpClientFactory.CreateClient("N8nAi");
            var payload = new
            {
                asset,
                confluenceScore = snapshot.Confluence.Score,
                classification = snapshot.Confluence.Classification,
                explanation = snapshot.Confluence.Explanation,
                positive = snapshot.Confluence.PositiveFactors,
                negative = snapshot.Confluence.NegativeFactors,
                zones = snapshot.OperationalZones.Select(z => z.Label).ToList()
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
}
