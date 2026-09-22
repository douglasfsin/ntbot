using NtBot.Connector.Windows.Configuration;
using NtBot.Connector.Windows.Providers.Profit;

namespace NtBot.Connector.Windows.UI;

/// <summary>
/// Seletor da fonte de market data Profit: DDE | RTD | ProfitDLL.
/// </summary>
public sealed class ProfitConnectionForm : Form
{
    private readonly IProfitMarketDataModeController _mode;
    private readonly ProfitMarketDataCoordinator _coordinator;
    private readonly ComboBox _modeCombo;
    private readonly Label _statusLabel;
    private readonly Label _detailLabel;
    private readonly CheckBox _rtdFallbackCheck;
    private readonly TextBox _marketDataUrl;
    private readonly System.Windows.Forms.Timer _refreshTimer;

    public ProfitConnectionForm(
        IProfitMarketDataModeController mode,
        ProfitMarketDataCoordinator coordinator,
        Microsoft.Extensions.Options.IOptions<ConnectorOptions> options)
    {
        _mode = mode;
        _coordinator = coordinator;

        Text = "Fonte Profit — Market Data";
        Width = 520;
        Height = 320;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        ShowInTaskbar = true;

        var opts = options.Value;

        var modeLabel = new Label
        {
            Text = "Fonte de cotações:",
            Left = 16,
            Top = 18,
            AutoSize = true
        };

        _modeCombo = new ComboBox
        {
            Left = 16,
            Top = 42,
            Width = 360,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _modeCombo.Items.AddRange(
        [
            ProfitMarketDataMode.Dde.ToDisplayName(),
            ProfitMarketDataMode.Rtd.ToDisplayName(),
            ProfitMarketDataMode.ProfitDll.ToDisplayName()
        ]);
        _modeCombo.SelectedIndex = (int)_mode.Current;

        _rtdFallbackCheck = new CheckBox
        {
            Text = "Permitir RTD como fallback quando DDE estiver sem ticks",
            Left = 16,
            Top = 78,
            Width = 460,
            AutoSize = true,
            Checked = opts.AllowRtdFallbackWhenDdeStale
        };

        var urlLabel = new Label
        {
            Text = "MarketData.API (ProfitDLL):",
            Left = 16,
            Top = 112,
            AutoSize = true
        };

        _marketDataUrl = new TextBox
        {
            Left = 16,
            Top = 134,
            Width = 460,
            Text = opts.MarketDataApiBaseUrl
        };

        _statusLabel = new Label
        {
            Left = 16,
            Top = 176,
            Width = 460,
            Height = 24,
            Font = new Font(Font, FontStyle.Bold)
        };

        _detailLabel = new Label
        {
            Left = 16,
            Top = 204,
            Width = 460,
            Height = 40,
            ForeColor = Color.DimGray
        };

        var applyBtn = new Button
        {
            Text = "Aplicar",
            Left = 16,
            Top = 250,
            Width = 100,
            Height = 28
        };
        applyBtn.Click += (_, _) => Apply();

        var closeBtn = new Button
        {
            Text = "Fechar",
            Left = 126,
            Top = 250,
            Width = 90,
            Height = 28
        };
        closeBtn.Click += (_, _) => Hide();

        Controls.Add(modeLabel);
        Controls.Add(_modeCombo);
        Controls.Add(_rtdFallbackCheck);
        Controls.Add(urlLabel);
        Controls.Add(_marketDataUrl);
        Controls.Add(_statusLabel);
        Controls.Add(_detailLabel);
        Controls.Add(applyBtn);
        Controls.Add(closeBtn);

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _refreshTimer.Tick += (_, _) => RefreshStatus();
        _mode.ModeChanged += _ =>
        {
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(RefreshStatus);
        };

        RefreshStatus();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _refreshTimer.Start();
        _modeCombo.SelectedIndex = (int)_mode.Current;
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
        base.OnFormClosing(e);
    }

    private void Apply()
    {
        var selected = (ProfitMarketDataMode)_modeCombo.SelectedIndex;
        _mode.SetMode(selected, persist: true);
        PersistExtraSettings();
        _coordinator.AllowRtdFallback =
            selected == ProfitMarketDataMode.Dde
            && _rtdFallbackCheck.Checked
            && !_coordinator.ReplayModeActive;
        _mode.NotifySettingsChanged();
        RefreshStatus();

        MessageBox.Show(
            this,
            $"Fonte alterada para:\n{selected.ToDisplayName()}\n\nOs providers aplicam a troca automaticamente.",
            "Fonte Profit",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void PersistExtraSettings()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path))
                return;

            var text = File.ReadAllText(path);
            var root = System.Text.Json.Nodes.JsonNode.Parse(text) as System.Text.Json.Nodes.JsonObject
                       ?? new System.Text.Json.Nodes.JsonObject();
            var connector = root["Connector"] as System.Text.Json.Nodes.JsonObject
                            ?? new System.Text.Json.Nodes.JsonObject();
            root["Connector"] = connector;

            connector["AllowRtdFallbackWhenDdeStale"] = _rtdFallbackCheck.Checked;
            if (!string.IsNullOrWhiteSpace(_marketDataUrl.Text))
                connector["MarketDataApiBaseUrl"] = _marketDataUrl.Text.Trim();

            File.WriteAllText(
                path,
                root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // best effort — mode still applied in memory
        }
    }

    private void RefreshStatus()
    {
        if (IsDisposed) return;

        var mode = _mode.Current;
        if (_modeCombo.SelectedIndex != (int)mode && !_modeCombo.DroppedDown)
            _modeCombo.SelectedIndex = (int)mode;

        _statusLabel.Text = $"Ativo: {mode.ToDisplayName()}  ·  Source={_coordinator.ActiveSource}";
        _statusLabel.ForeColor = mode switch
        {
            ProfitMarketDataMode.Dde => Color.DarkOrange,
            ProfitMarketDataMode.Rtd => Color.DarkBlue,
            ProfitMarketDataMode.ProfitDll => Color.DarkGreen,
            _ => Color.Black
        };

        _detailLabel.Text =
            $"DDE hb={(Fmt(_coordinator.LastDdeTickUtc))}  |  RTD hb={(Fmt(_coordinator.LastRtdTickUtc))}  |  DLL hb={(Fmt(_coordinator.LastDllTickUtc))}\n" +
            $"RTD fallback={_coordinator.AllowRtdFallback}  Replay={_coordinator.ReplayModeActive}";

        _rtdFallbackCheck.Enabled = mode == ProfitMarketDataMode.Dde || _modeCombo.SelectedIndex == 0;
    }

    private static string Fmt(DateTime? utc)
    {
        if (!utc.HasValue) return "—";
        return $"{(DateTime.UtcNow - utc.Value).TotalSeconds:F1}s";
    }
}
