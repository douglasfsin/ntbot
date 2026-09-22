using Microsoft.EntityFrameworkCore;
using NtBot.Domain.Entities.Portfolio;
using NtBot.Infrastructure.Persistence;

namespace NtBot.Api.Services.Portfolio;

/// <summary>
/// Contrato Open Finance Brasil — pronto para adapter FAPI-BR real (mTLS / ITP).
/// Produção com XP/BTG/Safra exige parceiro; v1 = shell + simulação.
/// </summary>
public interface IOpenFinanceProvider
{
    string ProviderName { get; }
    bool IsSimulation { get; }

    Task<IReadOnlyList<OpenFinancePositionDto>> FetchPositionsAsync(
        Guid tenantId, Guid clientUserId, string? providerFilter = null, CancellationToken ct = default);

    Task<OpenFinanceConsentDto> RequestConsentAsync(
        Guid tenantId, Guid clientUserId, string provider, string? scopeJson, CancellationToken ct = default);

    /// <summary>Simula redirect do banco / grant FAPI — Pending → Active.</summary>
    Task<OpenFinanceConsentDto?> GrantConsentAsync(Guid tenantId, Guid consentId, CancellationToken ct = default);

    Task<OpenFinanceConsentDto?> RevokeConsentAsync(Guid tenantId, Guid consentId, CancellationToken ct = default);

    Task<List<OpenFinanceConsentDto>> ListConsentsAsync(
        Guid tenantId, Guid? clientUserId, CancellationToken ct = default);

    /// <summary>
    /// Importa posições mock (ou futuras posições reais) para a carteira após consent Active.
    /// </summary>
    Task<OpenFinanceImportResultDto> ImportPositionsAsync(
        Guid tenantId, Guid clientUserId, Guid portfolioId, Guid? consentId, CancellationToken ct = default);
}

/// <summary>
/// Simulação demo: ciclo Request → Grant → Import de posições XP/BTG/Safra.
/// Sem credenciais reais. Substituir por adapter FAPI-BR quando houver parceiro ITP.
/// </summary>
public sealed class SimulatedOpenFinanceProvider : IOpenFinanceProvider
{
    private readonly NtBotDbContext _db;
    private readonly ILogger<SimulatedOpenFinanceProvider> _logger;

    public SimulatedOpenFinanceProvider(NtBotDbContext db, ILogger<SimulatedOpenFinanceProvider> logger)
    {
        _db = db;
        _logger = logger;
    }

    public string ProviderName => "Simulated";
    public bool IsSimulation => true;

    public Task<IReadOnlyList<OpenFinancePositionDto>> FetchPositionsAsync(
        Guid tenantId, Guid clientUserId, string? providerFilter = null, CancellationToken ct = default)
    {
        var hasActive = _db.OpenFinanceConsents.AsNoTracking()
            .Any(c => c.TenantId == tenantId && c.ClientUserId == clientUserId && c.Status == "Active"
                      && (providerFilter == null || c.Provider == providerFilter));

        if (!hasActive)
        {
            _logger.LogInformation(
                "[OpenFinance:Sim] FetchPositions sem consent Active tenant={TenantId} client={ClientId}",
                tenantId, clientUserId);
            return Task.FromResult<IReadOnlyList<OpenFinancePositionDto>>(Array.Empty<OpenFinancePositionDto>());
        }

        var providers = _db.OpenFinanceConsents.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.ClientUserId == clientUserId && c.Status == "Active")
            .Select(c => c.Provider)
            .Distinct()
            .ToList();

