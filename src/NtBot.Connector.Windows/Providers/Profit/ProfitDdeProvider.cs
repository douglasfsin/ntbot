using System.Globalization;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Extensions.Options;
using NtBot.Connector.Windows.Configuration;
using NtBot.Connector.Windows.Core;
using NtBot.Connector.Windows.MarketData;
using NtBot.Shared.Normalized;

namespace NtBot.Connector.Windows.Providers.Profit;

/// <summary>
/// Provider DDE nativo do ProfitChart — publica ticks no MarketDataBus.
/// Protocolo Profit: profitchart | COT ! ATIVO.ULT
/// </summary>
public sealed class ProfitDdeProvider : BackgroundService, IBrokerPlugin, IDdeReplayController
{
    private static readonly string[] ServerCandidates = ["profitchart", "PROFITCHART"];
    private static readonly string[] TopicCandidates = ["COT", "cot", "Cot"];
    private static readonly string[] PriceFields = ["ULT", "QC", "QV", "PRT", "FEC", "MAX", "MIN", "VOL", "QTT", "ABE"];
    private static readonly string[] DdeQuoteFields = ["ULT", "QC", "QV", "MAX", "MIN", "FEC", "VOL", "QTT", "ABE", "PRT"];

    private string _mainDdeTopic = "COT";
    private readonly Dictionary<string, DateTime> _lastWarnUtcBySymbol = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _mirrorTargets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, decimal> _lastPublishedPrice = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _lastReplayFeedSymbol = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _ddeConfigPath;
    private readonly string _rtdConfigPath;
    private DateTime _ddeConfigMtime = DateTime.MinValue;
    private DateTime _rtdConfigMtime = DateTime.MinValue;
    private volatile bool _resubscribeRequested;

    private readonly ConnectorOptions _options;
    private readonly IProfitMarketDataModeController _mode;
    private readonly IMarketDataPublisher _publisher;
    private readonly IMarketDataCache _cache;
    private readonly ProfitMarketDataCoordinator _coordinator;
    private readonly ILogger<ProfitDdeProvider> _logger;
    private ProfitDdeConfig _config;
    private readonly Dictionary<string, ProfitRtdConfigEntry> _rtdEntries = new(StringComparer.OrdinalIgnoreCase);

    private Thread? _ddeThread;
    private CancellationTokenSource? _ddeLoopCts;
    private bool _sessionConnected;
    private bool _hasSubscriptions;
    private DateTime _lastAssetTickUtc = DateTime.MinValue;
    private string _statusMessage = string.Empty;

    public ProfitDdeProvider(
        IOptions<ConnectorOptions> options,
        IProfitMarketDataModeController mode,
        IMarketDataPublisher publisher,
        IMarketDataCache cache,
        ProfitMarketDataCoordinator coordinator,
        ILogger<ProfitDdeProvider> logger)
    {
        _options = options.Value;
        _mode = mode;
        _publisher = publisher;
        _cache = cache;
        _coordinator = coordinator;
        _logger = logger;
        _config = ProfitDdeConfigLoader.Load(_options.ProfitDdeConfigPath);
        _ddeConfigPath = Path.Combine(AppContext.BaseDirectory, _options.ProfitDdeConfigPath);
        _rtdConfigPath = Path.Combine(AppContext.BaseDirectory, _options.ProfitRtdConfigPath);
        ReloadConfigs(force: true);
    }

    private void ReloadConfigs(bool force = false)
    {
        try
        {
            if (File.Exists(_ddeConfigPath))
            {
                var mtime = File.GetLastWriteTimeUtc(_ddeConfigPath);
                if (force || mtime > _ddeConfigMtime)
                {
                    _config = ProfitDdeConfigLoader.Load(_options.ProfitDdeConfigPath);
                    _ddeConfigMtime = mtime;
                }
            }

            if (File.Exists(_rtdConfigPath))
            {
                var mtime = File.GetLastWriteTimeUtc(_rtdConfigPath);
                if (force || mtime > _rtdConfigMtime)
                {
                    _rtdEntries.Clear();
                    var json = File.ReadAllText(_rtdConfigPath);
                    var map = JsonSerializer.Deserialize<Dictionary<string, ProfitRtdConfigEntry>>(json);
                    if (map != null)
                    {
                        foreach (var (key, entry) in map)
                            _rtdEntries[key] = entry;
                    }

                    _rtdConfigMtime = mtime;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Falha ao recarregar configs Profit DDE/RTD");
        }
    }

    public BrokerSource Source => BrokerSource.Profit;
    public string Name => "ProfitChart DDE";
    public bool IsConnected => _sessionConnected;

    public event Action<NormalizedMarketTick>? OnTick;
    public event Action<NormalizedBrokerStatus>? OnStatusChanged;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_mode.Current != ProfitMarketDataMode.Dde)
            {
                DisconnectAll();
                SetStatus(false, "disabled", $"Modo ativo: {_mode.Current.ToDisplayName()} — DDE em espera");
                await _mode.WaitForModeAsync(ProfitMarketDataMode.Dde, stoppingToken);
                continue;
            }

            SetStatus(false, "connecting", "Conectando DDE…");

            _coordinator.ReplayModeActive = _config.ReplayMode;
            _coordinator.AllowRtdFallback = _options.AllowRtdFallbackWhenDdeStale && !_config.ReplayMode;
            if (_config.ReplayMode)
                _coordinator.DdeFallbackThreshold = TimeSpan.FromHours(24);

            _logger.LogInformation(
                "Profit DDE provider iniciando ({Server}|{Topic}) ReplayMode={Replay}",
                _config.DdeServer,
                _config.DdeTopic,
                _config.ReplayMode);

            try
            {
                if (_ddeThread?.IsAlive != true)
                    await StartDdeThreadAsync(stoppingToken);

                while (!stoppingToken.IsCancellationRequested
                       && _ddeThread?.IsAlive == true
                       && _mode.Current == ProfitMarketDataMode.Dde)
                {
                    await Task.Delay(1000, stoppingToken);
                }

                if (_mode.Current != ProfitMarketDataMode.Dde)
                {
                    DisconnectAll();
                    continue;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                SetStatus(false, "error", ex.Message);
                _logger.LogWarning(ex, "Profit DDE erro — retry em 5s");
                DisconnectAll();
                await Task.Delay(5000, stoppingToken);
            }
        }

        DisconnectAll();
    }

