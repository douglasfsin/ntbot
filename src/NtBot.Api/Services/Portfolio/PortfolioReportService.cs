using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NtBot.Domain.Entities;
using NtBot.Domain.Entities.Portfolio;
using NtBot.Identity.Services;
using NtBot.Infrastructure.Persistence;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NtBot.Api.Services.Portfolio;

/// <summary>Relatório de risco auditável (HTML + PDF) com branding white-label e versionamento.</summary>
public interface IPortfolioReportService
{
    Task<PortfolioReportJobDto> GenerateAsync(Guid tenantId, Guid userId, Guid portfolioId, bool sendEmail, string? email, CancellationToken ct = default);
    Task<PortfolioReportJobDto?> GetAsync(Guid tenantId, Guid jobId, CancellationToken ct = default);
    Task<List<PortfolioReportJobDto>> ListAsync(Guid tenantId, CancellationToken ct = default);
    Task<(byte[] Pdf, string FileName)?> GetPdfAsync(Guid tenantId, Guid jobId, CancellationToken ct = default);
}

public sealed class PortfolioReportService : IPortfolioReportService
{
    private readonly NtBotDbContext _db;
    private readonly IEmailService _email;
    private readonly IPerformanceService _performance;
    private readonly ILogger<PortfolioReportService> _logger;

    static PortfolioReportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public PortfolioReportService(
        NtBotDbContext db,
        IEmailService email,
        IPerformanceService performance,
        ILogger<PortfolioReportService> logger)
    {
        _db = db;
        _email = email;
        _performance = performance;
        _logger = logger;
    }

