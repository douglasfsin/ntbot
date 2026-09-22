using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NtBot.Api.Services.WhiteLabel;
using NtBot.Domain.Entities;
using NtBot.Domain.Entities.Portfolio;
using NtBot.Infrastructure.Persistence;

namespace NtBot.Api.Services.Portfolio;

public interface IPortfolioService
{
    Task<List<ClientSummaryDto>> ListClientsAsync(Guid tenantId, CancellationToken ct = default);
    Task<ClientInvestorProfileDto?> GetProfileAsync(Guid tenantId, Guid clientUserId, CancellationToken ct = default);
    Task<ClientInvestorProfileDto> UpsertProfileAsync(Guid tenantId, Guid advisorUserId, UpsertProfileRequest req, CancellationToken ct = default);
    Task<List<GoalPortfolioDto>> ListPortfoliosAsync(Guid tenantId, Guid? clientUserId, Guid? forUserId, string role, CancellationToken ct = default);
    Task<GoalPortfolioDto?> GetPortfolioAsync(Guid tenantId, Guid portfolioId, CancellationToken ct = default);
    Task<GoalPortfolioDto> CreatePortfolioAsync(Guid tenantId, Guid advisorUserId, CreatePortfolioRequest req, CancellationToken ct = default);
    Task<GoalPortfolioDto?> UpdatePortfolioAsync(Guid tenantId, Guid portfolioId, UpdatePortfolioRequest req, CancellationToken ct = default);
    Task<PortfolioPositionDto> AddPositionAsync(Guid tenantId, Guid portfolioId, AddPositionRequest req, CancellationToken ct = default);
    Task<int> ImportCsvAsync(Guid tenantId, Guid portfolioId, string csvContent, CancellationToken ct = default);
    Task DeletePositionAsync(Guid tenantId, Guid positionId, CancellationToken ct = default);
    Task<List<ProductCatalogDto>> ListProductsAsync(Guid tenantId, string? family = null, bool? retailOnly = null, string? ratingMin = null, CancellationToken ct = default);
    Task<PortfolioRecommendationDto> SendOfferAsync(Guid tenantId, Guid advisorUserId, SendOfferRequest req, CancellationToken ct = default);
    Task<List<PortfolioRecommendationDto>> ListOffersAsync(Guid tenantId, Guid userId, string role, bool pendingOnly, CancellationToken ct = default);
    Task<PortfolioRecommendationDto?> RespondOfferAsync(Guid tenantId, Guid clientUserId, Guid offerId, bool accept, CancellationToken ct = default);
}

public sealed class PortfolioService : IPortfolioService
{
    private readonly NtBotDbContext _db;
    private readonly ITenantFeatureService _features;

    public PortfolioService(NtBotDbContext db, ITenantFeatureService features)
    {
        _db = db;
        _features = features;
    }

    private async Task EnsurePortfolioAsync(Guid tenantId, CancellationToken ct)
    {
        if (!await _features.IsPortfolioModuleEnabledAsync(tenantId, ct))
            throw new InvalidOperationException("Módulo de portfólio não habilitado. Requer plano Partner ou WhiteLabelEnabled.");
    }

    public async Task<List<ClientSummaryDto>> ListClientsAsync(Guid tenantId, CancellationToken ct = default)
    {
        await EnsurePortfolioAsync(tenantId, ct);
        var clients = await _db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.Role == UserRole.CLIENT && u.IsActive)
            .OrderBy(u => u.FullName ?? u.Email)
            .Select(u => new ClientSummaryDto
            {
                UserId = u.Id,
                Email = u.Email,
                FullName = u.FullName,
                PortfolioCount = _db.GoalPortfolios.Count(p => p.TenantId == tenantId && p.ClientUserId == u.Id && p.Status != PortfolioStatus.Archived)
            })
            .ToListAsync(ct);

        var profiles = await _db.ClientInvestorProfiles.AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .ToDictionaryAsync(p => p.UserId, ct);