        if (!string.IsNullOrWhiteSpace(providerFilter))
            providers = providers.Where(p => p.Equals(providerFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        var list = new List<OpenFinancePositionDto>();
        foreach (var p in providers)
            list.AddRange(MockPositionsFor(p));

        return Task.FromResult<IReadOnlyList<OpenFinancePositionDto>>(list);
    }

    public async Task<OpenFinanceConsentDto> RequestConsentAsync(
        Guid tenantId, Guid clientUserId, string provider, string? scopeJson, CancellationToken ct = default)
    {
        var normalized = NormalizeProvider(provider);
        var entity = new OpenFinanceConsent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ClientUserId = clientUserId,
            Provider = normalized,
            ScopeJson = scopeJson ?? """{"accounts":true,"investments":true,"sim":true}""",
            Status = "Pending",
            ExpiresAt = DateTime.UtcNow.AddDays(90),
            AuditLogRef = $"sim-{Guid.NewGuid():N}"[..32],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.OpenFinanceConsents.Add(entity);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "[OpenFinance:Sim] Consent Pending id={Id} provider={Provider} — aguardando Grant (demo)",
            entity.Id, normalized);

        return Map(entity, "Consentimento Pending (simulado). Clique em Conceder para ativar e importar posições mock.");
    }

    public async Task<OpenFinanceConsentDto?> GrantConsentAsync(Guid tenantId, Guid consentId, CancellationToken ct = default)
    {
        var entity = await _db.OpenFinanceConsents
            .FirstOrDefaultAsync(c => c.Id == consentId && c.TenantId == tenantId, ct);
        if (entity is null) return null;
        if (entity.Status == "Revoked")
            return Map(entity, "Consentimento já revogado.");

        entity.Status = "Active";
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[OpenFinance:Sim] Consent Granted id={Id} provider={Provider}", entity.Id, entity.Provider);
        return Map(entity, "Consentimento Active (simulado). Pode importar posições mock para a carteira.");
    }

    public async Task<OpenFinanceConsentDto?> RevokeConsentAsync(Guid tenantId, Guid consentId, CancellationToken ct = default)
    {
        var entity = await _db.OpenFinanceConsents
            .FirstOrDefaultAsync(c => c.Id == consentId && c.TenantId == tenantId, ct);
        if (entity is null) return null;

        entity.Status = "Revoked";
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[OpenFinance:Sim] Consent revoked id={Id}", entity.Id);
        return Map(entity, "Consentimento revogado (simulado).");
    }

    public async Task<List<OpenFinanceConsentDto>> ListConsentsAsync(
        Guid tenantId, Guid? clientUserId, CancellationToken ct = default)
    {
        var q = _db.OpenFinanceConsents.AsNoTracking().Where(c => c.TenantId == tenantId);
        if (clientUserId.HasValue)
            q = q.Where(c => c.ClientUserId == clientUserId.Value);

        var list = await q.OrderByDescending(c => c.CreatedAt).Take(100).ToListAsync(ct);
        return list.Select(c => Map(c, null)).ToList();
    }

