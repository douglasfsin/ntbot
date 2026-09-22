namespace NtBot.Domain.Entities.Portfolio;

/// <summary>Job de relatório HTML/PDF de carteira (e-mail via MailKit). Versões auditáveis.</summary>
public class PortfolioReportJob
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid GoalPortfolioId { get; set; }
    public Guid RequestedByUserId { get; set; }

    public string Status { get; set; } = "Pending"; // Pending | Generated | Sent | Failed
    public string? RecipientEmail { get; set; }
    public string? HtmlContent { get; set; }

    /// <summary>PDF gerado (QuestPDF) — relatório de risco auditável.</summary>
    public byte[]? PdfContent { get; set; }

    /// <summary>Summary | RiskAudit</summary>
    public string ReportType { get; set; } = "RiskAudit";

    /// <summary>Versão monotônica por carteira (1, 2, …).</summary>
    public int Version { get; set; } = 1;

    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