        foreach (var c in clients)
        {
            if (profiles.TryGetValue(c.UserId, out var pr))
            {
                c.RiskProfile = pr.RiskProfile.ToString();
                c.Segment = pr.Segment.ToString();
            }
        }
        return clients;
    }

    public async Task<ClientInvestorProfileDto?> GetProfileAsync(Guid tenantId, Guid clientUserId, CancellationToken ct = default)
    {
        await EnsurePortfolioAsync(tenantId, ct);
        var p = await _db.ClientInvestorProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == clientUserId, ct);
        return p is null ? null : MapProfile(p);
    }

    public async Task<ClientInvestorProfileDto> UpsertProfileAsync(Guid tenantId, Guid advisorUserId, UpsertProfileRequest req, CancellationToken ct = default)
    {
        await EnsurePortfolioAsync(tenantId, ct);
        var clientOk = await _db.Users.AnyAsync(u => u.Id == req.ClientUserId && u.TenantId == tenantId, ct);
        if (!clientOk) throw new InvalidOperationException("Cliente não encontrado neste tenant.");

        var entity = await _db.ClientInvestorProfiles
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == req.ClientUserId, ct);
        if (entity is null)
        {
            entity = new ClientInvestorProfile
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = req.ClientUserId,
                CreatedAt = DateTime.UtcNow
            };
            _db.ClientInvestorProfiles.Add(entity);
        }

        entity.RiskProfile = Enum.TryParse<InvestorRiskProfile>(req.RiskProfile, true, out var rp) ? rp : InvestorRiskProfile.Moderate;
        entity.Segment = Enum.TryParse<InvestorSegment>(req.Segment, true, out var seg) ? seg : InvestorSegment.Retail;
        entity.SuitabilityAnswersJson = req.SuitabilityAnswersJson;
        entity.AssessedAt = DateTime.UtcNow;
        entity.AssessedByUserId = advisorUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return MapProfile(entity);
    }

    public async Task<List<GoalPortfolioDto>> ListPortfoliosAsync(Guid tenantId, Guid? clientUserId, Guid? forUserId, string role, CancellationToken ct = default)
    {
        await EnsurePortfolioAsync(tenantId, ct);
        var q = _db.GoalPortfolios.AsNoTracking().Where(p => p.TenantId == tenantId);
        if (string.Equals(role, "CLIENT", StringComparison.OrdinalIgnoreCase) && forUserId.HasValue)
            q = q.Where(p => p.ClientUserId == forUserId.Value);
        else if (clientUserId.HasValue)
            q = q.Where(p => p.ClientUserId == clientUserId.Value);

        var list = await q.OrderByDescending(p => p.UpdatedAt).ToListAsync(ct);
        var ids = list.Select(p => p.Id).ToList();
        var posCounts = await _db.PortfolioPositions.AsNoTracking()
            .Where(x => ids.Contains(x.GoalPortfolioId))
            .GroupBy(x => x.GoalPortfolioId)
            .Select(g => new { g.Key, Count = g.Count(), Mtm = g.Sum(x => x.MtmValue ?? (x.Quantity * x.AvgPrice)) })
            .ToDictionaryAsync(x => x.Key, ct);

        return list.Select(p =>
        {
            posCounts.TryGetValue(p.Id, out var stats);
            return MapPortfolio(p, stats?.Count ?? 0, stats?.Mtm ?? 0);
        }).ToList();
    }

    public async Task<GoalPortfolioDto?> GetPortfolioAsync(Guid tenantId, Guid portfolioId, CancellationToken ct = default)
    {
        await EnsurePortfolioAsync(tenantId, ct);
        var p = await _db.GoalPortfolios.AsNoTracking()
            .Include(x => x.Positions)
            .FirstOrDefaultAsync(x => x.Id == portfolioId && x.TenantId == tenantId, ct);
        if (p is null) return null;
        var dto = MapPortfolio(p, p.Positions.Count, p.Positions.Sum(x => x.MtmValue ?? x.Quantity * x.AvgPrice));
        dto.Positions = p.Positions.Select(MapPosition).ToList();
        return dto;
    }

    public async Task<GoalPortfolioDto> CreatePortfolioAsync(Guid tenantId, Guid advisorUserId, CreatePortfolioRequest req, CancellationToken ct = default)
    {
        await EnsurePortfolioAsync(tenantId, ct);
        var clientOk = await _db.Users.AnyAsync(u => u.Id == req.ClientUserId && u.TenantId == tenantId, ct);
        if (!clientOk) throw new InvalidOperationException("Cliente não encontrado.");

        var entity = new GoalPortfolio
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AdvisorUserId = advisorUserId,
            ClientUserId = req.ClientUserId,
            ObjectiveName = req.ObjectiveName.Trim(),
            GoalType = Enum.TryParse<PortfolioGoalType>(req.GoalType, true, out var gt) ? gt : PortfolioGoalType.Custom,
            TargetDate = req.TargetDate,
            BaseCurrency = string.IsNullOrWhiteSpace(req.BaseCurrency) ? "BRL" : req.BaseCurrency.Trim().ToUpperInvariant(),
            Status = PortfolioStatus.Active,
            AnalysisPrompt = req.AnalysisPrompt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.GoalPortfolios.Add(entity);
        await _db.SaveChangesAsync(ct);
        return MapPortfolio(entity, 0, 0);
    }

    public async Task<GoalPortfolioDto?> UpdatePortfolioAsync(Guid tenantId, Guid portfolioId, UpdatePortfolioRequest req, CancellationToken ct = default)
    {
        await EnsurePortfolioAsync(tenantId, ct);
        var entity = await _db.GoalPortfolios.FirstOrDefaultAsync(p => p.Id == portfolioId && p.TenantId == tenantId, ct);
        if (entity is null) return null;
        if (!string.IsNullOrWhiteSpace(req.ObjectiveName)) entity.ObjectiveName = req.ObjectiveName.Trim();
        if (!string.IsNullOrWhiteSpace(req.GoalType) && Enum.TryParse<PortfolioGoalType>(req.GoalType, true, out var gt))
            entity.GoalType = gt;
        if (req.TargetDate.HasValue) entity.TargetDate = req.TargetDate;
        if (!string.IsNullOrWhiteSpace(req.Status) && Enum.TryParse<PortfolioStatus>(req.Status, true, out var st))
            entity.Status = st;
        if (req.AnalysisPrompt is not null) entity.AnalysisPrompt = req.AnalysisPrompt;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return MapPortfolio(entity, 0, 0);
    }

    public async Task<PortfolioPositionDto> AddPositionAsync(Guid tenantId, Guid portfolioId, AddPositionRequest req, CancellationToken ct = default)
    {
        await EnsurePortfolioAsync(tenantId, ct);
        var portfolio = await _db.GoalPortfolios.FirstOrDefaultAsync(p => p.Id == portfolioId && p.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("Carteira não encontrada.");

        var profile = await _db.ClientInvestorProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.UserId == portfolio.ClientUserId, ct);

        ProductCatalogItem? product = null;
        if (req.ProductId.HasValue)
            product = await _db.ProductCatalogItems.AsNoTracking().FirstOrDefaultAsync(p => p.Id == req.ProductId, ct);

        var entity = new PortfolioPosition
        {
            Id = Guid.NewGuid(),
            GoalPortfolioId = portfolioId,
            ProductId = req.ProductId,
            Symbol = (req.Symbol ?? product?.CnpjOrTicker ?? "?").Trim().ToUpperInvariant(),
            CustodianLabel = req.CustodianLabel,
            Quantity = req.Quantity,
            AvgPrice = req.AvgPrice,
            WeightPct = req.WeightPct,
            TargetValue = req.TargetValue,
            MtmValue = req.MtmValue ?? (req.Quantity * req.AvgPrice),
            Source = PositionSource.Manual,
            SuitabilityStatus = EvaluateSuitability(product, profile),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.PortfolioPositions.Add(entity);
        portfolio.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return MapPosition(entity);
    }

    public async Task<int> ImportCsvAsync(Guid tenantId, Guid portfolioId, string csvContent, CancellationToken ct = default)
    {
        await EnsurePortfolioAsync(tenantId, ct);
        var portfolio = await _db.GoalPortfolios.FirstOrDefaultAsync(p => p.Id == portfolioId && p.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("Carteira não encontrada.");

        var lines = csvContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var count = 0;
        foreach (var line in lines.Skip(1)) // header: Symbol,Quantity,AvgPrice,Custodian
        {
            var parts = SplitCsvLine(line);
            if (parts.Length < 3) continue;
            if (!decimal.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var qty)) continue;
            if (!decimal.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var price)) continue;

            _db.PortfolioPositions.Add(new PortfolioPosition
            {
                Id = Guid.NewGuid(),
                GoalPortfolioId = portfolioId,
                Symbol = parts[0].Trim().ToUpperInvariant(),
                Quantity = qty,
                AvgPrice = price,
                CustodianLabel = parts.Length > 3 ? parts[3].Trim() : null,
                MtmValue = qty * price,
                Source = PositionSource.CsvImport,
                SuitabilityStatus = SuitabilityStatus.Unknown,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            count++;
        }
        portfolio.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return count;
    }

    public async Task DeletePositionAsync(Guid tenantId, Guid positionId, CancellationToken ct = default)
    {
        await EnsurePortfolioAsync(tenantId, ct);
        var pos = await _db.PortfolioPositions
            .Include(p => p.GoalPortfolio)
            .FirstOrDefaultAsync(p => p.Id == positionId && p.GoalPortfolio!.TenantId == tenantId, ct);
        if (pos is null) return;
        _db.PortfolioPositions.Remove(pos);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<ProductCatalogDto>> ListProductsAsync(
        Guid tenantId, string? family = null, bool? retailOnly = null, string? ratingMin = null, CancellationToken ct = default)
    {
        await EnsurePortfolioAsync(tenantId, ct);
        var q = _db.ProductCatalogItems.AsNoTracking()
            .Where(p => p.IsActive && (p.TenantId == null || p.TenantId == tenantId));

        if (!string.IsNullOrWhiteSpace(family) && Enum.TryParse<ProductFamily>(family, true, out var fam))
            q = q.Where(p => p.ProductFamily == fam);
        if (retailOnly == true)
            q = q.Where(p => p.FlagRetailAllowed);

        var list = await q.OrderBy(p => p.ProductFamily).ThenBy(p => p.Name)
            .Select(p => new ProductCatalogDto
            {
                Id = p.Id,
                CnpjOrTicker = p.CnpjOrTicker,
                Name = p.Name,
                ProductFamily = p.ProductFamily.ToString(),
                FlagRetailAllowed = p.FlagRetailAllowed,
                CreditRating = p.CreditRating,
                RiskRating = p.RiskRating
            })
            .ToListAsync(ct);

        // Filtro simples de rating (AAA > AA > A > BBB > BB…): se ratingMin informado, mantém ratings "melhores ou iguais"
        if (!string.IsNullOrWhiteSpace(ratingMin))
        {
            var minScore = RatingScore(ratingMin);
            list = list.Where(p => string.IsNullOrEmpty(p.CreditRating) || RatingScore(p.CreditRating) >= minScore).ToList();
        }

        return list;
    }

    private static int RatingScore(string rating)
    {
        var r = rating.Trim().ToUpperInvariant();
        if (r.StartsWith("AAA")) return 6;
        if (r.StartsWith("AA")) return 5;
        if (r.StartsWith('A')) return 4;
        if (r.StartsWith("BBB")) return 3;
        if (r.StartsWith("BB")) return 2;
        if (r.StartsWith('B')) return 1;
        return 0;
    }

    public async Task<PortfolioRecommendationDto> SendOfferAsync(Guid tenantId, Guid advisorUserId, SendOfferRequest req, CancellationToken ct = default)
    {
        await EnsurePortfolioAsync(tenantId, ct);
        var portfolio = await _db.GoalPortfolios.FirstOrDefaultAsync(p => p.Id == req.GoalPortfolioId && p.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("Carteira não encontrada.");
        var product = await _db.ProductCatalogItems.AsNoTracking().FirstOrDefaultAsync(p => p.Id == req.ProductId, ct)
            ?? throw new InvalidOperationException("Produto não encontrado.");

        var profile = await _db.ClientInvestorProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.UserId == portfolio.ClientUserId, ct);

        var suitability = EvaluateSuitability(product, profile);
        if (suitability == SuitabilityStatus.Misaligned && !req.ForceSend)
            throw new InvalidOperationException($"Produto desalinhado ao perfil (DESENQUADRADO). Use ForceSend para enviar mesmo assim. Motivo: varejo/rating.");

        var entity = new PortfolioRecommendation
        {
            Id = Guid.NewGuid(),
            GoalPortfolioId = portfolio.Id,
            ProductId = product.Id,
            AdvisorUserId = advisorUserId,
            EstimatedSharpeImpact = req.EstimatedSharpeImpact,
            SimulatedMetricsJson = req.SimulatedMetricsJson,
            Notes = req.Notes,
            Status = OfferStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = req.ExpiresAt
        };
        _db.PortfolioRecommendations.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new PortfolioRecommendationDto
        {
            Id = entity.Id,
            GoalPortfolioId = entity.GoalPortfolioId,
            ProductId = product.Id,
            ProductName = product.Name,
            ProductFamily = product.ProductFamily.ToString(),
            AdvisorUserId = advisorUserId,
            EstimatedSharpeImpact = entity.EstimatedSharpeImpact,
            Notes = entity.Notes,
            Status = entity.Status.ToString(),
            SuitabilityHint = suitability.ToString(),
            CreatedAt = entity.CreatedAt,
            // Disclaimer: aceite = intenção apenas
            Disclaimer = "Aceite registra apenas intenção no app — sem execução em corretora (v1)."
        };
    }

    public async Task<List<PortfolioRecommendationDto>> ListOffersAsync(Guid tenantId, Guid userId, string role, bool pendingOnly, CancellationToken ct = default)
    {
        await EnsurePortfolioAsync(tenantId, ct);
        var q = from r in _db.PortfolioRecommendations.AsNoTracking()
                join p in _db.GoalPortfolios.AsNoTracking() on r.GoalPortfolioId equals p.Id
                join prod in _db.ProductCatalogItems.AsNoTracking() on r.ProductId equals prod.Id
                where p.TenantId == tenantId
                select new { r, p, prod };

        if (string.Equals(role, "CLIENT", StringComparison.OrdinalIgnoreCase))
            q = q.Where(x => x.p.ClientUserId == userId);
        else if (string.Equals(role, "ADVISOR", StringComparison.OrdinalIgnoreCase))
            q = q.Where(x => x.r.AdvisorUserId == userId || x.p.AdvisorUserId == userId);

        if (pendingOnly)
            q = q.Where(x => x.r.Status == OfferStatus.Pending);

        var rows = await q.OrderByDescending(x => x.r.CreatedAt).Take(100).ToListAsync(ct);
        return rows.Select(x => new PortfolioRecommendationDto
        {
            Id = x.r.Id,
            GoalPortfolioId = x.r.GoalPortfolioId,
            PortfolioName = x.p.ObjectiveName,
            ProductId = x.prod.Id,
            ProductName = x.prod.Name,
            ProductFamily = x.prod.ProductFamily.ToString(),
            AdvisorUserId = x.r.AdvisorUserId,
            EstimatedSharpeImpact = x.r.EstimatedSharpeImpact,
            Notes = x.r.Notes,
            Status = x.r.Status.ToString(),
            CreatedAt = x.r.CreatedAt,
            ClientRespondedAt = x.r.ClientRespondedAt,
            Disclaimer = "Aceite registra apenas intenção no app — sem execução em corretora (v1)."
        }).ToList();
    }

    public async Task<PortfolioRecommendationDto?> RespondOfferAsync(Guid tenantId, Guid clientUserId, Guid offerId, bool accept, CancellationToken ct = default)
    {
        await EnsurePortfolioAsync(tenantId, ct);
        var entity = await _db.PortfolioRecommendations
            .Include(r => r.GoalPortfolio)
            .Include(r => r.Product)
            .FirstOrDefaultAsync(r => r.Id == offerId, ct);
        if (entity?.GoalPortfolio is null || entity.GoalPortfolio.TenantId != tenantId)
            return null;
        if (entity.GoalPortfolio.ClientUserId != clientUserId)
            throw new UnauthorizedAccessException("Oferta não pertence a este cliente.");
        if (entity.Status != OfferStatus.Pending)
            throw new InvalidOperationException("Oferta já respondida.");

        // Aceite = intenção apenas (sem brokerage execution) — decisão v1.
        entity.Status = accept ? OfferStatus.Accepted : OfferStatus.Rejected;
        entity.ClientRespondedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new PortfolioRecommendationDto
        {
            Id = entity.Id,
            GoalPortfolioId = entity.GoalPortfolioId,
            ProductId = entity.ProductId,
            ProductName = entity.Product?.Name,
            ProductFamily = entity.Product?.ProductFamily.ToString(),
            Status = entity.Status.ToString(),
            ClientRespondedAt = entity.ClientRespondedAt,
            Disclaimer = "Aceite registra apenas intenção no app — sem execução em corretora (v1)."
        };
    }

    private static SuitabilityStatus EvaluateSuitability(ProductCatalogItem? product, ClientInvestorProfile? profile)
    {
        if (product is null || profile is null) return SuitabilityStatus.Unknown;
        if (profile.Segment == InvestorSegment.Retail && !product.FlagRetailAllowed)
            return SuitabilityStatus.Misaligned;
        if (product.ProductFamily == ProductFamily.FIDC
            && profile.Segment == InvestorSegment.Retail
            && (string.IsNullOrEmpty(product.CreditRating)
                || product.CreditRating.StartsWith("B", StringComparison.OrdinalIgnoreCase)
                || product.CreditRating.StartsWith("C", StringComparison.OrdinalIgnoreCase)))
            return SuitabilityStatus.Misaligned;
        return SuitabilityStatus.Aligned;
    }

    private static string[] SplitCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        foreach (var ch in line)
        {
            if (ch == '"') { inQuotes = !inQuotes; continue; }
            if (ch == ',' && !inQuotes) { result.Add(sb.ToString()); sb.Clear(); continue; }
            sb.Append(ch);
        }
        result.Add(sb.ToString());
        return result.ToArray();
    }

    private static ClientInvestorProfileDto MapProfile(ClientInvestorProfile p) => new()
    {
        Id = p.Id,
        UserId = p.UserId,
        RiskProfile = p.RiskProfile.ToString(),
        Segment = p.Segment.ToString(),
        AssessedAt = p.AssessedAt
    };

    private static GoalPortfolioDto MapPortfolio(GoalPortfolio p, int positionCount, decimal mtm) => new()
    {
        Id = p.Id,
        ClientUserId = p.ClientUserId,
        AdvisorUserId = p.AdvisorUserId,
        ObjectiveName = p.ObjectiveName,
        GoalType = p.GoalType.ToString(),
        TargetDate = p.TargetDate,
        BaseCurrency = p.BaseCurrency,
        Status = p.Status.ToString(),
        AnalysisPrompt = p.AnalysisPrompt,
        LastAnalysisSummary = p.LastAnalysisSummary,
        LastAnalysisNextBestAction = p.LastAnalysisNextBestAction,
        LastAnalysisAt = p.LastAnalysisAt,
        LastAnalysisIsStub = p.LastAnalysisIsStub,
        PositionCount = positionCount,
        TotalMtm = mtm,
        UpdatedAt = p.UpdatedAt
    };

    private static PortfolioPositionDto MapPosition(PortfolioPosition p) => new()
    {
        Id = p.Id,
        ProductId = p.ProductId,
        Symbol = p.Symbol,
        CustodianLabel = p.CustodianLabel,
        Quantity = p.Quantity,
        AvgPrice = p.AvgPrice,
        WeightPct = p.WeightPct,
        TargetValue = p.TargetValue,
        MtmValue = p.MtmValue,
        Source = p.Source.ToString(),
        SuitabilityStatus = p.SuitabilityStatus.ToString()
    };
}

// DTOs
public sealed class ClientSummaryDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = "";
    public string? FullName { get; set; }
    public string? RiskProfile { get; set; }
    public string? Segment { get; set; }
    public int PortfolioCount { get; set; }
}

