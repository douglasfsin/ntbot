using NtBot.Connector.Windows.MarketData;
using NtBot.Connector.Windows.Services;

namespace NtBot.Connector.Windows.UI;

public sealed class DiagnosticsForm : Form
{
    private readonly IProviderMonitor _monitor;
    private readonly IPlatformStatusRegistry _registry;
    private readonly IMarketDataCache _cache;
    private readonly Label _summaryLabel;
    private readonly ListView _platformsView;
    private readonly ListView _providersView;
    private readonly ListView _cacheView;
    private readonly System.Windows.Forms.Timer _timer;

    public DiagnosticsForm(
        IProviderMonitor monitor,
        IPlatformStatusRegistry registry,
        IMarketDataCache cache)
    {
        _monitor = monitor;
        _registry = registry;
        _cache = cache;

        Text = "Market Data Diagnostics";
        Width = 1100;
        Height = 580;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(720, 420);

        _summaryLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 56,
            Padding = new Padding(8),
            Font = new Font(Font.FontFamily, 9f)
        };

        var tabs = new TabControl { Dock = DockStyle.Fill };

        _platformsView = CreateListView(
            ("Plataforma", 140),
            ("Habilitada", 70),
            ("Conexão", 90),
            ("Status", 90),
            ("Detalhe", 280),
            ("Atualizado", 80));

        _providersView = CreateListView(
            ("Provider", 80),
            ("Saúde", 80),
            ("TPS", 55),
            ("Símbolos", 65),
            ("Último tick", 80),
            ("Idade (s)", 65),
            ("Reconexões", 75),
            ("Falhas", 55),
            ("Detalhe", 200));

        _cacheView = CreateListView(
            ("Casa", 70),
            ("Símbolo", 80),
            ("DDE/RTD", 80),
            ("Último", 85),
            ("Compra", 75),
            ("Venda", 75),
            ("Abert.", 75),
            ("Máx", 70),
            ("Min", 70),
            ("Fech.", 70),
            ("Volume", 75),
            ("Atualizado", 75),
            ("Idade (s)", 60));

        tabs.TabPages.Add(WrapTab("Plataformas (casas)", _platformsView));
        tabs.TabPages.Add(WrapTab("Market Data", _providersView));
        tabs.TabPages.Add(WrapTab("Cache de cotações", _cacheView));

