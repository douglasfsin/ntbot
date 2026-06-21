using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NtBot.TradingIntelligence.Models;
using NtBot.TradingIntelligence.Services;

namespace NtBot.Api.Controllers;

[ApiController]
[Route("api/trading-intelligence")]
[Authorize]
public class TradingIntelligenceController : ControllerBase
{
    private readonly ITradingIntelligenceService _service;

    public TradingIntelligenceController(ITradingIntelligenceService service) => _service = service;

    [HttpGet("{symbol}")]
    public async Task<IActionResult> GetSnapshot(string symbol, CancellationToken cancellationToken)
    {
        var snapshot = await _service.GetSnapshotAsync(symbol, cancellationToken: cancellationToken);
        return snapshot is null ? NotFound() : Ok(snapshot);
    }
}

[ApiController]
[Route("api/driver-compositions")]
[Authorize]
public class DriverCompositionController : ControllerBase
{
    private readonly IDriverCompositionAdminService _admin;

    public DriverCompositionController(IDriverCompositionAdminService admin) => _admin = admin;

    [HttpGet("{targetAsset}")]
    public async Task<IActionResult> List(string targetAsset, CancellationToken cancellationToken) =>
        Ok(await _admin.ListAsync(targetAsset, cancellationToken: cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DriverCompositionUpsertRequest request, CancellationToken cancellationToken)
    {
        var created = await _admin.CreateAsync(request, cancellationToken: cancellationToken);
        return created is null ? BadRequest() : Ok(created);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] DriverCompositionUpsertRequest request, CancellationToken cancellationToken)
    {
        var updated = await _admin.UpdateAsync(id, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await _admin.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("duplicate")]
    public async Task<IActionResult> Duplicate([FromBody] DuplicateCompositionRequest request, CancellationToken cancellationToken) =>
        Ok(new { copied = await _admin.DuplicateAsync(request.SourceAsset, request.TargetAsset, cancellationToken: cancellationToken) });

    [HttpGet("{targetAsset}/export")]
    public async Task<IActionResult> Export(string targetAsset, CancellationToken cancellationToken) =>
        Ok(await _admin.ExportAsync(targetAsset, cancellationToken: cancellationToken));

    [HttpPost("{targetAsset}/import")]
    public async Task<IActionResult> Import(string targetAsset, [FromBody] List<DriverCompositionUpsertRequest> items, CancellationToken cancellationToken) =>
        Ok(new { imported = await _admin.ImportAsync(targetAsset, items, cancellationToken: cancellationToken) });

    [HttpPost("reorder")]
    public async Task<IActionResult> Reorder([FromBody] ReorderCompositionRequest request, CancellationToken cancellationToken)
    {
        await _admin.ReorderAsync(request.TargetAsset, request.OrderedIds, cancellationToken: cancellationToken);
        return Ok(new { reordered = true });
    }
}