public sealed class ClientInvestorProfileDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string RiskProfile { get; set; } = "";
    public string Segment { get; set; } = "";
    public DateTime? AssessedAt { get; set; }
}

public sealed class UpsertProfileRequest
{
    public Guid ClientUserId { get; set; }
    public string RiskProfile { get; set; } = "Moderate";
    public string Segment { get; set; } = "Retail";
    public string? SuitabilityAnswersJson { get; set; }
}

public sealed class GoalPortfolioDto
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
    public DateTime UpdatedAt { get; set; }
    public List<PortfolioPositionDto>? Positions { get; set; }
}

public sealed class CreatePortfolioRequest
{
    public Guid ClientUserId { get; set; }
    public string ObjectiveName { get; set; } = "";
    public string GoalType { get; set; } = "Custom";
    public DateTime? TargetDate { get; set; }
    public string? BaseCurrency { get; set; }
    public string? AnalysisPrompt { get; set; }
}

public sealed class UpdatePortfolioRequest
{
    public string? ObjectiveName { get; set; }
    public string? GoalType { get; set; }
    public DateTime? TargetDate { get; set; }
    public string? Status { get; set; }
    public string? AnalysisPrompt { get; set; }
}

public sealed class PortfolioPositionDto
{
    public Guid Id { get; set; }
    public Guid? ProductId { get; set; }
    public string Symbol { get; set; } = "";
    public string? CustodianLabel { get; set; }
    public decimal Quantity { get; set; }
    public decimal AvgPrice { get; set; }
    public decimal? WeightPct { get; set; }
    public decimal? TargetValue { get; set; }
    public decimal? MtmValue { get; set; }
    public string Source { get; set; } = "";
    public string SuitabilityStatus { get; set; } = "";
}