    public Task ConnectAsync(CancellationToken ct)
    {
        if (!_options.EnableProfitDde)
            return Task.CompletedTask;

        if (_ddeThread?.IsAlive == true)
            return Task.CompletedTask;

        return StartDdeThreadAsync(ct);
    }

    public Task DisconnectAsync(CancellationToken ct)
    {
        DisconnectAll();
        return Task.CompletedTask;
    }

    public Task<NormalizedAccount?> GetAccountAsync(CancellationToken ct) =>
        Task.FromResult<NormalizedAccount?>(null);

    public Task<IReadOnlyList<NormalizedOrder>> GetOpenOrdersAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NormalizedOrder>>([]);

    public Task<IReadOnlyList<NormalizedOrder>> GetOrdersAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NormalizedOrder>>([]);

    public Task<IReadOnlyList<NormalizedExecution>> GetRecentExecutionsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NormalizedExecution>>([]);

    public Task<IReadOnlyList<NormalizedPosition>> GetPositionsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NormalizedPosition>>([]);

    private async Task StartDdeThreadAsync(CancellationToken ct)
    {
        if (_ddeThread?.IsAlive == true)
            return;

        _ddeLoopCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _ddeThread = new Thread(() => DdeThreadMain(_ddeLoopCts.Token, ready))
        {
            IsBackground = true,
            Name = "ProfitDDE"
        };
        _ddeThread.SetApartmentState(ApartmentState.STA);
        _ddeThread.Start();

        await ready.Task.WaitAsync(ct);
    }

    private sealed class DdeAssetSubscription
    {
        public required ProfitDdeAssetConfig Asset { get; init; }
        public required string DdeSymbol { get; init; }
        public required string UltItem { get; init; }
        public required string Topic { get; init; }
        public bool Advise { get; init; }
        public bool BlindAdvise { get; init; }
    }