    public async Task<PortfolioReportJobDto> GenerateAsync(
        Guid tenantId, Guid userId, Guid portfolioId, bool sendEmail, string? email, CancellationToken ct = default)
    {
        var portfolio = await _db.GoalPortfolios.AsNoTracking()
            .Include(p => p.Positions)
            .FirstOrDefaultAsync(p => p.Id == portfolioId && p.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("Carteira não encontrada.");

        var profile = await _db.ClientInvestorProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.UserId == portfolio.ClientUserId, ct);

        var branding = await _db.TenantBrandings.AsNoTracking()
            .FirstOrDefaultAsync(b => b.TenantId == tenantId, ct);

        var perf = await _performance.GetPerformanceAsync(tenantId, portfolioId, ct);
        var version = await _db.PortfolioReportJobs
            .Where(j => j.GoalPortfolioId == portfolioId)
            .Select(j => (int?)j.Version)
            .MaxAsync(ct) ?? 0;
        version++;

        var html = BuildRiskHtml(portfolio, profile, branding, perf, version);
        byte[]? pdf = null;
        try
        {
            pdf = BuildRiskPdf(portfolio, profile, branding, perf, version);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PDF generation failed — HTML only");
        }

        var job = new PortfolioReportJob
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            GoalPortfolioId = portfolioId,
            RequestedByUserId = userId,
            Status = "Generated",
            RecipientEmail = email,
            HtmlContent = html,
            PdfContent = pdf,
            ReportType = "RiskAudit",
            Version = version,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        _db.PortfolioReportJobs.Add(job);

        if (sendEmail)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                job.Status = "Failed";
                job.ErrorMessage = "E-mail do destinatário é obrigatório para envio.";
            }
            else
            {
                try
                {
                    await _email.SendHtmlAsync(
                        email.Trim(),
                        $"Relatório de risco v{version} — {portfolio.ObjectiveName}",
                        html);
                    job.Status = "Sent";
                    job.CompletedAt = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    job.Status = "Failed";
                    job.ErrorMessage = ex.Message;
                    job.CompletedAt = DateTime.UtcNow;
                    _logger.LogWarning(ex, "Portfolio report email failed");
                }
            }
        }

        await _db.SaveChangesAsync(ct);
        return Map(job);
    }

    public async Task<PortfolioReportJobDto?> GetAsync(Guid tenantId, Guid jobId, CancellationToken ct = default)
    {
        var j = await _db.PortfolioReportJobs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == jobId && x.TenantId == tenantId, ct);
        return j is null ? null : Map(j);
    }

    public async Task<List<PortfolioReportJobDto>> ListAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _db.PortfolioReportJobs
            .Where(j => j.TenantId == tenantId)
            .OrderByDescending(j => j.CreatedAt)
            .Take(50)
            .Select(j => new PortfolioReportJobDto
            {
                Id = j.Id,
                GoalPortfolioId = j.GoalPortfolioId,
                Status = j.Status,
                RecipientEmail = j.RecipientEmail,
                CreatedAt = j.CreatedAt,
                CompletedAt = j.CompletedAt,
                ErrorMessage = j.ErrorMessage,
                HasHtml = j.HtmlContent != null,
                HasPdf = j.PdfContent != null,
                ReportType = j.ReportType,
                Version = j.Version
            })
            .ToListAsync(ct);
    }

    public async Task<(byte[] Pdf, string FileName)?> GetPdfAsync(Guid tenantId, Guid jobId, CancellationToken ct = default)
    {
        var j = await _db.PortfolioReportJobs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == jobId && x.TenantId == tenantId, ct);
        if (j?.PdfContent is null || j.PdfContent.Length == 0) return null;
        return (j.PdfContent, $"relatorio-risco-v{j.Version}-{j.Id:N}.pdf");
    }

    private static string BuildRiskHtml(
        GoalPortfolio portfolio,
        ClientInvestorProfile? profile,
        TenantBranding? branding,
        PortfolioPerformanceDto perf,
        int version)
    {
        var sb = new StringBuilder();
        var pt = CultureInfo.GetCultureInfo("pt-BR");
        var primary = branding?.PrimaryColor ?? "#0ea5e9";
        var secondary = branding?.SecondaryColor ?? "#f0b90b";
        var appName = branding?.AppDisplayName ?? "NTBot";
        var total = portfolio.Positions.Sum(p => p.MtmValue ?? p.Quantity * p.AvgPrice);
        var aligned = portfolio.Positions.Count(p => p.SuitabilityStatus == SuitabilityStatus.Aligned);
        var mis = portfolio.Positions.Count(p => p.SuitabilityStatus == SuitabilityStatus.Misaligned);
        var ts = DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm", pt);

        sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>Relatório de risco auditável</title>");
        sb.Append("<style>body{font-family:Segoe UI,Arial,sans-serif;color:#1a1a1a;margin:24px;}");
        sb.Append($"h1{{color:{primary};border-bottom:3px solid {secondary};padding-bottom:8px;}}");
        sb.Append("table{border-collapse:collapse;width:100%;margin:16px 0;}th,td{border:1px solid #ddd;padding:8px;text-align:left;}");
        sb.Append($"th{{background:{primary};color:#fff;}}.muted{{color:#666;font-size:12px;}}");
        sb.Append(".badge{padding:2px 8px;border-radius:4px;font-size:11px;}.ok{background:#dcfce7;}.bad{background:#fee2e2;}");
        sb.Append(".warn{background:#fef3c7;}.brand{display:flex;align-items:center;gap:12px;margin-bottom:16px;}");
        sb.Append($"header.brand img{{max-height:48px;}}.meta{{background:#f8fafc;padding:12px;border-left:4px solid {secondary};}}</style></head><body>");

        sb.Append("<header class=\"brand\">");
        if (!string.IsNullOrWhiteSpace(branding?.LogoUrl))
            sb.Append($"<img src=\"{WebUtility.HtmlEncode(branding.LogoUrl)}\" alt=\"logo\"/>");
        sb.Append($"<div><strong>{WebUtility.HtmlEncode(appName)}</strong><br/><span class=\"muted\">Relatório de risco auditável · v{version}</span></div>");
        sb.Append("</header>");

        sb.Append($"<h1>Risco &amp; enquadramento</h1>");
        sb.Append("<div class=\"meta\">");
        sb.Append($"<p><strong>{WebUtility.HtmlEncode(portfolio.ObjectiveName)}</strong> · {portfolio.GoalType} · {portfolio.Status}</p>");
        sb.Append($"<p>Gerado em <strong>{ts} UTC</strong> · Moeda {WebUtility.HtmlEncode(portfolio.BaseCurrency)} · MtM <strong>{total.ToString("N2", pt)}</strong></p>");
        if (profile is not null)
            sb.Append($"<p>Perfil: {profile.RiskProfile} · Segmento: {profile.Segment}</p>");
        sb.Append("</div>");

        sb.Append("<h2>Performance</h2><ul>");
        sb.Append($"<li>TWR: <strong>{(perf.TwrPercent?.ToString("N2", pt) ?? "—")}%</strong></li>");
        sb.Append($"<li>MWR: <strong>{(perf.MwrPercent?.ToString("N2", pt) ?? "—")}%</strong></li>");
        sb.Append($"<li>Snapshots MtM: {perf.SnapshotCount} · Fluxos: {perf.CashflowCount} · Caminho oficial: {(perf.UsedSnapshots ? "sim" : "não")}</li>");
        sb.Append("</ul>");
        sb.Append($"<p class=\"muted\">{WebUtility.HtmlEncode(perf.MethodNote)}</p>");

        sb.Append("<h2>Concentração</h2>");
        sb.Append("<table><thead><tr><th>Símbolo</th><th>Peso %</th><th>MtM</th><th>Custódia</th><th>Flag</th></tr></thead><tbody>");
        foreach (var p in portfolio.Positions.OrderByDescending(x => x.MtmValue ?? x.Quantity * x.AvgPrice))
        {
            var mtm = p.MtmValue ?? p.Quantity * p.AvgPrice;
            var weight = total > 0 ? mtm / total * 100m : 0;
            var flag = weight >= 25m ? "Concentração ≥25%" : weight >= 15m ? "Atenção ≥15%" : "OK";
            var cls = weight >= 25m ? "bad" : weight >= 15m ? "warn" : "ok";
            sb.Append("<tr>");
            sb.Append($"<td>{WebUtility.HtmlEncode(p.Symbol)}</td>");
            sb.Append($"<td>{weight.ToString("N2", pt)}</td>");
            sb.Append($"<td>{mtm.ToString("N2", pt)}</td>");
            sb.Append($"<td>{WebUtility.HtmlEncode(p.CustodianLabel ?? "—")}</td>");
            sb.Append($"<td><span class=\"badge {cls}\">{flag}</span></td>");
            sb.Append("</tr>");
        }
        if (portfolio.Positions.Count == 0)
            sb.Append("<tr><td colspan=\"5\">Sem posições.</td></tr>");
        sb.Append("</tbody></table>");

        sb.Append("<h2>Suitability</h2>");
        sb.Append($"<p>{aligned} ENQUADRADAS · {mis} DESENQUADRADAS · {portfolio.Positions.Count - aligned - mis} sem classificação</p>");
        sb.Append("<table><thead><tr><th>Símbolo</th><th>Status</th></tr></thead><tbody>");
        foreach (var p in portfolio.Positions.Where(x => x.SuitabilityStatus == SuitabilityStatus.Misaligned))
        {
            sb.Append("<tr>");
            sb.Append($"<td>{WebUtility.HtmlEncode(p.Symbol)}</td>");
            sb.Append("<td><span class=\"badge bad\">DESENQUADRADO</span></td>");
            sb.Append("</tr>");
        }
        if (mis == 0)
            sb.Append("<tr><td colspan=\"2\">Nenhum mismatch de suitability.</td></tr>");
        sb.Append("</tbody></table>");

        if (!string.IsNullOrWhiteSpace(portfolio.LastAnalysisSummary))
        {
            sb.Append("<h2>Última análise AI</h2>");
            sb.Append($"<p>{WebUtility.HtmlEncode(portfolio.LastAnalysisSummary)}</p>");
        }

        sb.Append("<p class=\"muted\">Documento ilustrativo — não constitui recomendação de investimento. ");
        sb.Append("Aceite de ofertas = intenção (sem execução em corretora). ");
        sb.Append("Open Finance produção requer parceiro ITP. ");
        sb.Append($"Versão auditável v{version} · {WebUtility.HtmlEncode(appName)}.</p>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static byte[] BuildRiskPdf(
        GoalPortfolio portfolio,
        ClientInvestorProfile? profile,
        TenantBranding? branding,
        PortfolioPerformanceDto perf,
        int version)
    {
        var pt = CultureInfo.GetCultureInfo("pt-BR");
        var primary = ParseColor(branding?.PrimaryColor, Colors.Blue.Medium);
        var appName = branding?.AppDisplayName ?? "NTBot";
        var total = portfolio.Positions.Sum(p => p.MtmValue ?? p.Quantity * p.AvgPrice);
        var mis = portfolio.Positions.Count(p => p.SuitabilityStatus == SuitabilityStatus.Misaligned);
        var ts = DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm", pt);

        var rows = portfolio.Positions
            .OrderByDescending(x => x.MtmValue ?? x.Quantity * x.AvgPrice)
            .Select(p =>
            {
                var mtm = p.MtmValue ?? p.Quantity * p.AvgPrice;
                var weight = total > 0 ? mtm / total * 100m : 0;
                return new
                {
                    p.Symbol,
                    Weight = weight.ToString("N2", pt),
                    Mtm = mtm.ToString("N2", pt),
                    Cust = p.CustodianLabel ?? "—",
                    Flag = weight >= 25m ? "≥25%" : weight >= 15m ? "≥15%" : "OK"
                };
            }).ToList();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text(appName).FontSize(16).Bold().FontColor(primary);
                    col.Item().Text($"Relatório de risco auditável · v{version}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(4).LineHorizontal(2).LineColor(primary);
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text(portfolio.ObjectiveName).FontSize(14).Bold();
                    col.Item().Text($"Gerado: {ts} UTC · MtM: {total.ToString("N2", pt)} {portfolio.BaseCurrency}");
                    if (profile is not null)
                        col.Item().Text($"Perfil: {profile.RiskProfile} · Segmento: {profile.Segment}");

                    col.Item().PaddingTop(8).Text("Performance").Bold().FontColor(primary);
                    col.Item().Text($"TWR: {(perf.TwrPercent?.ToString("N2", pt) ?? "—")}% · MWR: {(perf.MwrPercent?.ToString("N2", pt) ?? "—")}%");
                    col.Item().Text($"Snapshots: {perf.SnapshotCount} · Fluxos: {perf.CashflowCount} · Oficial: {(perf.UsedSnapshots ? "sim" : "não")}");

                    col.Item().PaddingTop(8).Text("Concentração").Bold().FontColor(primary);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1.5f);
                            c.RelativeColumn(1.5f);
                            c.RelativeColumn(1);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Background(primary).Padding(4).Text("Símbolo").FontColor(Colors.White);
                            h.Cell().Background(primary).Padding(4).Text("Peso%").FontColor(Colors.White);
                            h.Cell().Background(primary).Padding(4).Text("MtM").FontColor(Colors.White);
                            h.Cell().Background(primary).Padding(4).Text("Custódia").FontColor(Colors.White);
                            h.Cell().Background(primary).Padding(4).Text("Flag").FontColor(Colors.White);
                        });
                        foreach (var r in rows)
                        {
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(r.Symbol);
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(r.Weight);
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(r.Mtm);
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(r.Cust);
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(r.Flag);
                        }
                        if (rows.Count == 0)
                            table.Cell().ColumnSpan(5).Padding(4).Text("Sem posições.");
                    });

                    col.Item().PaddingTop(8).Text($"Suitability: {mis} desenquadrada(s)").Bold().FontColor(primary);
                    foreach (var p in portfolio.Positions.Where(x => x.SuitabilityStatus == SuitabilityStatus.Misaligned))
                        col.Item().Text($"• {p.Symbol} — DESENQUADRADO");
                    if (mis == 0)
                        col.Item().Text("Nenhum mismatch de suitability.");
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Ilustrativo — não é recomendação. OF produção = parceiro ITP. ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.Span($"v{version}").FontSize(8);
                });
            });
        }).GeneratePdf();
    }

    private static string ParseColor(string? hex, string fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        var h = hex.Trim();
        if (!h.StartsWith('#')) h = "#" + h;
        try { return h; }
        catch { return fallback; }
    }

    private static PortfolioReportJobDto Map(PortfolioReportJob j) => new()
    {
        Id = j.Id,
        GoalPortfolioId = j.GoalPortfolioId,
        Status = j.Status,
        RecipientEmail = j.RecipientEmail,
        CreatedAt = j.CreatedAt,
        CompletedAt = j.CompletedAt,
        ErrorMessage = j.ErrorMessage,
        HasHtml = j.HtmlContent != null,
        HasPdf = j.PdfContent != null && j.PdfContent.Length > 0,
        HtmlContent = j.HtmlContent,
        ReportType = j.ReportType,
        Version = j.Version
    };
}

public sealed class PortfolioReportJobDto
{
    public Guid Id { get; set; }
    public Guid GoalPortfolioId { get; set; }
    public string Status { get; set; } = "";
    public string? RecipientEmail { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public bool HasHtml { get; set; }
    public bool HasPdf { get; set; }
    public string? HtmlContent { get; set; }
    public string ReportType { get; set; } = "RiskAudit";
    public int Version { get; set; }
}
