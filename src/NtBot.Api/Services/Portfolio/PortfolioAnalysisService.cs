using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NtBot.Infrastructure.Persistence;

namespace NtBot.Api.Services.Portfolio;

/// <summary>
/// Análise AI de carteira. Usa webhook n8n (<c>Portfolio:N8nWebhookUrl</c> ou
/// <c>TradingIntelligence:N8nWebhookUrl</c>); senão gera análise heurística local e marca IsStub.
/// Persiste o resultado em <see cref="Domain.Entities.Portfolio.GoalPortfolio"/>.
/// </summary>
public interface IPortfolioAnalysisService
{
    Task<PortfolioAnalysisResult> AnalyzeAsync(PortfolioAnalysisRequest request, CancellationToken ct = default);
}

public sealed class PortfolioAnalysisRequest
{
    public Guid TenantId { get; set; }
    public Guid GoalPortfolioId { get; set; }
    public string? CustomPrompt { get; set; }
}

public sealed class PortfolioAnalysisResult
{
    public Guid GoalPortfolioId { get; set; }
    public string Summary { get; set; } = "";
    public string? NextBestAction { get; set; }
    public bool IsStub { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public sealed class PortfolioAnalysisService : IPortfolioAnalysisService
{
    private readonly NtBotDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly ILogger<PortfolioAnalysisService> _logger;

    public PortfolioAnalysisService(
        NtBotDbContext db,
        IHttpClientFactory http,
        IConfiguration config,
        ILogger<PortfolioAnalysisService> logger)
    {
        _db = db;
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task<PortfolioAnalysisResult> AnalyzeAsync(PortfolioAnalysisRequest request, CancellationToken ct = default)
    {
        var portfolio = await _db.GoalPortfolios
            .Include(p => p.Positions)
            .FirstOrDefaultAsync(p => p.Id == request.GoalPortfolioId && p.TenantId == request.TenantId, ct);

        if (portfolio is null)
            return new PortfolioAnalysisResult { GoalPortfolioId = request.GoalPortfolioId, Summary = "Carteira não encontrada.", IsStub = true };

        var profile = await _db.ClientInvestorProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == request.TenantId && p.UserId == portfolio.ClientUserId, ct);

        var prompt = request.CustomPrompt
            ?? portfolio.AnalysisPrompt
            ?? "Analise a carteira e sugira Next Best Action alinhado ao perfil do investidor e suitability CVM 175.";

        var totalMtm = portfolio.Positions.Sum(p => p.MtmValue ?? p.Quantity * p.AvgPrice);
        var misaligned = portfolio.Positions.Count(p => p.SuitabilityStatus == Domain.Entities.Portfolio.SuitabilityStatus.Misaligned);
        var positionsSummary = string.Join("; ",
            portfolio.Positions.Select(p => $"{p.Symbol} q={p.Quantity} mtm={p.MtmValue ?? p.Quantity * p.AvgPrice:F2} suit={p.SuitabilityStatus}"));

        PortfolioAnalysisResult result;
        var webhook = _config["Portfolio:N8nWebhookUrl"];
        if (string.IsNullOrWhiteSpace(webhook))
            webhook = _config["TradingIntelligence:N8nWebhookUrl"];

        if (!string.IsNullOrWhiteSpace(webhook))
        {
            try
            {
                var client = _http.CreateClient("N8nAi");
                var payload = new
                {
                    agent = "portfolio",
                    client_user_id = portfolio.ClientUserId.ToString(),
                    objective = portfolio.ObjectiveName,
                    goal_type = portfolio.GoalType.ToString(),
                    risk_profile = profile?.RiskProfile.ToString(),
                    segment = profile?.Segment.ToString(),
                    total_mtm = totalMtm,
                    position_count = portfolio.Positions.Count,
                    misaligned_count = misaligned,
                    positions = positionsSummary,
                    prompt
                };
                using var resp = await client.PostAsJsonAsync(webhook, payload, ct);
                if (resp.IsSuccessStatusCode)
                {
                    var text = await resp.Content.ReadAsStringAsync(ct);
                    result = ParseN8nResponse(portfolio.Id, text);
                }
                else
                {
                    _logger.LogWarning("Portfolio n8n HTTP {Status} — fallback heurístico", (int)resp.StatusCode);
                    result = BuildHeuristic(portfolio.Id, portfolio.ObjectiveName, totalMtm, portfolio.Positions.Count, misaligned, prompt, isStub: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Portfolio n8n analysis failed — fallback heurístico");
                result = BuildHeuristic(portfolio.Id, portfolio.ObjectiveName, totalMtm, portfolio.Positions.Count, misaligned, prompt, isStub: true);
            }
        }
        else
        {
            result = BuildHeuristic(portfolio.Id, portfolio.ObjectiveName, totalMtm, portfolio.Positions.Count, misaligned, prompt, isStub: true);
        }

        portfolio.LastAnalysisSummary = result.Summary.Length > 8000 ? result.Summary[..8000] : result.Summary;
        portfolio.LastAnalysisNextBestAction = result.NextBestAction;
        portfolio.LastAnalysisAt = result.GeneratedAt;
        portfolio.LastAnalysisIsStub = result.IsStub;
        portfolio.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return result;
    }

    private static PortfolioAnalysisResult ParseN8nResponse(Guid portfolioId, string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            var summary = root.TryGetProperty("summary", out var s) ? s.GetString()
                : root.TryGetProperty("master", out var m) ? m.GetString()
                : root.TryGetProperty("analysis", out var a) ? a.GetString()
                : null;
            var nba = root.TryGetProperty("nextBestAction", out var n) ? n.GetString()
                : root.TryGetProperty("next_best_action", out var n2) ? n2.GetString()
                : root.TryGetProperty("recommendation", out var r) ? r.GetString()
                : null;
            if (!string.IsNullOrWhiteSpace(summary))
            {
                return new PortfolioAnalysisResult
                {
                    GoalPortfolioId = portfolioId,
                    Summary = summary!,
                    NextBestAction = nba ?? "Revisar propostas pendentes e enquadramento.",
                    IsStub = false
                };
            }
        }
        catch (JsonException)
        {
            /* plain text */
        }

        var trimmed = text.Length > 4000 ? text[..4000] : text;
        return new PortfolioAnalysisResult
        {
            GoalPortfolioId = portfolioId,
            Summary = trimmed,
            NextBestAction = "Ver resposta do agente n8n.",
            IsStub = false
        };
    }

    private static PortfolioAnalysisResult BuildHeuristic(
        Guid portfolioId, string objective, decimal totalMtm, int posCount, int misaligned, string prompt, bool isStub)
    {
        var nba = misaligned > 0
            ? $"Revisar {misaligned} posição(ões) DESENQUADRADAS e propor realocação alinhada ao perfil."
            : posCount == 0
                ? "Montar alocação inicial (FIF liquidez / reserva) conforme objetivo."
                : "Avaliar diversificação por classe e oportunidade de cross-sell no catálogo (FIF/FII/FIDC).";

        return new PortfolioAnalysisResult
        {
            GoalPortfolioId = portfolioId,
            Summary =
                $"Análise local — objetivo «{objective}»: {posCount} posição(ões), MtM {totalMtm:N2} BRL, " +
                $"{misaligned} desenquadrada(s). Prompt: {Truncate(prompt, 200)}. " +
                (isStub ? "Configure Portfolio:N8nWebhookUrl (ou TradingIntelligence:N8nWebhookUrl) para IA via n8n." : ""),
            NextBestAction = nba,
            IsStub = isStub
        };
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
