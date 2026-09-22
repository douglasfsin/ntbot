using NtBot.Connector.Windows.Providers.Profit;
using NtBot.Connector.Windows.SignalR;

namespace NtBot.Connector.Windows.Workers;

/// <summary>
/// Sincroniza o ReplayMode DDE com a API (botão nas páginas WIN/WDO do Web).
/// </summary>
public sealed class DdeReplaySyncWorker : BackgroundService
{
    private readonly IDdeReplayController _replay;
    private readonly INtBotApiClient _api;
    private readonly ILogger<DdeReplaySyncWorker> _logger;
    private DateTime _lastAppliedUtc = DateTime.MinValue;

    public DdeReplaySyncWorker(
        IDdeReplayController replay,
        INtBotApiClient api,
        ILogger<DdeReplaySyncWorker> logger)
    {
        _replay = replay;
        _api = api;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(12), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Falha ao sincronizar DDE Replay com a API");
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    private async Task SyncOnceAsync(CancellationToken ct)
    {
        if (!_api.IsConfigured)
            return;

        await _api.EnsureSessionAsync(ct);
        var command = await _api.GetDdeReplayCommandAsync(ct);
        if (command is null)
            return;

        if (command.UpdatedUtc <= _lastAppliedUtc)
            return;

        var local = _replay.GetStatus();
        var contractsEqual = ContractsEqual(local.Contracts, command.Contracts);
        if (local.Enabled == command.Enabled && contractsEqual)
        {
            _lastAppliedUtc = command.UpdatedUtc;
            return;
        }

        _logger.LogInformation(
            "Aplicando DDE Replay da API: Enabled={Enabled} UpdatedBy={UpdatedBy}",
            command.Enabled,
            command.UpdatedBy);

        _replay.SetReplayMode(command.Enabled, command.Contracts);
        _lastAppliedUtc = command.UpdatedUtc;
        await _api.AckDdeReplayAsync(command.Enabled, command.Contracts, ct);
    }

    private static bool ContractsEqual(
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right)
    {
        if (left.Count != right.Count)
            return false;

        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var other)
                || !value.Equals(other, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }
}
