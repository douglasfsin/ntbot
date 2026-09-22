namespace NtBot.Web.Models;

public sealed class TenantBrandingModel
{
    public Guid TenantId { get; set; }
    public string AppDisplayName { get; set; } = "NTBot";
    public string? LogoUrl { get; set; }
    public string? FaviconUrl { get; set; }
    public string? LoginBackgroundUrl { get; set; }
    public string PrimaryColor { get; set; } = "#0ea5e9";
    public string SecondaryColor { get; set; } = "#f0b90b";
    public string? BgPrimary { get; set; }
    public string? BgSecondary { get; set; }
    public string? TextPrimary { get; set; }
    public string? LoginHeadline { get; set; }
    public string? LoginSubtext { get; set; }
    public string? SupportEmail { get; set; }
    public string PublicSlug { get; set; } = "";
    public string? CustomDomain { get; set; }
}

public sealed class TenantFeaturesModel
{
    public bool WhiteLabelEnabled { get; set; }
    public bool PortfolioModuleEnabled { get; set; }
    public string? PlanSlug { get; set; }
    public int? MaxClients { get; set; }
}

public sealed class BrandingMeResponse
{
    public TenantBrandingModel? Branding { get; set; }
    public TenantFeaturesModel? Features { get; set; }
}

public sealed class ClientSummaryModel
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = "";
    public string? FullName { get; set; }
    public string? RiskProfile { get; set; }
    public string? Segment { get; set; }
    public int PortfolioCount { get; set; }
}

public sealed class GoalPortfolioModel
{
    public Guid Id { get; set; }
    public Guid ClientUserId { get; set; }
    public Guid AdvisorUserId { get; set; }
    public string ObjectiveName { get; set; } = "";
    public string GoalType { get; set; } = "";
    public DateTime? TargetDate { get; set; }
    public string BaseCurrency { get; set; } = "BRL";
    public string Status { get; set; } = "";
    public string? AnalysisPrompt { get; set; }
    public string? LastAnalysisSummary { get; set; }
    public string? LastAnalysisNextBestAction { get; set; }
    public DateTime? LastAnalysisAt { get; set; }
    public bool LastAnalysisIsStub { get; set; }
    public int PositionCount { get; set; }
    public decimal TotalMtm { get; set; }
    public List<PortfolioPositionModel>? Positions { get; set; }
}

public sealed class PortfolioPositionModel
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = "";
    public string? CustodianLabel { get; set; }
    public decimal Quantity { get; set; }
    public decimal AvgPrice { get; set; }
    public decimal? MtmValue { get; set; }
    public string Source { get; set; } = "";
    public string SuitabilityStatus { get; set; } = "";
}

public sealed class ProductCatalogModel
{
    public Guid Id { get; set; }
    public string CnpjOrTicker { get; set; } = "";
    public string Name { get; set; } = "";
    public string ProductFamily { get; set; } = "";
    public bool FlagRetailAllowed { get; set; }
    public string? CreditRating { get; set; }
    public int? RiskRating { get; set; }
}

public sealed class PortfolioOfferModel
{
    public Guid Id { get; set; }
    public Guid GoalPortfolioId { get; set; }
    public string? PortfolioName { get; set; }
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductFamily { get; set; }
    public decimal? EstimatedSharpeImpact { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "";
    public string? SuitabilityHint { get; set; }
    public string? Disclaimer { get; set; }
}

public sealed class PortfolioPerformanceModel
{
    public Guid GoalPortfolioId { get; set; }
    public decimal? TwrPercent { get; set; }
    public decimal? MwrPercent { get; set; }
    public decimal? TotalCostBasis { get; set; }
    public decimal? EndingMarketValue { get; set; }
    public int SnapshotCount { get; set; }
    public int CashflowCount { get; set; }
    public bool UsedSnapshots { get; set; }
    public string MethodNote { get; set; } = "";
    public string Disclaimer { get; set; } = "";
    public bool IsStub { get; set; }
}

public sealed class PortfolioAnalysisModel
{
    public Guid GoalPortfolioId { get; set; }
    public string Summary { get; set; } = "";
    public string? NextBestAction { get; set; }
    public bool IsStub { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public sealed class PortfolioReportJobModel
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

public sealed class OpenFinanceConsentModel
{
    public Guid Id { get; set; }
    public Guid ClientUserId { get; set; }
    public string Provider { get; set; } = "";
    public string Status { get; set; } = "";
    public string? ScopeJson { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Message { get; set; }
}

public sealed class OpenFinanceStatusModel
{
    public string? Provider { get; set; }
    public bool Available { get; set; }
    public bool Simulation { get; set; }
    public string? Message { get; set; }
    public int ConsentCount { get; set; }
    public int ActiveCount { get; set; }
}

public sealed class PortfolioValuationSnapshotModel
{
    public Guid Id { get; set; }
    public Guid GoalPortfolioId { get; set; }
    public DateTime SnapshotDate { get; set; }
    public decimal TotalMarketValue { get; set; }
    public decimal CostBasis { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class PortfolioCashflowModel
{
    public Guid Id { get; set; }
    public Guid GoalPortfolioId { get; set; }
    public DateTime CashflowDate { get; set; }
    public decimal Amount { get; set; }
    public string Type { get; set; } = "";
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class OpenFinanceImportResultModel
{
    public int Imported { get; set; }
    public List<string>? Providers { get; set; }
    public string Message { get; set; } = "";
}
