using Microsoft.EntityFrameworkCore;
using NtBot.Domain.Entities;
using NtBot.Infrastructure.Persistence;

namespace NtBot.Api.Services.Boletagem;

/// <summary>
/// Monitor periódico: meta do dia e drawdown máximo com fechamento automático no MT5.
/// </summary>
public sealed class BoletagemMonitorWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BoletagemMonitorWorker> _logger;

    public BoletagemMonitorWorker(IServiceScopeFactory scopeFactory, ILogger<BoletagemMonitorWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Boletagem monitor falhou");
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NtBotDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IBoletagemService>();

        var active = await db.BoletaSessions
            .AsNoTracking()
            .Where(s => s.Status == BoletaSessionStatus.Active && s.AutomationEnabled)
            .Select(s => new { s.TenantId, s.Symbol })
            .Distinct()
            .ToListAsync(ct);

        foreach (var item in active)
        {
            try
            {
                await service.MonitorAsync(item.TenantId, item.Symbol, ct);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Monitor boleta {Symbol} tenant {Tenant}", item.Symbol, item.TenantId);
            }
        }
    }
}