    private void DdeThreadMain(CancellationToken ct, TaskCompletionSource<bool> ready)
    {
        ProfitDdeSession? session = null;

        try
        {
            var assets = ResolveConfiguredAssets();
            BuildMirrorMap(assets);
            if (assets.Count == 0)
            {
                SetStatus(false, "waiting", "Nenhum ativo ativo em profit_dde_config / rtd_config");
                ready.TrySetResult(true);
                return;
            }

            session = new ProfitDdeSession();
            var subscriptions = SubscribeAllAssets(session, assets);
            _sessionConnected = session.IsConnected;
            _hasSubscriptions = subscriptions.Count > 0 || _mirrorTargets.Count > 0;
            UpdateConnectionStatus(subscriptions);

            ready.TrySetResult(true);

            var lastPollUtc = DateTime.UtcNow;
            var lastRecoveryUtc = DateTime.UtcNow;
            var lastSessionResetUtc = DateTime.UtcNow;
            var lastConfigReloadUtc = DateTime.UtcNow;
            while (!ct.IsCancellationRequested)
            {
                Application.DoEvents();

                if (_resubscribeRequested)
                {
                    _resubscribeRequested = false;
                    _logger.LogInformation("DDE resubscribe solicitado (ReplayMode={Replay})", _config.ReplayMode);
                    ReloadConfigs(force: true);
                    assets = ResolveConfiguredAssets();
                    BuildMirrorMap(assets);
                    session.Dispose();
                    session = new ProfitDdeSession();
                    subscriptions = SubscribeAllAssets(session, assets);
                    _sessionConnected = session.IsConnected;
                    _hasSubscriptions = subscriptions.Count > 0 || _mirrorTargets.Count > 0;
                    _lastAssetTickUtc = DateTime.MinValue;
                    UpdateConnectionStatus(subscriptions);
                    lastRecoveryUtc = DateTime.UtcNow;
                    lastSessionResetUtc = DateTime.UtcNow;
                    lastConfigReloadUtc = DateTime.UtcNow;
                    continue;
                }

                if (DateTime.UtcNow - lastConfigReloadUtc >= TimeSpan.FromSeconds(15))
                {
                    ReloadConfigs();
                    var latestAssets = ResolveConfiguredAssets();
                    BuildMirrorMap(latestAssets);

                    var symbolChanged = latestAssets
                        .Where(a => !a.IsMirrorOnly)
                        .Any(asset =>
                        {
                            var existing = subscriptions.FirstOrDefault(s =>
                                s.Asset.LogicalSymbol.Equals(asset.LogicalSymbol, StringComparison.OrdinalIgnoreCase));
                            return existing is not null
                                && !existing.DdeSymbol.Equals(asset.ResolveSymbol(), StringComparison.OrdinalIgnoreCase);
                        });

                    if (symbolChanged)
                    {
                        _resubscribeRequested = true;
                        continue;
                    }

                    foreach (var asset in latestAssets.Where(a => !a.IsMirrorOnly))
                    {
                        if (subscriptions.Any(s =>
                                s.Asset.LogicalSymbol.Equals(asset.LogicalSymbol, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        if (TrySubscribeAssetAllModes(session, asset, out var subscription))
                        {
                            subscriptions.Add(subscription);
                            _logger.LogInformation(
                                "DDE novo ativo {Symbol} via {Topic}!{Item}",
                                asset.LogicalSymbol,
                                subscription.Topic,
                                subscription.UltItem);
                        }
                    }

                    assets = latestAssets;
                    lastConfigReloadUtc = DateTime.UtcNow;
                    _hasSubscriptions = subscriptions.Count > 0 || _mirrorTargets.Count > 0;
                    UpdateConnectionStatus(subscriptions);
                }

                var needsRecovery = subscriptions.Count == 0
                    && DateTime.UtcNow - lastRecoveryUtc >= TimeSpan.FromSeconds(30);

                if (needsRecovery)
                {
                    subscriptions = RecoverSubscriptions(session, assets, subscriptions);
                    _hasSubscriptions = subscriptions.Count > 0 || _mirrorTargets.Count > 0;
                    UpdateConnectionStatus(subscriptions);
                    lastRecoveryUtc = DateTime.UtcNow;
                }
                else if (_lastAssetTickUtc != DateTime.MinValue
                    && DateTime.UtcNow - _lastAssetTickUtc > TimeSpan.FromSeconds(60)
                    && DateTime.UtcNow - lastRecoveryUtc >= TimeSpan.FromSeconds(30))
                {
                    subscriptions = RecoverSubscriptions(session, assets, subscriptions);
                    _hasSubscriptions = subscriptions.Count > 0 || _mirrorTargets.Count > 0;
                    UpdateConnectionStatus(subscriptions);
                    lastRecoveryUtc = DateTime.UtcNow;
                }

                if (_lastAssetTickUtc == DateTime.MinValue
                    && DateTime.UtcNow - lastSessionResetUtc > TimeSpan.FromMinutes(2))
                {
                    _logger.LogInformation("DDE sem ticks — reconectando sessão {Server}|{Topic}", _config.DdeServer, _config.DdeTopic);
                    session.Dispose();
                    session = new ProfitDdeSession();
                    subscriptions = SubscribeAllAssets(session, assets);
                    _sessionConnected = session.IsConnected;
                    _hasSubscriptions = subscriptions.Count > 0 || _mirrorTargets.Count > 0;
                    UpdateConnectionStatus(subscriptions);
                    lastSessionResetUtc = DateTime.UtcNow;
                    lastRecoveryUtc = DateTime.UtcNow;
                }

                if (subscriptions.Count > 0
                    && DateTime.UtcNow - lastPollUtc >= TimeSpan.FromMilliseconds(GetPollIntervalMs()))
                {
                    foreach (var sub in subscriptions)
                    {
                        try
                        {
                            PublishFromSubscription(session, sub);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "DDE poll falhou {Topic}!{Symbol}", sub.Topic, sub.DdeSymbol);
                        }
                    }

                    lastPollUtc = DateTime.UtcNow;
                }

                Thread.Sleep(10);
            }
        }
        catch (Exception ex)
        {
            ready.TrySetException(ex);
            SetStatus(false, "error", ex.Message);
            _logger.LogError(ex, "Thread DDE encerrada com erro");
        }
        finally
        {
            session?.Dispose();
            _sessionConnected = false;
            _hasSubscriptions = false;
        }
    }

    private List<ProfitDdeAssetConfig> ResolveConfiguredAssets()
    {
        var assets = ProfitMarketAssetResolver.ResolveAll(_config, _rtdEntries);
        return ApplyReplayMode(assets);
    }

    private List<ProfitDdeAssetConfig> ApplyReplayMode(List<ProfitDdeAssetConfig> assets)
    {
        if (!_config.ReplayMode)
        {
            _coordinator.ReplayModeActive = false;
            _coordinator.DdeFallbackThreshold = TimeSpan.FromSeconds(5);
            return assets;
        }

        // Replay no Profit costuma rodar no contínuo (WDOFUT/WINFUT).
        // Não remapeamos para o mês (WDOQ26) — isso fazia o DDE ler cotação live/stale errada.
        // RTD live fica bloqueado via ReplayModeActive.
        _coordinator.ReplayModeActive = true;
        _coordinator.DdeFallbackThreshold = TimeSpan.FromHours(24);

        var contracts = _config.ReplayContracts ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in assets)
        {
            if (asset.IsMirrorOnly)
                continue;

            if (!IsHighPrioritySymbol(asset.LogicalSymbol))
                continue;

            var continuous = ResolveContinuousDdeSymbol(asset);
            asset.DdeSymbol = continuous;

            var aliases = asset.DdeAliases?.ToList() ?? [];
            if (contracts.TryGetValue(asset.LogicalSymbol, out var contract)
                && !string.IsNullOrWhiteSpace(contract)
                && !contract.Equals(continuous, StringComparison.OrdinalIgnoreCase)
                && !aliases.Contains(contract, StringComparer.OrdinalIgnoreCase))
            {
                aliases.Insert(0, contract.Trim().ToUpperInvariant());
            }

            foreach (var alias in new[] { asset.LogicalSymbol, $"{asset.LogicalSymbol}FUT" })
            {
                if (string.IsNullOrWhiteSpace(alias))
                    continue;
                if (alias.Equals(continuous, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!aliases.Contains(alias, StringComparer.OrdinalIgnoreCase))
                    aliases.Add(alias);
            }

            asset.DdeAliases = aliases;
        }

        return assets;
    }

    private static string ResolveContinuousDdeSymbol(ProfitDdeAssetConfig asset)
    {
        var current = asset.ResolveSymbol();
        if (current.EndsWith("FUT", StringComparison.OrdinalIgnoreCase))
            return current.ToUpperInvariant();

        return asset.LogicalSymbol.ToUpperInvariant() switch
        {
            "WIN" => "WINFUT",
            "WDO" => "WDOFUT",
            _ => string.IsNullOrWhiteSpace(current) ? asset.LogicalSymbol.ToUpperInvariant() : current.ToUpperInvariant()
        };
    }

    public DdeReplayStatus GetStatus()
    {
        ReloadConfigs();
        var assets = ResolveConfiguredAssets();
        return new DdeReplayStatus
        {
            Enabled = _config.ReplayMode,
            Contracts = new Dictionary<string, string>(_config.ReplayContracts ?? [], StringComparer.OrdinalIgnoreCase),
            Assets = assets.Select(a => new DdeReplayAssetRow
            {
                LogicalSymbol = a.LogicalSymbol,
                DdeSymbol = a.ResolveSymbol(),
                MirrorFromSymbol = a.MirrorFromSymbol,
                IsMirrorOnly = a.IsMirrorOnly,
                IsActive = a.IsActive
            }).ToList(),
            IsConnected = _sessionConnected,
            Message = _statusMessage
        };
    }

    public DdeReplayStatus SetReplayMode(bool enabled, IReadOnlyDictionary<string, string>? contracts = null)
    {
        ReloadConfigs(force: true);

        _config.ReplayMode = enabled;
        _config.ReplayContracts ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Defaults: contínuo (onde o replay do Profit costuma rodar).
        if (!_config.ReplayContracts.ContainsKey("WIN"))
            _config.ReplayContracts["WIN"] = "WINFUT";
        if (!_config.ReplayContracts.ContainsKey("WDO"))
            _config.ReplayContracts["WDO"] = "WDOFUT";

        if (contracts is { Count: > 0 })
        {
            foreach (var (key, value) in contracts)
            {
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                    continue;
                _config.ReplayContracts[key.Trim().ToUpperInvariant()] = value.Trim().ToUpperInvariant();
            }
        }
        else if (enabled)
        {
            // Migra contratos mensais legados (WDOQ26) → contínuo, se ainda estiverem no default antigo.
            NormalizeLegacyReplayContracts(_config.ReplayContracts);
        }

        _coordinator.ReplayModeActive = enabled;
        _coordinator.DdeFallbackThreshold = enabled ? TimeSpan.FromHours(24) : TimeSpan.FromSeconds(5);

        if (enabled)
        {
            _lastPublishedPrice.Clear();
            _lastReplayFeedSymbol.Clear();
            var cleared = _cache.RemoveWhere(t =>
                t.Provider == BrokerSource.Profit
                && (t.Symbol.Equals("WIN", StringComparison.OrdinalIgnoreCase)
                    || t.Symbol.Equals("WDO", StringComparison.OrdinalIgnoreCase)
                    || t.Symbol.StartsWith("WIN", StringComparison.OrdinalIgnoreCase)
                    || t.Symbol.StartsWith("WDO", StringComparison.OrdinalIgnoreCase)));
            if (cleared > 0)
                _logger.LogInformation("ReplayMode limpou {Count} ticks live/stale do cache", cleared);
        }

        try
        {
            ProfitDdeConfigLoader.Save(_options.ProfitDdeConfigPath, _config);
            _ddeConfigMtime = File.Exists(_ddeConfigPath)
                ? File.GetLastWriteTimeUtc(_ddeConfigPath)
                : DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao persistir ReplayMode no profit_dde_config.json");
        }

        RequestResubscribe();
        _logger.LogInformation(
            "DDE ReplayMode={Enabled} (RTD live={Rtd}) contracts=[{Contracts}]",
            enabled,
            enabled ? "bloqueado" : "fallback",
            string.Join(", ", (_config.ReplayContracts ?? []).Select(kv => $"{kv.Key}→{kv.Value}")));

        return GetStatus();
    }

    private static void NormalizeLegacyReplayContracts(Dictionary<string, string> contracts)
    {
        foreach (var logical in new[] { "WIN", "WDO" })
        {
            if (!contracts.TryGetValue(logical, out var contract) || string.IsNullOrWhiteSpace(contract))
            {
                contracts[logical] = logical == "WIN" ? "WINFUT" : "WDOFUT";
                continue;
            }

            // WINQ26 / WDOQ26 / WINV25 etc. → contínuo
            var upper = contract.Trim().ToUpperInvariant();
            var isMonthly = upper.Length >= 5
                && !upper.EndsWith("FUT", StringComparison.Ordinal)
                && (upper.StartsWith("WIN", StringComparison.Ordinal) || upper.StartsWith("WDO", StringComparison.Ordinal))
                && char.IsLetter(upper[3])
                && char.IsDigit(upper[^1]);

            if (isMonthly)
                contracts[logical] = logical == "WIN" ? "WINFUT" : "WDOFUT";
        }
    }

    public void RequestResubscribe() => _resubscribeRequested = true;

    private void BuildMirrorMap(List<ProfitDdeAssetConfig> assets)
    {
        _mirrorTargets.Clear();
        foreach (var asset in assets)
        {
            if (!asset.IsMirrorOnly)
                continue;

            var source = asset.MirrorFromSymbol!.Trim();
            if (!_mirrorTargets.TryGetValue(source, out var targets))
            {
                targets = [];
                _mirrorTargets[source] = targets;
            }

            if (!targets.Contains(asset.LogicalSymbol, StringComparer.OrdinalIgnoreCase))
                targets.Add(asset.LogicalSymbol);
        }

        if (_mirrorTargets.Count > 0)
        {
            var summary = string.Join(", ", _mirrorTargets.Select(kv => $"{kv.Key}→{string.Join("|", kv.Value)}"));
            _logger.LogInformation("DDE mirror ativo: {Summary}", summary);
        }
    }

    private List<DdeAssetSubscription> SubscribeAllAssets(ProfitDdeSession session, List<ProfitDdeAssetConfig> assets)
    {
        ConnectSession(session, _mainDdeTopic);

        var subscriptions = new List<DdeAssetSubscription>();
        var subscribeQueue = assets
            .Where(a => !a.IsMirrorOnly)
            .OrderBy(a => SubscriptionPriority(a.LogicalSymbol))
            .ThenBy(a => a.LogicalSymbol, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var asset in subscribeQueue)
        {
            if (TrySubscribeAssetAllModes(session, asset, out var subscription))
                subscriptions.Add(subscription);
            else
                _logger.LogWarning("Nenhum item DDE válido para {Symbol}", asset.LogicalSymbol);

            Thread.Sleep(IsHighPrioritySymbol(asset.LogicalSymbol) ? 50 : 100);
        }

        return subscriptions;
    }

    private static bool IsHighPrioritySymbol(string symbol) =>
        symbol.Equals("WIN", StringComparison.OrdinalIgnoreCase)
        || symbol.Equals("WDO", StringComparison.OrdinalIgnoreCase);

    private static int SubscriptionPriority(string symbol) => symbol.ToUpperInvariant() switch
    {
        "WIN" => 0,
        "WDO" => 1,
        var s when s.StartsWith("WIN", StringComparison.Ordinal) => 2,
        var s when s.StartsWith("WDO", StringComparison.Ordinal) => 3,
        var s when s.StartsWith("DI", StringComparison.Ordinal) => 5,
        _ => 10
    };

    private bool TrySubscribeAssetAllModes(
        ProfitDdeSession session,
        ProfitDdeAssetConfig asset,
        out DdeAssetSubscription subscription)
    {
        // Profit expõe cotações apenas no tópico COT — trocar para PETR4|WINQ26 etc.
        // desconecta a conversa e remove todos os advises já registrados.
        foreach (var ddeSymbol in asset.EnumerateDdeSymbols())
        {
            if (TrySubscribeAsset(session, _mainDdeTopic, asset, ddeSymbol, out subscription))
                return true;
        }

        subscription = default!;
        return false;
    }

    private List<DdeAssetSubscription> RecoverSubscriptions(
        ProfitDdeSession session,
        List<ProfitDdeAssetConfig> assets,
        List<DdeAssetSubscription> current)
    {
        var missing = assets
            .Where(a => !current.Any(s => s.Asset.LogicalSymbol.Equals(a.LogicalSymbol, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (missing.Count == 0 && _hasSubscriptions)
            return current;

        _logger.LogDebug("DDE recovery — re-subscribe {Count} ativo(s)", missing.Count == 0 ? assets.Count : missing.Count);

        if (!session.IsConnected)
            ConnectSession(session, _mainDdeTopic);

        foreach (var asset in (missing.Count > 0 ? missing : assets)
                     .Where(a => !a.IsMirrorOnly)
                     .OrderBy(a => SubscriptionPriority(a.LogicalSymbol))
                     .ThenBy(a => a.LogicalSymbol, StringComparer.OrdinalIgnoreCase))
        {
            if (current.Any(s => s.Asset.LogicalSymbol.Equals(asset.LogicalSymbol, StringComparison.OrdinalIgnoreCase)))
                continue;

            if (TrySubscribeAssetAllModes(session, asset, out var subscription))
            {
                current.Add(subscription);
                _logger.LogInformation(
                    "DDE recovery {Symbol} via {Topic}!{Item} ({Mode})",
                    asset.LogicalSymbol,
                    subscription.Topic,
                    subscription.UltItem,
                    subscription.Advise ? "advise" : "poll");
            }
        }

        return current;
    }

    private void ConnectSession(ProfitDdeSession session, string preferredTopic)
    {
        var servers = new[] { _config.DdeServer }
            .Concat(ServerCandidates)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var topics = new[] { preferredTopic, _config.DdeTopic }
            .Concat(TopicCandidates)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        Exception? lastError = null;
        foreach (var server in servers)
        {
            foreach (var topic in topics)
            {
                try
                {
                    session.Connect(server, topic);
                    _config.DdeServer = server;
                    if (IsCotTopic(topic))
                    {
                        _config.DdeTopic = topic;
                        _mainDdeTopic = topic;
                    }
                    _logger.LogInformation("DDE conectado {Server}|{Topic}", server, topic);
                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    _logger.LogDebug(ex, "DDE connect falhou para {Server}|{Topic}", server, topic);
                }
            }
        }

        throw lastError ?? new InvalidOperationException("Não foi possível conectar ao DDE do Profit");
    }

    private bool TrySubscribeAssetOnSymbolTopic(
        ProfitDdeSession session,
        ProfitDdeAssetConfig asset,
        string ddeSymbol,
        out DdeAssetSubscription subscription)
    {
        subscription = default!;
        var topics = new List<string> { ddeSymbol, asset.LogicalSymbol }
            .Where(t => !string.IsNullOrWhiteSpace(t) && !IsCotTopic(t))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var symbolTopic in topics)
        {
            try
            {
                session.Connect(_config.DdeServer, symbolTopic);
                _logger.LogInformation(
                    "DDE conectado {Server}|{Topic} para {Symbol} (dde={Dde})",
                    _config.DdeServer, symbolTopic, asset.LogicalSymbol, ddeSymbol);

                if (TrySubscribeAsset(session, symbolTopic, asset, ddeSymbol, out subscription))
                    return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "DDE topic {Topic} indisponível para {Symbol} (dde={Dde})",
                    symbolTopic,
                    asset.LogicalSymbol,
                    ddeSymbol);
            }
        }

        try { ConnectSession(session, _mainDdeTopic); }
        catch { /* best effort */ }

        return false;
    }

    private bool TrySubscribeAsset(
        ProfitDdeSession session,
        string topic,
        ProfitDdeAssetConfig asset,
        string ddeSymbol,
        out DdeAssetSubscription subscription)
    {
        var attempts = new List<string>();
        foreach (var candidate in GetItemCandidates(asset, topic, ddeSymbol))
        {
            var adviseSub = CreateSubscription(asset, ddeSymbol, candidate, topic, true, false);
            if (session.TryStartAdvise(
                    candidate,
                    (_, _) => PublishFromSubscription(session, adviseSub),
                    out var adviseError))
            {
                var probe = session.Request(candidate);
                attempts.Add($"{candidate}=advise,probe={probe ?? "null"}");
                subscription = CreateSubscription(asset, ddeSymbol, candidate, topic, true, probe == null);
                _logger.LogInformation(
                    "DDE advise {Server}|{Topic}!{Item} probe={Probe} logical={Logical} dde={Dde}",
                    _config.DdeServer, topic, candidate, probe ?? "—", asset.LogicalSymbol, ddeSymbol);
                PublishFromSubscription(session, subscription);
                return true;
            }

            attempts.Add($"{candidate}=advise:{adviseError ?? "fail"}");

            var probeOnly = session.Request(candidate);
            attempts.Add($"{candidate}=request:{probeOnly ?? "null"}");
            if (TryParsePrice(probeOnly, out _))
            {
                subscription = CreateSubscription(asset, ddeSymbol, candidate, topic, false, false);
                _logger.LogInformation(
                    "DDE poll {Server}|{Topic}!{Item} probe={Probe} logical={Logical} dde={Dde}",
                    _config.DdeServer, topic, candidate, probeOnly, asset.LogicalSymbol, ddeSymbol);
                PublishFromSubscription(session, subscription);
                return true;
            }
        }

        LogSubscribeFailure(asset.LogicalSymbol, attempts);

        subscription = CreateSubscription(asset, ddeSymbol, $"{ddeSymbol}.ULT", topic, false, false);
        return false;
    }

    private static DdeAssetSubscription CreateSubscription(
        ProfitDdeAssetConfig asset,
        string ddeSymbol,
        string ultItem,
        string topic,
        bool advise,
        bool blindAdvise) =>
        new()
        {
            Asset = asset,
            DdeSymbol = ddeSymbol,
            UltItem = ultItem,
            Topic = topic,
            Advise = advise,
            BlindAdvise = blindAdvise
        };

    private void LogSubscribeFailure(string logicalSymbol, List<string> attempts)
    {
        var now = DateTime.UtcNow;
        if (_lastWarnUtcBySymbol.TryGetValue(logicalSymbol, out var last)
            && now - last < TimeSpan.FromSeconds(60))
        {
            _logger.LogDebug(
                "DDE sem item para {Symbol} ({Count} tentativas)",
                logicalSymbol,
                attempts.Count);
            return;
        }

        _lastWarnUtcBySymbol[logicalSymbol] = now;
        var preview = string.Join("; ", attempts.Take(6));
        if (attempts.Count > 6)
            preview += $"; …+{attempts.Count - 6}";

        _logger.LogWarning(
            "Nenhum item DDE para {Symbol}. Tentativas: {Attempts}",
            logicalSymbol,
            preview);
    }

    private static bool IsCotTopic(string topic) =>
        topic.Equals("COT", StringComparison.OrdinalIgnoreCase)
        || topic.Equals("cot", StringComparison.OrdinalIgnoreCase);

    private IEnumerable<string> GetItemCandidates(ProfitDdeAssetConfig asset, string topic, string ddeSymbol)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fields = new[] { asset.PriceItem }
            .Concat(PriceFields)
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(f => f.Contains('.') ? f.Split('.')[^1] : f)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        if (!IsCotTopic(topic))
        {
            foreach (var field in fields)
            {
                if (seen.Add(field))
                    yield return field;

                var lower = field.ToLowerInvariant();
                if (seen.Add(lower))
                    yield return lower;

                foreach (var item in AddItem(seen, ddeSymbol, field))
                    yield return item;
            }

            yield break;
        }

        foreach (var field in fields)
        {
            foreach (var item in AddItem(seen, $"{ddeSymbol}.{field}", field))
                yield return item;
            foreach (var item in AddItem(seen, ddeSymbol, field))
                yield return item;
        }

        if (_rtdEntries.TryGetValue(asset.LogicalSymbol, out var rtd))
        {
            foreach (var field in fields)
            {
                foreach (var item in AddItem(seen, rtd.TICK, field))
                    yield return item;

                if (rtd.TICKERS != null)
                {
                    foreach (var ticker in rtd.TICKERS)
                    {
                        foreach (var item in AddItem(seen, ticker, field))
                            yield return item;
                    }
                }
            }
        }

        foreach (var alias in asset.EnumerateDdeSymbols())
        {
            foreach (var field in fields)
            {
                foreach (var item in AddItem(seen, alias, field))
                    yield return item;
            }
        }

        foreach (var item in seen.ToList())
        {
            var lower = item.ToLowerInvariant();
            if (seen.Add(lower))
                yield return lower;

            var bracketed = $"[{item}]";
            if (seen.Add(bracketed))
                yield return bracketed;
        }
    }

    private static IEnumerable<string> AddItem(HashSet<string> seen, string? symbol, string? field = null)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            yield break;

        var item = symbol.Contains('.')
            ? symbol
            : $"{symbol}.{(field ?? "ULT")}";

        if (seen.Add(item))
            yield return item;

        var lower = item.ToLowerInvariant();
        if (seen.Add(lower))
            yield return lower;
    }

    private int GetPollIntervalMs()
    {
        if (_config.ReplayMode)
            return Math.Min(_config.HeartbeatMs > 0 ? _config.HeartbeatMs : 200, 200);

        return _config.HeartbeatMs > 0 ? _config.HeartbeatMs : 1000;
    }

    private void PublishFromSubscription(ProfitDdeSession session, DdeAssetSubscription sub)
    {
        if (!session.CurrentTopic.Equals(sub.Topic, StringComparison.OrdinalIgnoreCase))
            session.Connect(_config.DdeServer, sub.Topic);

        if (_config.ReplayMode && IsHighPrioritySymbol(sub.Asset.LogicalSymbol))
        {
            PublishReplayPreferMoving(session, sub);
            return;
        }

        var fields = PollQuoteFields(session, sub);
        PublishMergedTick(sub.Asset, fields, sub.DdeSymbol);
    }

    /// <summary>
    /// No replay, tenta o contínuo e aliases e prefere o símbolo cujo ULT está se movendo
    /// (evita ficar preso na cotação live/stale de sexta da aba atual).
    /// </summary>
    private void PublishReplayPreferMoving(ProfitDdeSession session, DdeAssetSubscription sub)
    {
        var candidates = sub.Asset.EnumerateDdeSymbols().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (candidates.Count == 0)
            candidates.Add(sub.DdeSymbol);

        IReadOnlyDictionary<string, string?>? chosenFields = null;
        string? chosenSymbol = null;
        decimal? chosenLast = null;
        var logical = sub.Asset.LogicalSymbol;
        _lastPublishedPrice.TryGetValue(logical, out var previous);

        foreach (var ddeSymbol in candidates)
        {
            var probeSub = new DdeAssetSubscription
            {
                Asset = sub.Asset,
                DdeSymbol = ddeSymbol,
                UltItem = $"{ddeSymbol}.ULT",
                Topic = sub.Topic,
                Advise = sub.Advise,
                BlindAdvise = sub.BlindAdvise
            };

            var fields = PollQuoteFields(session, probeSub);
            var last = ParseField(fields, "ULT");
            if (last is null or <= 0)
                continue;

            chosenFields ??= fields;
            chosenSymbol ??= ddeSymbol;
            chosenLast ??= last;

            if (previous > 0 && last.Value != previous)
            {
                chosenFields = fields;
                chosenSymbol = ddeSymbol;
                chosenLast = last;
                break;
            }

            if (ddeSymbol.EndsWith("FUT", StringComparison.OrdinalIgnoreCase)
                && chosenSymbol is not null
                && !chosenSymbol.EndsWith("FUT", StringComparison.OrdinalIgnoreCase))
            {
                chosenFields = fields;
                chosenSymbol = ddeSymbol;
                chosenLast = last;
            }
        }

        if (chosenFields is null || chosenSymbol is null || chosenLast is null)
            return;

        if (_lastReplayFeedSymbol.TryGetValue(logical, out var prevFeed)
            && !prevFeed.Equals(chosenSymbol, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "Replay DDE feed {Logical}: {From} → {To} (ULT={Last})",
                logical,
                prevFeed,
                chosenSymbol,
                chosenLast);
        }

        _lastReplayFeedSymbol[logical] = chosenSymbol;
        PublishMergedTick(sub.Asset, chosenFields, chosenSymbol);
    }

    private Dictionary<string, string?> PollQuoteFields(ProfitDdeSession session, DdeAssetSubscription sub)
    {
        var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in DdeQuoteFields)
        {
            string? value = null;
            foreach (var item in GetFieldItemCandidates(sub, field))
            {
                value = session.Request(item);
                if (!string.IsNullOrWhiteSpace(value))
                    break;
            }

            fields[field] = value;
        }

        return fields;
    }