    public async Task<OpenFinanceImportResultDto> ImportPositionsAsync(
        Guid tenantId, Guid clientUserId, Guid portfolioId, Guid? consentId, CancellationToken ct = default)
    {
        var portfolio = await _db.GoalPortfolios
            .Include(p => p.Positions)
            .FirstOrDefaultAsync(p => p.Id == portfolioId && p.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("Carteira não encontrada.");

        if (portfolio.ClientUserId != clientUserId && clientUserId != Guid.Empty)
        {
            // Assessor pode importar para o cliente da carteira
            clientUserId = portfolio.ClientUserId;
        }

        var consentsQ = _db.OpenFinanceConsents
            .Where(c => c.TenantId == tenantId && c.ClientUserId == portfolio.ClientUserId && c.Status == "Active");
        if (consentId.HasValue)
            consentsQ = consentsQ.Where(c => c.Id == consentId.Value);

        var active = await consentsQ.ToListAsync(ct);
        if (active.Count == 0)
            throw new InvalidOperationException("Nenhum consentimento Active. Solicite e conceda antes de importar.");

        var imported = 0;
        foreach (var consent in active)
        {
            foreach (var pos in MockPositionsFor(consent.Provider))
            {
                var existing = portfolio.Positions
                    .FirstOrDefault(p => p.Symbol == pos.Symbol && p.CustodianLabel == pos.Custodian
                                         && p.Source == PositionSource.OpenFinance);
                if (existing is not null)
                {
                    existing.Quantity = pos.Quantity;
                    existing.MtmValue = pos.MtmValue;
                    existing.AvgPrice = pos.Quantity > 0 ? (pos.MtmValue ?? 0) / pos.Quantity : existing.AvgPrice;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    var qty = pos.Quantity;
                    var mtm = pos.MtmValue ?? 0;
                    _db.PortfolioPositions.Add(new PortfolioPosition
                    {
                        Id = Guid.NewGuid(),
                        GoalPortfolioId = portfolioId,
                        Symbol = pos.Symbol,
                        CustodianLabel = pos.Custodian,
                        Quantity = qty,
                        AvgPrice = qty > 0 ? mtm / qty : 0,
                        MtmValue = mtm,
                        Source = PositionSource.OpenFinance,
                        SuitabilityStatus = SuitabilityStatus.Unknown,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                imported++;
            }
        }

        portfolio.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "[OpenFinance:Sim] Imported {Count} positions portfolio={PortfolioId} providers={Providers}",
            imported, portfolioId, string.Join(",", active.Select(a => a.Provider)));

        return new OpenFinanceImportResultDto
        {
            Imported = imported,
            Providers = active.Select(a => a.Provider).Distinct().ToList(),
            Message = $"Importação simulada: {imported} posição(ões) de {string.Join(", ", active.Select(a => a.Provider).Distinct())}."
        };
    }

    private static string NormalizeProvider(string? provider)
    {
        var p = (provider ?? "XP").Trim();
        return p.ToUpperInvariant() switch
        {
            "XP" => "XP",
            "BTG" => "BTG",
            "SAFRA" => "Safra",
            "STUB" => "Stub",
            _ => p.Length > 40 ? p[..40] : p
        };
    }

    private static IEnumerable<OpenFinancePositionDto> MockPositionsFor(string provider) => provider switch
    {
        "XP" =>
        [
            new() { Symbol = "XP-FIF-LIQ", Quantity = 1000, MtmValue = 10500, Custodian = "XP" },
            new() { Symbol = "HGLG11", Quantity = 50, MtmValue = 8250, Custodian = "XP" }
        ],
        "BTG" =>
        [
            new() { Symbol = "BTG-FIDC-SR", Quantity = 200, MtmValue = 22000, Custodian = "BTG" },
            new() { Symbol = "PETR4", Quantity = 100, MtmValue = 3800, Custodian = "BTG" }
        ],
        "Safra" =>
        [
            new() { Symbol = "SAFRA-RF-CDI", Quantity = 5000, MtmValue = 5120, Custodian = "Safra" },
            new() { Symbol = "KNRI11", Quantity = 30, MtmValue = 4800, Custodian = "Safra" }
        ],
        _ =>
        [
            new() { Symbol = "STUB-CAIXA", Quantity = 100, MtmValue = 10000, Custodian = "Stub" }
        ]
    };

    private static OpenFinanceConsentDto Map(OpenFinanceConsent c, string? message) => new()
    {
        Id = c.Id,
        ClientUserId = c.ClientUserId,
        Provider = c.Provider,
        Status = c.Status,
        ScopeJson = c.ScopeJson,
        ExpiresAt = c.ExpiresAt,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
        Message = message
    };
}

/// <summary>NoOp legado — mantido para testes / feature flag.</summary>
public sealed class NoOpOpenFinanceProvider : IOpenFinanceProvider
{
    private readonly NtBotDbContext _db;
    private readonly ILogger<NoOpOpenFinanceProvider> _logger;

    public NoOpOpenFinanceProvider(NtBotDbContext db, ILogger<NoOpOpenFinanceProvider> logger)
    {
        _db = db;
        _logger = logger;
    }

    public string ProviderName => "NoOp";
    public bool IsSimulation => false;

    public Task<IReadOnlyList<OpenFinancePositionDto>> FetchPositionsAsync(
        Guid tenantId, Guid clientUserId, string? providerFilter = null, CancellationToken ct = default)
    {
        _logger.LogInformation("[OpenFinance:NoOp] FetchPositions — vazio");
        return Task.FromResult<IReadOnlyList<OpenFinancePositionDto>>(Array.Empty<OpenFinancePositionDto>());
    }

    public async Task<OpenFinanceConsentDto> RequestConsentAsync(
        Guid tenantId, Guid clientUserId, string provider, string? scopeJson, CancellationToken ct = default)
    {
        var entity = new OpenFinanceConsent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ClientUserId = clientUserId,
            Provider = string.IsNullOrWhiteSpace(provider) ? "Stub" : provider.Trim(),
            ScopeJson = scopeJson ?? """{"accounts":true,"investments":true}""",
            Status = "Active",
            ExpiresAt = DateTime.UtcNow.AddDays(90),
            AuditLogRef = $"noop-{Guid.NewGuid():N}"[..32],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.OpenFinanceConsents.Add(entity);
        await _db.SaveChangesAsync(ct);
        return new OpenFinanceConsentDto
        {
            Id = entity.Id,
            ClientUserId = entity.ClientUserId,
            Provider = entity.Provider,
            Status = entity.Status,
            ScopeJson = entity.ScopeJson,
            ExpiresAt = entity.ExpiresAt,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            Message = "NoOp — Active imediato, sem importação."
        };
    }

    public async Task<OpenFinanceConsentDto?> GrantConsentAsync(Guid tenantId, Guid consentId, CancellationToken ct = default)
    {
        var e = await _db.OpenFinanceConsents.FirstOrDefaultAsync(c => c.Id == consentId && c.TenantId == tenantId, ct);
        if (e is null) return null;
        e.Status = "Active";
        e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new OpenFinanceConsentDto
        {
            Id = e.Id,
            ClientUserId = e.ClientUserId,
            Provider = e.Provider,
            Status = e.Status,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt,
            Message = "Granted (NoOp)"
        };
    }

    public async Task<OpenFinanceConsentDto?> RevokeConsentAsync(Guid tenantId, Guid consentId, CancellationToken ct = default)
    {
        var entity = await _db.OpenFinanceConsents
            .FirstOrDefaultAsync(c => c.Id == consentId && c.TenantId == tenantId, ct);
        if (entity is null) return null;
        entity.Status = "Revoked";
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new OpenFinanceConsentDto
        {
            Id = entity.Id,
            ClientUserId = entity.ClientUserId,
            Provider = entity.Provider,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            Message = "Revogado (NoOp)"
        };
    }

    public async Task<List<OpenFinanceConsentDto>> ListConsentsAsync(
        Guid tenantId, Guid? clientUserId, CancellationToken ct = default)
    {
        var q = _db.OpenFinanceConsents.AsNoTracking().Where(c => c.TenantId == tenantId);
        if (clientUserId.HasValue)
            q = q.Where(c => c.ClientUserId == clientUserId.Value);
        return await q.OrderByDescending(c => c.CreatedAt).Take(100)
            .Select(c => new OpenFinanceConsentDto
            {
                Id = c.Id,
                ClientUserId = c.ClientUserId,
                Provider = c.Provider,
                Status = c.Status,
                ScopeJson = c.ScopeJson,
                ExpiresAt = c.ExpiresAt,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            }).ToListAsync(ct);
    }

    public Task<OpenFinanceImportResultDto> ImportPositionsAsync(
        Guid tenantId, Guid clientUserId, Guid portfolioId, Guid? consentId, CancellationToken ct = default)
        => throw new InvalidOperationException("NoOp não importa posições. Use o provider Simulated.");
}

public sealed class OpenFinancePositionDto
{
    public string Symbol { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal? MtmValue { get; set; }
    public string? Custodian { get; set; }
}

public sealed class OpenFinanceConsentDto
{
    public Guid Id { get; set; }
    public Guid ClientUserId { get; set; }
    public string Provider { get; set; } = "";
    public string Status { get; set; } = "";
    public string? ScopeJson { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? Message { get; set; }
}

public sealed class OpenFinanceImportResultDto
{
    public int Imported { get; set; }
    public List<string> Providers { get; set; } = new();
    public string Message { get; set; } = "";
}
