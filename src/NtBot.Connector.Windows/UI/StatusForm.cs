using Microsoft.Extensions.Options;
using NtBot.Connector.Windows.Configuration;
using NtBot.Connector.Windows.Core;
using NtBot.Connector.Windows.Services;
using NtBot.Connector.Windows.SignalR;

namespace NtBot.Connector.Windows.UI;

public sealed class StatusForm : Form
{
    private readonly IPlatformStatusRegistry _registry;
    private readonly INtBotApiClient _api;
    private readonly IEnumerable<IBrokerPlugin> _plugins;
    private readonly ConnectorOptions _options;
    private readonly ListView _listView;
    private readonly Label _apiLabel;
    private readonly Label _versionLabel;
    private readonly System.Windows.Forms.Timer _refreshTimer;

    public StatusForm(
        IPlatformStatusRegistry registry,
        INtBotApiClient api,
        IEnumerable<IBrokerPlugin> plugins,
        IOptions<ConnectorOptions> options)
    {
        _registry = registry;
        _api = api;
        _plugins = plugins;
        _options = options.Value;

        Text = $"NTBot Connector v{_options.Version}";
        Width = 520;
        Height = 360;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        ShowInTaskbar = true;

        _versionLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Padding = new Padding(8, 6, 8, 0),
            Text = $"API: {_options.ApiBaseUrl}"
        };

        _apiLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            Padding = new Padding(8, 0, 8, 0),
            ForeColor = Color.DarkGreen
        };

        _listView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable
        };
        _listView.Columns.Add("Plataforma", 130);
        _listView.Columns.Add("Status", 90);
        _listView.Columns.Add("Detalhe", 200);
        _listView.Columns.Add("Atualizado", 80);

        var panel = new Panel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(8) };
        var reconnectBtn = new Button { Text = "Reconectar tudo", Width = 120, Height = 28, Left = 8, Top = 8 };
        reconnectBtn.Click += (_, _) => _ = ReconnectAllAsync();
        var closeBtn = new Button { Text = "Fechar", Width = 80, Height = 28, Left = 136, Top = 8 };
        closeBtn.Click += (_, _) => Hide();
        panel.Controls.Add(reconnectBtn);
        panel.Controls.Add(closeBtn);

        Controls.Add(_listView);
        Controls.Add(panel);
        Controls.Add(_apiLabel);
        Controls.Add(_versionLabel);

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _refreshTimer.Tick += (_, _) => RefreshView();
        _registry.Changed += OnRegistryChanged;

        RefreshView();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _refreshTimer.Start();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _refreshTimer.Stop();
        _registry.Changed -= OnRegistryChanged;
        base.OnFormClosing(e);
    }

    private void OnRegistryChanged()
    {
        if (IsDisposed || !IsHandleCreated) return;

        try
        {
            BeginInvoke(RefreshView);
        }
        catch (ObjectDisposedException)
        {
            // form closing
        }
    }

    private void RefreshView()
    {
        if (IsDisposed) return;

        var api = _registry.GetApiStatus();
        var apiOnline = api?.IsConnected ?? _api.IsOnline;
        _apiLabel.Text = apiOnline
            ? "● NTBot API online"
            : "○ NTBot API offline";
        _apiLabel.ForeColor = apiOnline ? Color.DarkGreen : Color.DarkRed;

        _listView.BeginUpdate();
        _listView.Items.Clear();

        foreach (var entry in _registry.Snapshot)
        {
            var item = new ListViewItem(entry.Name);
            item.SubItems.Add(entry.IsEnabled
                ? (entry.IsConnected ? "Conectado" : entry.Status)
                : "Desabilitado");
            item.SubItems.Add(entry.Message ?? "—");
            item.SubItems.Add(entry.UpdatedUtc.ToLocalTime().ToString("HH:mm:ss"));
            item.ForeColor = entry.IsConnected ? Color.DarkGreen
                : entry.IsEnabled ? Color.DarkOrange : Color.Gray;
            _listView.Items.Add(item);
        }

        _listView.EndUpdate();
    }

    private async Task ReconnectAllAsync()
    {
        foreach (var plugin in _plugins)
        {
            try
            {
                await plugin.DisconnectAsync(CancellationToken.None);
                await plugin.ConnectAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Falha ao reconectar {Plugin}", plugin.Name);
            }
        }

        try
        {
            await _api.EnsureSessionAsync(CancellationToken.None);
        }
        catch
        {
            // ignored
        }

        RefreshView();
    }
}