    private static IEnumerable<string> GetFieldItemCandidates(DdeAssetSubscription sub, string field)
    {
        var fieldLower = field.ToLowerInvariant();
        if (IsCotTopic(sub.Topic))
        {
            yield return $"{sub.DdeSymbol}.{fieldLower}";
            yield return $"{sub.DdeSymbol}.{field}";
            yield return $"[{sub.DdeSymbol}.{fieldLower}]";
            yield return $"[{sub.DdeSymbol}.{field}]";
            yield break;
        }

        yield return fieldLower;
        yield return field;
        yield return $"{sub.DdeSymbol}.{fieldLower}";
        yield return $"{sub.DdeSymbol}.{field}";
        yield return $"[{sub.DdeSymbol}.{fieldLower}]";
        yield return $"[{sub.DdeSymbol}.{field}]";
    }

    private void PublishMergedTick(
        ProfitDdeAssetConfig asset,
        IReadOnlyDictionary<string, string?> fields,
        string? ddeFeedSymbol = null)
    {
        var last = ParseField(fields, "ULT");
        if (last is null or <= 0)
            return;

        var feed = ddeFeedSymbol ?? asset.ResolveSymbol();
        var logical = asset.LogicalSymbol;
        var priceChanged = !_lastPublishedPrice.TryGetValue(logical, out var previous) || previous != last.Value;

        // Sempre marca atividade DDE (evita recovery falso quando o preço do replay está estável).
        _lastAssetTickUtc = DateTime.UtcNow;
        _coordinator.RecordDdeTick();

        // Em replay, só republica se o preço mudou — evita "congelar" a cotação de sexta a cada poll.
        if (_config.ReplayMode && !priceChanged)
        {
            if (IsHighPrioritySymbol(logical)
                && DateTime.UtcNow.Second % 10 == 0) // throttle
            {
                _logger.LogDebug(
                    "[ReplayMonitor] {Logical} ULT={Last} sem mudança (Source={Source}:{Feed}) — heartbeat ok",
                    logical,
                    last,
                    _config.ReplayMode ? "DDE-REPLAY" : "DDE",
                    feed);
            }

            return;
        }

        _lastPublishedPrice[logical] = last.Value;

        var sourcePrefix = _config.ReplayMode ? "DDE-REPLAY" : "DDE";
        var tick = MarketTickNormalizer.Normalize(new MarketTick
        {
            Provider = BrokerSource.Profit,
            Symbol = logical,
            LastPrice = last,
            Bid = ParseField(fields, "QC"),
            Ask = ParseField(fields, "QV"),
            Volume = ParseVolume(fields),
            Open = ParseField(fields, "ABE"),
            High = ParseField(fields, "MAX"),
            Low = ParseField(fields, "MIN"),
            Close = ParseField(fields, "FEC"),
            TimestampUtc = DateTime.UtcNow,
            Source = $"{sourcePrefix}:{feed}"
        });

        if (_config.ReplayMode && IsHighPrioritySymbol(logical))
        {
            _logger.LogInformation(
                "[ReplayMonitor] tick {Logical} ULT={Last} Bid={Bid} Ask={Ask} Source={Source} Vol={Volume}",
                logical,
                tick.LastPrice,
                tick.Bid,
                tick.Ask,
                tick.Source,
                tick.Volume);
        }
        else if (IsHighPrioritySymbol(logical))
        {
            _logger.LogDebug(
                "DDE tick {Logical} ULT={Last} Source={Source}",
                logical,
                tick.LastPrice,
                tick.Source);
        }

        _ = _publisher.PublishAsync(tick);
        OnTick?.Invoke(tick.ToNormalized());
        PublishMirrorTicks(tick, logical);
    }

