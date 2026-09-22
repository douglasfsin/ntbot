using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NtBot.Api.Services.Portfolio;
using NtBot.Api.Services.WhiteLabel;

namespace NtBot.Api.Controllers;

[ApiController]
[Route("api/portfolio")]
[Authorize]
public sealed class PortfolioController : ControllerBase
{
    private readonly IPortfolioService _portfolio;
    private readonly IPortfolioReportService _reports;
    private readonly IPerformanceService _performance;
    private readonly IPortfolioAnalysisService _analysis;
    private readonly IOpenFinanceProvider _openFinance;
    private readonly IPortfolioValuationService _valuations;
    private readonly ITenantFeatureService _features;

    public PortfolioController(
        IPortfolioService portfolio,
        IPortfolioReportService reports,
        IPerformanceService performance,
        IPortfolioAnalysisService analysis,
        IOpenFinanceProvider openFinance,
        IPortfolioValuationService valuations,
        ITenantFeatureService features)
    {
        _portfolio = portfolio;
        _reports = reports;
        _performance = performance;
        _analysis = analysis;
        _openFinance = openFinance;
        _valuations = valuations;
        _features = features;
    }

    [HttpGet("features")]
    public async Task<IActionResult> Features(CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty) return Unauthorized();
        return Ok(await _features.GetSnapshotAsync(tenantId, ct));
    }

    [HttpGet("clients")]
    [Authorize(Roles = "ADMIN,ADVISOR")]
    public async Task<IActionResult> Clients(CancellationToken ct)
    {
        try
        {
            return Ok(await _portfolio.ListClientsAsync(GetTenantId(), ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("clients/{clientUserId:guid}/profile")]
    [Authorize(Roles = "ADMIN,ADVISOR,CLIENT")]
    public async Task<IActionResult> GetProfile(Guid clientUserId, CancellationToken ct)
    {
        if (IsClient() && clientUserId != GetUserId()) return Forbid();
        return Ok(await _portfolio.GetProfileAsync(GetTenantId(), clientUserId, ct));
    }

    [HttpPut("clients/profile")]
    [Authorize(Roles = "ADMIN,ADVISOR")]
    public async Task<IActionResult> UpsertProfile([FromBody] UpsertProfileRequest req, CancellationToken ct)
    {
        try
        {
            return Ok(await _portfolio.UpsertProfileAsync(GetTenantId(), GetUserId(), req, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("portfolios")]
    public async Task<IActionResult> ListPortfolios([FromQuery] Guid? clientUserId, CancellationToken ct)
    {
        try
        {
            var role = GetRole();
            return Ok(await _portfolio.ListPortfoliosAsync(GetTenantId(), clientUserId, GetUserId(), role, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("portfolios/{id:guid}")]
    public async Task<IActionResult> GetPortfolio(Guid id, CancellationToken ct)
    {
        var dto = await _portfolio.GetPortfolioAsync(GetTenantId(), id, ct);
        if (dto is null) return NotFound();
        if (IsClient() && dto.ClientUserId != GetUserId()) return Forbid();
        return Ok(dto);
    }

    [HttpPost("portfolios")]
    [Authorize(Roles = "ADMIN,ADVISOR")]
    public async Task<IActionResult> CreatePortfolio([FromBody] CreatePortfolioRequest req, CancellationToken ct)
    {
        try
        {
            return Ok(await _portfolio.CreatePortfolioAsync(GetTenantId(), GetUserId(), req, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("portfolios/{id:guid}")]
    [Authorize(Roles = "ADMIN,ADVISOR")]
    public async Task<IActionResult> UpdatePortfolio(Guid id, [FromBody] UpdatePortfolioRequest req, CancellationToken ct)
    {
        var dto = await _portfolio.UpdatePortfolioAsync(GetTenantId(), id, req, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("portfolios/{id:guid}/positions")]
    [Authorize(Roles = "ADMIN,ADVISOR")]
    public async Task<IActionResult> AddPosition(Guid id, [FromBody] AddPositionRequest req, CancellationToken ct)
    {
        try
        {
            return Ok(await _portfolio.AddPositionAsync(GetTenantId(), id, req, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("portfolios/{id:guid}/import-csv")]
    [Authorize(Roles = "ADMIN,ADVISOR")]
    public async Task<IActionResult> ImportCsv(Guid id, [FromBody] CsvImportRequest req, CancellationToken ct)
    {
        try
        {
            var count = await _portfolio.ImportCsvAsync(GetTenantId(), id, req.CsvContent ?? "", ct);
            return Ok(new { imported = count });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("positions/{positionId:guid}")]
    [Authorize(Roles = "ADMIN,ADVISOR")]
    public async Task<IActionResult> DeletePosition(Guid positionId, CancellationToken ct)
    {
        await _portfolio.DeletePositionAsync(GetTenantId(), positionId, ct);
        return NoContent();
    }

    [HttpGet("products")]
    public async Task<IActionResult> Products(
        [FromQuery] string? family,
        [FromQuery] bool? retailOnly,
        [FromQuery] string? ratingMin,
        CancellationToken ct)
    {
        try
        {
            return Ok(await _portfolio.ListProductsAsync(GetTenantId(), family, retailOnly, ratingMin, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("offers")]
    [Authorize(Roles = "ADMIN,ADVISOR")]
    public async Task<IActionResult> SendOffer([FromBody] SendOfferRequest req, CancellationToken ct)
    {
        try
        {
            return Ok(await _portfolio.SendOfferAsync(GetTenantId(), GetUserId(), req, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("offers")]
    public async Task<IActionResult> ListOffers([FromQuery] bool pendingOnly = false, CancellationToken ct = default)
    {
        return Ok(await _portfolio.ListOffersAsync(GetTenantId(), GetUserId(), GetRole(), pendingOnly, ct));
    }

    [HttpPost("offers/{offerId:guid}/respond")]
    [Authorize(Roles = "CLIENT,ADMIN")]
    public async Task<IActionResult> RespondOffer(Guid offerId, [FromBody] RespondOfferRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _portfolio.RespondOfferAsync(GetTenantId(), GetUserId(), offerId, req.Accept, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("portfolios/{id:guid}/performance")]
    public async Task<IActionResult> Performance(Guid id, CancellationToken ct)
        => Ok(await _performance.GetPerformanceAsync(GetTenantId(), id, ct));

    [HttpPost("portfolios/{id:guid}/valuations/capture")]
    [Authorize(Roles = "ADMIN,ADVISOR,CLIENT")]
    public async Task<IActionResult> CaptureValuation(Guid id, [FromBody] CaptureValuationRequest? req, CancellationToken ct)
    {
        try
        {
            var dto = await _portfolio.GetPortfolioAsync(GetTenantId(), id, ct);
            if (dto is null) return NotFound();
            if (IsClient() && dto.ClientUserId != GetUserId()) return Forbid();
            return Ok(await _valuations.CaptureAsync(GetTenantId(), id, req?.Notes, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("portfolios/{id:guid}/valuations")]
    public async Task<IActionResult> ListValuations(Guid id, CancellationToken ct)
    {
        var dto = await _portfolio.GetPortfolioAsync(GetTenantId(), id, ct);
        if (dto is null) return NotFound();
        if (IsClient() && dto.ClientUserId != GetUserId()) return Forbid();
        return Ok(await _valuations.ListSnapshotsAsync(GetTenantId(), id, ct));
    }

    [HttpPost("portfolios/{id:guid}/cashflows")]
    [Authorize(Roles = "ADMIN,ADVISOR")]
    public async Task<IActionResult> AddCashflow(Guid id, [FromBody] AddCashflowRequest req, CancellationToken ct)
    {
        try
        {
            return Ok(await _valuations.AddCashflowAsync(GetTenantId(), id, req, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("portfolios/{id:guid}/cashflows")]
    public async Task<IActionResult> ListCashflows(Guid id, CancellationToken ct)
    {
        var dto = await _portfolio.GetPortfolioAsync(GetTenantId(), id, ct);
        if (dto is null) return NotFound();
        if (IsClient() && dto.ClientUserId != GetUserId()) return Forbid();
        return Ok(await _valuations.ListCashflowsAsync(GetTenantId(), id, ct));
    }

    [HttpPost("portfolios/{id:guid}/analyze")]
    [Authorize(Roles = "ADMIN,ADVISOR")]
    public async Task<IActionResult> Analyze(Guid id, [FromBody] PortfolioAnalyzeRequest? req, CancellationToken ct)
    {
        var result = await _analysis.AnalyzeAsync(new PortfolioAnalysisRequest
        {
            TenantId = GetTenantId(),
            GoalPortfolioId = id,
            CustomPrompt = req?.CustomPrompt
        }, ct);
        return Ok(result);
    }

    [HttpPost("portfolios/{id:guid}/report")]
    [Authorize(Roles = "ADMIN,ADVISOR")]
    public async Task<IActionResult> Report(Guid id, [FromBody] ReportRequest? req, CancellationToken ct)
    {
        try
        {
            var send = req?.SendEmail == true;
            var job = await _reports.GenerateAsync(GetTenantId(), GetUserId(), id, send, req?.Email, ct);
            return Ok(job);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("reports")]
    [Authorize(Roles = "ADMIN,ADVISOR")]
    public async Task<IActionResult> Reports(CancellationToken ct)
        => Ok(await _reports.ListAsync(GetTenantId(), ct));

    [HttpGet("reports/{jobId:guid}")]
    [Authorize(Roles = "ADMIN,ADVISOR")]
    public async Task<IActionResult> GetReport(Guid jobId, CancellationToken ct)
    {
        var job = await _reports.GetAsync(GetTenantId(), jobId, ct);
        return job is null ? NotFound() : Ok(job);
    }

    [HttpGet("reports/{jobId:guid}/pdf")]
    [Authorize(Roles = "ADMIN,ADVISOR,CLIENT")]
    public async Task<IActionResult> GetReportPdf(Guid jobId, CancellationToken ct)
    {
        var pdf = await _reports.GetPdfAsync(GetTenantId(), jobId, ct);
        if (pdf is null) return NotFound();
        return File(pdf.Value.Pdf, "application/pdf", pdf.Value.FileName);
    }

    [HttpGet("open-finance/status")]
    public async Task<IActionResult> OpenFinanceStatus(CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var clientId = IsClient() ? GetUserId() : (Guid?)null;
        var consents = await _openFinance.ListConsentsAsync(tenantId, clientId, ct);
        return Ok(new
        {
            provider = _openFinance.ProviderName,
            available = _openFinance.IsSimulation,
            simulation = _openFinance.IsSimulation,
            message = _openFinance.IsSimulation
                ? "Open Finance simulado (demo). Solicite → Conceda → Importe posições mock XP/BTG/Safra. Produção exige parceiro ITP/FAPI-BR."
                : "Open Finance NoOp — sem importação.",
            consentCount = consents.Count,
            activeCount = consents.Count(c => c.Status == "Active")
        });
    }

    [HttpGet("open-finance/consents")]
    [Authorize(Roles = "ADMIN,ADVISOR,CLIENT")]
    public async Task<IActionResult> ListConsents([FromQuery] Guid? clientUserId, CancellationToken ct)
    {
        var uid = IsClient() ? GetUserId() : clientUserId;
        if (IsClient()) uid = GetUserId();
        return Ok(await _openFinance.ListConsentsAsync(GetTenantId(), uid, ct));
    }

    [HttpPost("open-finance/consents")]
    [Authorize(Roles = "ADMIN,ADVISOR,CLIENT")]
    public async Task<IActionResult> RequestConsent([FromBody] ConsentDraftRequest req, CancellationToken ct)
    {
        var clientId = req.ClientUserId == Guid.Empty ? GetUserId() : req.ClientUserId;
        if (IsClient() && clientId != GetUserId()) return Forbid();
        var dto = await _openFinance.RequestConsentAsync(GetTenantId(), clientId, req.Provider ?? "XP", req.ScopeJson, ct);
        return Ok(dto);
    }

    [HttpPost("open-finance/consents/{id:guid}/grant")]
    [Authorize(Roles = "ADMIN,ADVISOR,CLIENT")]
    public async Task<IActionResult> GrantConsent(Guid id, CancellationToken ct)
    {
        var existing = (await _openFinance.ListConsentsAsync(GetTenantId(), null, ct))
            .FirstOrDefault(c => c.Id == id);
        if (existing is null) return NotFound();
        if (IsClient() && existing.ClientUserId != GetUserId()) return Forbid();
        var dto = await _openFinance.GrantConsentAsync(GetTenantId(), id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("open-finance/consents/{id:guid}/revoke")]
    [Authorize(Roles = "ADMIN,ADVISOR,CLIENT")]
    public async Task<IActionResult> RevokeConsent(Guid id, CancellationToken ct)
    {
        var existing = (await _openFinance.ListConsentsAsync(GetTenantId(), null, ct))
            .FirstOrDefault(c => c.Id == id);
        if (existing is null) return NotFound();
        if (IsClient() && existing.ClientUserId != GetUserId()) return Forbid();
        var dto = await _openFinance.RevokeConsentAsync(GetTenantId(), id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("open-finance/import")]
    [Authorize(Roles = "ADMIN,ADVISOR,CLIENT")]
    public async Task<IActionResult> ImportOpenFinance([FromBody] OpenFinanceImportRequest req, CancellationToken ct)
    {
        try
        {
            var portfolio = await _portfolio.GetPortfolioAsync(GetTenantId(), req.PortfolioId, ct);
            if (portfolio is null) return NotFound();
            if (IsClient() && portfolio.ClientUserId != GetUserId()) return Forbid();
            var clientId = IsClient() ? GetUserId() : portfolio.ClientUserId;
            var result = await _openFinance.ImportPositionsAsync(
                GetTenantId(), clientId, req.PortfolioId, req.ConsentId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Compat: draft antigo → request consent.</summary>
    [HttpPost("open-finance/consent-draft")]
    [Authorize(Roles = "ADMIN,ADVISOR,CLIENT")]
    public async Task<IActionResult> ConsentDraft([FromBody] ConsentDraftRequest req, CancellationToken ct)
    {
        var clientId = req.ClientUserId == Guid.Empty ? GetUserId() : req.ClientUserId;
        if (IsClient() && clientId != GetUserId()) return Forbid();
        var dto = await _openFinance.RequestConsentAsync(GetTenantId(), clientId, req.Provider ?? "XP", req.ScopeJson, ct);
        return Ok(dto);
    }

    private bool IsClient() => string.Equals(GetRole(), "CLIENT", StringComparison.OrdinalIgnoreCase);

    private Guid GetTenantId()
    {
        var claim = User.FindFirst("tenant_id")?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value
                    ?? User.FindFirst("user_id")?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    private string GetRole() => User.FindFirst(ClaimTypes.Role)?.Value ?? "";
}

public sealed class CsvImportRequest
{
    public string? CsvContent { get; set; }
}

public sealed class PortfolioAnalyzeRequest
{
    public string? CustomPrompt { get; set; }
}

public sealed class ReportRequest
{
    public string? Email { get; set; }
    public bool SendEmail { get; set; }
}

public sealed class ConsentDraftRequest
{
    public Guid ClientUserId { get; set; }
    public string? Provider { get; set; }
    public string? ScopeJson { get; set; }
}

public sealed class CaptureValuationRequest
{
    public string? Notes { get; set; }
}

public sealed class OpenFinanceImportRequest
{
    public Guid PortfolioId { get; set; }
    public Guid? ConsentId { get; set; }
}
