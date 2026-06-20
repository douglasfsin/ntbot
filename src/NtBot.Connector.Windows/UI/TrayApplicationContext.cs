using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NtBot.Connector.Windows.Configuration;
using NtBot.Connector.Windows.Core;
using NtBot.Connector.Windows.Services;
using NtBot.Connector.Windows.SignalR;
using NtBot.Connector.Windows.UI;
using Serilog;

namespace NtBot.Connector.Windows;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _menu;
    private readonly IHost _host;
    private readonly TrayHostForm _hostForm;
    private StatusForm? _statusForm;
    private bool _hostStarted;

    public TrayApplicationContext(IHost host)
    {
        _host = host;
        var options = host.Services.GetRequiredService<IOptions<ConnectorOptions>>().Value;

        _hostForm = new TrayHostForm();
        MainForm = _hostForm;

        _menu = BuildMenu(options.Version);

        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = $"NTBot Connector v{options.Version}",
            Visible = true
        };

        // Menu manual — mais confiável que ContextMenuStrip automático do NotifyIcon
        _tray.MouseUp += OnTrayMouseUp;
        _tray.DoubleClick += (_, _) => RunOnUiThread(ShowStatusPanel);

        _hostForm.Load += OnHostFormLoad;
        _hostForm.Show();

        var apiKeyHint = string.IsNullOrWhiteSpace(options.ApiKey)
            ? "Configure ApiKey em appsettings.json"
            : "Conectando à API…";

        _tray.ShowBalloonTip(
            3000,
            "NTBot Connector",
            $"Rodando na bandeja do sistema.\nDuplo clique para abrir o painel.\n{apiKeyHint}",
            ToolTipIcon.Info);
    }

    private ContextMenuStrip BuildMenu(string version)
    {
        var menu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            ShowCheckMargin = false
        };

        menu.Items.Add(new ToolStripMenuItem($"NTBot Connector v{version}") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());

        var statusItem = new ToolStripMenuItem("Painel de status");
        statusItem.Click += (_, _) => RunOnUiThread(ShowStatusPanel);
        menu.Items.Add(statusItem);

        var logsItem = new ToolStripMenuItem("Abrir pasta de logs");
        logsItem.Click += (_, _) => RunOnUiThread(OpenLogsFolder);
        menu.Items.Add(logsItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Sair");
        exitItem.Click += (_, _) => RunOnUiThread(RequestExit);
        menu.Items.Add(exitItem);

        return menu;
    }

    private void OnHostFormLoad(object? sender, EventArgs e)
    {
        _hostForm.Load -= OnHostFormLoad;
        StartHostOnce();
    }

    private void StartHostOnce()
    {
        if (_hostStarted)
            return;

        _hostStarted = true;
        _hostForm.Load -= OnHostFormLoad;

        Log.Information("Iniciando host em background (message loop ativo)");

        _ = Task.Run(async () =>
        {
            try
            {
                await _host.StartAsync();
                Log.Information("NTBot Connector host iniciado");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Falha ao iniciar host em background");
                RunOnUiThread(() =>
                    MessageBox.Show(
                        _hostForm,
                        $"Falha ao conectar serviços:\n{ex.Message}",
                        "NTBot Connector",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning));
            }
        });
    }

    private void OnTrayMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
            return;

        _menu.Show(Cursor.Position);
    }

    private void RunOnUiThread(Action action)
    {
        if (_hostForm.IsDisposed)
            return;

        if (_hostForm.InvokeRequired)
        {
            _hostForm.Invoke(action);
            return;
        }

        action();
    }

    private StatusForm GetStatusForm()
    {
        if (_statusForm is { IsDisposed: false })
            return _statusForm;

        _statusForm = new StatusForm(
            _host.Services.GetRequiredService<IPlatformStatusRegistry>(),
            _host.Services.GetRequiredService<INtBotApiClient>(),
            _host.Services.GetRequiredService<IEnumerable<IBrokerPlugin>>(),
            _host.Services.GetRequiredService<IOptions<ConnectorOptions>>());

        return _statusForm;
    }

    private void ShowStatusPanel()
    {
        try
        {
            var form = GetStatusForm();
            if (form.IsDisposed)
                return;

            Log.Information("Abrindo painel de status");

            if (form.Visible)
            {
                if (form.WindowState == FormWindowState.Minimized)
                    form.WindowState = FormWindowState.Normal;

                form.Activate();
                form.BringToFront();
                form.Focus();
                return;
            }

            form.Show();
            form.WindowState = FormWindowState.Normal;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.TopMost = true;
            form.Activate();
            form.BringToFront();
            form.TopMost = false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro ao abrir painel de status");
            MessageBox.Show(
                _hostForm,
                $"Não foi possível abrir o painel:\n{ex.Message}",
                "NTBot Connector",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void OpenLogsFolder()
    {
        try
        {
            var logsDir = Logging.ConnectorLogging.LogsDirectory;
            Directory.CreateDirectory(logsDir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = logsDir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro ao abrir pasta de logs");
            MessageBox.Show(
                _hostForm,
                $"Não foi possível abrir a pasta de logs:\n{ex.Message}",
                "NTBot Connector",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void RequestExit()
    {
        _tray.Visible = false;
        ExitThread();
    }

    protected override void ExitThreadCore()
    {
        _tray.MouseUp -= OnTrayMouseUp;
        _tray.Visible = false;
        _tray.Dispose();
        _menu.Dispose();

        if (_statusForm is { IsDisposed: false })
            _statusForm.Close();

        _ = Task.Run(async () =>
        {
            try
            {
                await _host.StopAsync(TimeSpan.FromSeconds(5));
                _host.Dispose();
            }
            catch
            {
                // best effort shutdown
            }
        });

        base.ExitThreadCore();
    }
}
