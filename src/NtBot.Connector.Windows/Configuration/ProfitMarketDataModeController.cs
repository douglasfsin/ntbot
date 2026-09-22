using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace NtBot.Connector.Windows.Configuration;

public interface IProfitMarketDataModeController
{
    ProfitMarketDataMode Current { get; }
    event Action<ProfitMarketDataMode>? ModeChanged;
    ProfitMarketDataMode GetEffectiveMode();
    void SetMode(ProfitMarketDataMode mode, bool persist = true);
    /// <summary>Acorda waiters sem alterar o modo (ex.: toggle de RTD fallback).</summary>
    void NotifySettingsChanged();
    Task WaitForModeAsync(ProfitMarketDataMode desired, CancellationToken cancellationToken);
    Task WaitForChangeAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Controla a fonte ativa de ticks Profit (DDE / RTD / ProfitDLL),
/// persiste em appsettings.json e notifica os providers.
/// </summary>
public sealed class ProfitMarketDataModeController : IProfitMarketDataModeController
{
    private readonly object _gate = new();
    private readonly IOptionsMonitor<ConnectorOptions> _options;
    private readonly ILogger<ProfitMarketDataModeController> _logger;
    private ProfitMarketDataMode _current;
    private TaskCompletionSource _changed = NewTcs();

    public ProfitMarketDataModeController(
        IOptionsMonitor<ConnectorOptions> options,
        ILogger<ProfitMarketDataModeController> logger)
    {
        _options = options;
        _logger = logger;
        _current = options.CurrentValue.ResolveProfitMarketDataMode();
        _logger.LogInformation(
            "Profit market data mode inicial: {Mode} ({Display})",
            _current,
            _current.ToDisplayName());
    }

    public ProfitMarketDataMode Current
    {
        get { lock (_gate) return _current; }
    }

    public event Action<ProfitMarketDataMode>? ModeChanged;

    public ProfitMarketDataMode GetEffectiveMode() => Current;

    public void SetMode(ProfitMarketDataMode mode, bool persist = true)
    {
        ProfitMarketDataMode previous;
        lock (_gate)
        {
            previous = _current;
            if (previous == mode)
            {
                if (persist)
                    Persist(mode);
                return;
            }

            _current = mode;
            var oldTcs = _changed;
            _changed = NewTcs();
            oldTcs.TrySetResult();
        }

        _logger.LogInformation(
            "Profit market data mode: {From} → {To} ({Display})",
            previous,
            mode,
            mode.ToDisplayName());

        if (persist)
            Persist(mode);

        ModeChanged?.Invoke(mode);
    }

    public void NotifySettingsChanged()
    {
        lock (_gate)
        {
            var oldTcs = _changed;
            _changed = NewTcs();
            oldTcs.TrySetResult();
        }

        ModeChanged?.Invoke(Current);
    }

    public async Task WaitForModeAsync(ProfitMarketDataMode desired, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (Current == desired)
                return;

            var wait = GetChangedTask();
            await Task.WhenAny(wait, Task.Delay(Timeout.Infinite, cancellationToken));
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    public async Task WaitForChangeAsync(CancellationToken cancellationToken)
    {
        var wait = GetChangedTask();
        await Task.WhenAny(wait, Task.Delay(Timeout.Infinite, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
    }

    private Task GetChangedTask()
    {
        lock (_gate)
            return _changed.Task;
    }

    private void Persist(ProfitMarketDataMode mode)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path))
            {
                _logger.LogWarning("appsettings.json não encontrado em {Path} — modo só em memória", path);
                return;
            }

            var root = JsonNode.Parse(File.ReadAllText(path)) ?? new JsonObject();
            var connector = root["Connector"] as JsonObject ?? new JsonObject();
            root["Connector"] = connector;

            connector["ProfitMarketDataMode"] = mode.ToString();

            // Mantém flags legadas alinhadas ao modo (compatibilidade).
            switch (mode)
            {
                case ProfitMarketDataMode.Dde:
                    connector["EnableProfitDde"] = true;
                    connector["EnableProfit"] = false;
                    break;
                case ProfitMarketDataMode.Rtd:
                    connector["EnableProfitDde"] = false;
                    connector["EnableProfit"] = true;
                    break;
                case ProfitMarketDataMode.ProfitDll:
                    connector["EnableProfitDde"] = false;
                    connector["EnableProfit"] = false;
                    break;
            }

            var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
            _logger.LogInformation("ProfitMarketDataMode persistido em {Path}: {Mode}", path, mode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao persistir ProfitMarketDataMode");
        }
    }

    private static TaskCompletionSource NewTcs() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