        Controls.Add(tabs);
        Controls.Add(_summaryLabel);

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += (_, _) => RefreshView();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        RefreshView();
        _timer.Start();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _timer.Stop();
        base.OnFormClosing(e);
    }

    private static TabPage WrapTab(string title, Control content)
    {
        var page = new TabPage(title) { Padding = new Padding(4) };
        content.Dock = DockStyle.Fill;
        page.Controls.Add(content);
        return page;
    }

    private static ListView CreateListView(params (string Header, int Width)[] columns)
    {
        var view = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable
        };

        foreach (var (header, width) in columns)
            view.Columns.Add(header, width);

        return view;
    }

    private void RefreshView()
    {
        if (IsDisposed)
            return;

        var diag = _monitor.GetDiagnostics();
        var now = DateTime.UtcNow;

        _summaryLabel.Text =
            $"Bus: cache {diag.CacheSymbolCount} símbolos | fila {diag.ChannelQueueDepth} | " +
            $"publicados {diag.PublishedTotal} | descartados {diag.DroppedTotal}\r\n" +
            $"Throughput: {diag.TicksPerSecond:N1} tps | latência média {diag.AverageLatencyMs:N1} ms | máx {diag.MaxLatencyMs:N1} ms";

        RefreshPlatforms();
        RefreshProviders(diag, now);
        RefreshCache(now);
    }

    private void RefreshPlatforms()
    {
        _platformsView.BeginUpdate();
        _platformsView.Items.Clear();

        var api = _registry.GetApiStatus();
        if (api != null)
            _platformsView.Items.Add(CreatePlatformItem(api));

        foreach (var entry in _registry.Snapshot)
            _platformsView.Items.Add(CreatePlatformItem(entry));

        _platformsView.EndUpdate();
    }

    private static ListViewItem CreatePlatformItem(PlatformStatusEntry entry)
    {
        var item = new ListViewItem(entry.Name);
        item.SubItems.Add(entry.IsEnabled ? "Sim" : "Não");
        item.SubItems.Add(entry.IsConnected ? "Conectado" : "Offline");
        item.SubItems.Add(entry.Status);
        item.SubItems.Add(Truncate(entry.Message ?? "—", 120));
        item.SubItems.Add(entry.UpdatedUtc.ToLocalTime().ToString("HH:mm:ss"));
        item.ForeColor = !entry.IsEnabled
            ? Color.Gray
            : entry.IsConnected
                ? Color.DarkGreen
                : Color.DarkOrange;
        return item;
    }

    private void RefreshProviders(MarketDataDiagnosticsSnapshot diag, DateTime nowUtc)
    {
        _providersView.BeginUpdate();
        _providersView.Items.Clear();

        foreach (var p in diag.Providers.OrderBy(x => x.Provider.ToString()))
        {
            var ageSeconds = p.TimeSinceLastTick?.TotalSeconds;
            var item = new ListViewItem(p.Provider.ToString());
            item.SubItems.Add(FormatHealthLevel(p.Level));
            item.SubItems.Add(p.TicksPerSecond.ToString("N1"));
            item.SubItems.Add(p.ActiveSymbols.ToString());
            item.SubItems.Add(p.LastTickUtc?.ToLocalTime().ToString("HH:mm:ss") ?? "—");
            item.SubItems.Add(ageSeconds.HasValue ? ageSeconds.Value.ToString("N0") : "—");
            item.SubItems.Add(p.ReconnectCount.ToString());
            item.SubItems.Add(p.FailureCount.ToString());
            item.SubItems.Add(Truncate(p.Message ?? "OK", 80));
            item.ForeColor = HealthColor(p.Level);
            _providersView.Items.Add(item);
        }

        _providersView.EndUpdate();
    }

    private void RefreshCache(DateTime nowUtc)
    {
        _cacheView.BeginUpdate();
        _cacheView.Items.Clear();

        foreach (var tick in _cache.Snapshot()
                     .OrderBy(t => t.Provider.ToString())
                     .ThenBy(t => t.Symbol, StringComparer.OrdinalIgnoreCase))
        {
            var ageSeconds = (nowUtc - tick.TimestampUtc).TotalSeconds;
            var item = new ListViewItem(tick.Provider.ToString());
            item.SubItems.Add(tick.Symbol);
            item.SubItems.Add(tick.Source);
            item.SubItems.Add(FormatPrice(tick.LastPrice));
            item.SubItems.Add(FormatPrice(tick.Bid));
            item.SubItems.Add(FormatPrice(tick.Ask));
            item.SubItems.Add(FormatPrice(tick.Open));
            item.SubItems.Add(FormatPrice(tick.High));
            item.SubItems.Add(FormatPrice(tick.Low));
            item.SubItems.Add(FormatPrice(tick.Close));
            item.SubItems.Add(tick.Volume?.ToString("N0") ?? "—");
            item.SubItems.Add(tick.TimestampUtc.ToLocalTime().ToString("HH:mm:ss"));
            item.SubItems.Add(ageSeconds.ToString("N0"));
            item.ForeColor = ageSeconds > 30 ? Color.DarkOrange : Color.DarkGreen;
            _cacheView.Items.Add(item);
        }

        if (_cacheView.Items.Count == 0)
        {
            var empty = new ListViewItem("—");
            empty.SubItems.Add("nenhuma cotação em cache");
            empty.ForeColor = Color.Gray;
            _cacheView.Items.Add(empty);
        }

        _cacheView.EndUpdate();
    }

    private static string FormatHealthLevel(ProviderHealthLevel level) => level switch
    {
        ProviderHealthLevel.Healthy => "OK",
        ProviderHealthLevel.Warning => "Atenção",
        ProviderHealthLevel.Stale => "Stale",
        ProviderHealthLevel.Disconnected => "Sem dados",
        _ => level.ToString()
    };

    private static Color HealthColor(ProviderHealthLevel level) => level switch
    {
        ProviderHealthLevel.Healthy => Color.DarkGreen,
        ProviderHealthLevel.Warning => Color.DarkOrange,
        ProviderHealthLevel.Stale => Color.IndianRed,
        _ => Color.Gray
    };

    private static string FormatPrice(decimal? value)
    {
        if (!value.HasValue)
            return "—";

        return Math.Abs(value.Value) >= 100
            ? value.Value.ToString("N0")
            : value.Value.ToString("N2");
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..(max - 1)] + "…";
}