public sealed class AddPositionRequest
{
    public Guid? ProductId { get; set; }
    public string? Symbol { get; set; }
    public string? CustodianLabel { get; set; }
    public decimal Quantity { get; set; }
    public decimal AvgPrice { get; set; }
    public decimal? WeightPct { get; set; }
    public decimal? TargetValue { get; set; }
    public decimal? MtmValue { get; set; }
}

public sealed class ProductCatalogDto
{
    public Guid Id { get; set; }
    public string CnpjOrTicker { get; set; } = "";
    public string Name { get; set; } = "";
    public string ProductFamily { get; set; } = "";
    public bool FlagRetailAllowed { get; set; }
    public string? CreditRating { get; set; }
    public int? RiskRating { get; set; }
}

public sealed class SendOfferRequest
{
    public Guid GoalPortfolioId { get; set; }
    public Guid ProductId { get; set; }
    public decimal? EstimatedSharpeImpact { get; set; }
    public string? SimulatedMetricsJson { get; set; }
    public string? Notes { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool ForceSend { get; set; }
}

public sealed class PortfolioRecommendationDto
{
    public Guid Id { get; set; }
    public Guid GoalPortfolioId { get; set; }
    public string? PortfolioName { get; set; }
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductFamily { get; set; }
    public Guid AdvisorUserId { get; set; }
    public decimal? EstimatedSharpeImpact { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "";
    public string? SuitabilityHint { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClientRespondedAt { get; set; }
    public string? Disclaimer { get; set; }
}

public sealed class RespondOfferRequest
{
    public bool Accept { get; set; }
}
