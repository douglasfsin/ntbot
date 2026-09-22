using NtBot.Connector.Windows.Providers.Profit;
using NtBot.Connector.Windows.SignalR;

namespace NtBot.Connector.Windows.UI;

public sealed class DdeReplayForm : Form
{
    private readonly IDdeReplayController _replay;
    private readonly INtBotApiClient _api;
    private readonly ListView _listView;
    private readonly CheckBox _replayCheck;
    private readonly TextBox _winContract;
    private readonly TextBox _wdoContract;
    private readonly Label _statusLabel;
    private readonly System.Windows.Forms.Timer _refreshTimer;

    public DdeReplayForm(IDdeReplayController replay, INtBotApiClient api)
    {
        _replay = replay;
        _api = api;

        Text = "DDE Replay — ProfitChart";
        Width = 640;
        Height = 420;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        ShowInTaskbar = true;

        var top = new Panel { Dock = DockStyle.Top, Height = 110, Padding = new Padding(10) };

        _replayCheck = new CheckBox
        {
            Text = "Ativar captura Replay (DDE contínuo + bloqueia RTD live)",
            AutoSize = true,
            Left = 10,
            Top = 10
        };

        var winLabel = new Label { Text = "WIN →", Left = 10, Top = 42, AutoSize = true };
        _winContract = new TextBox { Left = 60, Top = 38, Width = 100 };
        var wdoLabel = new Label { Text = "WDO →", Left = 180, Top = 42, AutoSize = true };
        _wdoContract = new TextBox { Left = 230, Top = 38, Width = 100 };

        var applyBtn = new Button { Text = "Aplicar", Left = 350, Top = 36, Width = 90, Height = 28 };
        applyBtn.Click += (_, _) => ApplyReplay();

        var resubBtn = new Button { Text = "Reconectar DDE", Left = 450, Top = 36, Width = 120, Height = 28 };
        resubBtn.Click += (_, _) =>
        {
            _replay.RequestResubscribe();
            RefreshView();
        };

        _statusLabel = new Label
        {
            Left = 10,
            Top = 74,
            Width = 580,
            Height = 28,
            ForeColor = Color.DarkSlateGray
        };

        top.Controls.Add(_replayCheck);
        top.Controls.Add(winLabel);
        top.Controls.Add(_winContract);
        top.Controls.Add(wdoLabel);
        top.Controls.Add(_wdoContract);
        top.Controls.Add(applyBtn);
        top.Controls.Add(resubBtn);
        top.Controls.Add(_statusLabel);

        _listView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable
        };
        _listView.Columns.Add("Lógico", 90);
        _listView.Columns.Add("DDE", 100);
        _listView.Columns.Add("Mirror", 100);
        _listView.Columns.Add("Tipo", 90);
        _listView.Columns.Add("Ativo", 60);

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(8) };
        var closeBtn = new Button { Text = "Fechar", Width = 80, Height = 28, Left = 8, Top = 8 };
        closeBtn.Click += (_, _) => Hide();
        bottom.Controls.Add(closeBtn);

        Controls.Add(_listView);
        Controls.Add(bottom);
        Controls.Add(top);

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 1500 };
        _refreshTimer.Tick += (_, _) => RefreshView();

        RefreshView(seedInputs: true);
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
        base.OnFormClosing(e);
    }

    private void ApplyReplay()
    {
        var contracts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(_winContract.Text))
            contracts["WIN"] = _winContract.Text.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(_wdoContract.Text))
            contracts["WDO"] = _wdoContract.Text.Trim().ToUpperInvariant();

        var status = _replay.SetReplayMode(_replayCheck.Checked, contracts);
        _ = _api.AckDdeReplayAsync(status.Enabled, status.Contracts, CancellationToken.None);
        RefreshView();
    }

    private void RefreshView(bool seedInputs = false)
    {
        if (IsDisposed) return;

        var status = _replay.GetStatus();
        if (seedInputs || !_replayCheck.Focused)
            _replayCheck.Checked = status.Enabled;

        if (seedInputs || (!_winContract.Focused && !_wdoContract.Focused))
        {
            if (status.Contracts.TryGetValue("WIN", out var win))
                _winContract.Text = win;
            if (status.Contracts.TryGetValue("WDO", out var wdo))
                _wdoContract.Text = wdo;
        }

        _statusLabel.Text = status.Enabled
            ? $"● Replay ON — {(status.IsConnected ? "DDE conectado" : "aguardando DDE")} · {status.Message}"
            : $"○ Replay OFF — {(status.IsConnected ? "DDE conectado" : "aguardando DDE")} · {status.Message}";
        _statusLabel.ForeColor = status.Enabled ? Color.DarkOrange : Color.DarkSlateGray;

        _listView.BeginUpdate();
        _listView.Items.Clear();
        foreach (var asset in status.Assets)
        {
            var item = new ListViewItem(asset.LogicalSymbol);
            item.SubItems.Add(asset.DdeSymbol);
            item.SubItems.Add(asset.MirrorFromSymbol ?? "—");
            item.SubItems.Add(asset.IsMirrorOnly ? "mirror" : "subscribe");
            item.SubItems.Add(asset.IsActive ? "sim" : "não");
            if (asset.LogicalSymbol is "WIN" or "WDO" or "WINQ26" or "WDOQ26"
                || asset.LogicalSymbol.StartsWith("WIN", StringComparison.OrdinalIgnoreCase)
                || asset.LogicalSymbol.StartsWith("WDO", StringComparison.OrdinalIgnoreCase))
            {
                item.ForeColor = status.Enabled ? Color.DarkOrange : Color.DarkGreen;
            }

            _listView.Items.Add(item);
        }

        _listView.EndUpdate();
    }
}