    private void PublishMirrorTicks(MarketTick source, string sourceLogical)
    {
        if (!_mirrorTargets.TryGetValue(sourceLogical, out var mirrors))
            return;

        foreach (var mirrorSymbol in mirrors)
        {
            var mirrored = source with
            {
                Symbol = mirrorSymbol,
                Source = $"MIRROR:{sourceLogical}"
            };

            _ = _publisher.PublishAsync(mirrored);
            OnTick?.Invoke(mirrored.ToNormalized());
        }
    }

    private static decimal? ParseField(IReadOnlyDictionary<string, string?> fields, string key)
    {
        return fields.TryGetValue(key, out var raw) && TryParsePrice(raw, out var value) ? value : null;
    }

    private static long? ParseVolume(IReadOnlyDictionary<string, string?> fields)
    {
        foreach (var key in new[] { "VOL", "QTT" })
        {
            if (!fields.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
                continue;

            if (TryParseLong(raw, out var vol) && vol > 0)
                return vol;
        }

        return null;
    }

    private static bool TryParseLong(string? text, out long value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();
        if (long.TryParse(trimmed, NumberStyles.Any, CultureInfo.GetCultureInfo("pt-BR"), out value))
            return true;

        var normalized = trimmed.Replace(',', '.');
        if (long.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            return true;

        if (decimal.TryParse(trimmed, NumberStyles.Any, CultureInfo.GetCultureInfo("pt-BR"), out var dec) && dec > 0)
        {
            value = (long)dec;
            return true;
        }

        return false;
    }

    private static bool TryParsePrice(string? text, out decimal price)
    {
        price = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();
        if (decimal.TryParse(trimmed, NumberStyles.Any, CultureInfo.GetCultureInfo("pt-BR"), out price) && price > 0)
            return true;

        var normalized = trimmed.Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out price) && price > 0;
    }

    private void UpdateConnectionStatus(List<DdeAssetSubscription> subscriptions)
    {
        _hasSubscriptions = subscriptions.Count > 0 || _mirrorTargets.Count > 0;
        var adviseCount = subscriptions.Count(s => s.Advise);
        var blindCount = subscriptions.Count(s => s.BlindAdvise);
        var pollCount = subscriptions.Count - adviseCount;
        var mirrorCount = _mirrorTargets.Values.Sum(v => v.Count);
        var replayTag = _config.ReplayMode ? " | REPLAY" : string.Empty;
        var message = _hasSubscriptions || mirrorCount > 0
            ? $"{subscriptions.Count} ativos ({adviseCount} advise, {blindCount} blind, {pollCount} poll) | {mirrorCount} mirror {_config.DdeServer}|{_config.DdeTopic}{replayTag}"
            : $"DDE {_config.DdeServer}|{_config.DdeTopic} — aguardando cotações (fallback RTD ativo){replayTag}";
        SetStatus(
            _sessionConnected,
            _hasSubscriptions || mirrorCount > 0 ? "connected" : "waiting",
            message);
    }

    private void DisconnectAll()
    {
        _ddeLoopCts?.Cancel();
        var thread = _ddeThread;
        if (thread?.IsAlive == true)
            thread.Join(TimeSpan.FromSeconds(3));

        _ddeLoopCts?.Dispose();
        _ddeLoopCts = null;
        _ddeThread = null;
        _sessionConnected = false;
        _hasSubscriptions = false;
        SetStatus(false, "disconnected", "DDE desconectado");
    }

    private void SetStatus(bool connected, string status, string? message)
    {
        _statusMessage = message ?? string.Empty;
        OnStatusChanged?.Invoke(new NormalizedBrokerStatus
        {
            Source = BrokerSource.Profit,
            IsConnected = connected,
            Status = status,
            Message = message,
            TimestampUtc = DateTime.UtcNow
        });
    }

    public override void Dispose()
    {
        DisconnectAll();
        base.Dispose();
    }
}
