using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NtBot.Connector.Windows.Configuration;
using NtBot.Connector.Windows.Core;
using NtBot.Connector.Windows.Providers.MT5;
using NtBot.Connector.Windows.Providers.NinjaTrader;
using NtBot.Connector.Windows.Providers.Profit;
using NtBot.Connector.Windows.Providers.TradingView;
using NtBot.Connector.Windows.Services;
using NtBot.Connector.Windows.SignalR;
using NtBot.Connector.Windows.Updater;
using NtBot.Connector.Windows.Workers;
using Serilog;

namespace NtBot.Connector.Windows;

public static class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("Logs/connector-.txt", rollingInterval: Serilog.RollingInterval.Day)
            .CreateLogger();

        try
        {
            var host = Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureServices(ConfigureServices)
                .Build();

            Application.Run(new TrayApplicationContext(host));
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Connector Windows encerrado inesperadamente");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.Configure<ConnectorOptions>(context.Configuration.GetSection(ConnectorOptions.SectionName));
        services.AddMemoryCache();
        services.AddSingleton<ConnectorSessionState>();
        services.AddSingleton<OfflineQueue>();
        services.AddSingleton<IDeltaAggregator, DeltaAggregator>();
        services.AddSingleton<ProviderOrchestrator>();

        services.AddHttpClient<INtBotApiClient, NtBotApiClient>();
        services.AddHttpClient<NinjaTraderProvider>();

        services.AddSingleton<ProfitRtdWorker>();
        services.AddSingleton<IBrokerPlugin>(sp => sp.GetRequiredService<ProfitRtdWorker>());
        services.AddHostedService(sp => sp.GetRequiredService<ProfitRtdWorker>());

        services.AddSingleton<Mt5Provider>();
        services.AddSingleton<IBrokerPlugin>(sp => sp.GetRequiredService<Mt5Provider>());

        services.AddSingleton<NinjaTraderProvider>();
        services.AddSingleton<IBrokerPlugin>(sp => sp.GetRequiredService<NinjaTraderProvider>());

        services.AddSingleton<TradingViewProvider>();
        services.AddSingleton<IBrokerPlugin>(sp => sp.GetRequiredService<TradingViewProvider>());

        services.AddHostedService<ConnectorIngestWorker>();
        services.AddHostedService<NtBotHubClient>();
        services.AddHostedService<AutoUpdateWorker>();
    }
}

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly IHost _host;

    public TrayApplicationContext(IHost host)
    {
        _host = host;
        var options = host.Services.GetRequiredService<IOptions<ConnectorOptions>>().Value;

        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = $"NTBot Connector v{options.Version}"
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add($"NTBot Connector v{options.Version}", null, (_, _) => { });
        menu.Items.Add("Sair", null, (_, _) => ExitThread());
        _tray.ContextMenuStrip = menu;

        _ = _host.StartAsync();
    }

    protected override void ExitThreadCore()
    {
        _tray.Visible = false;
        _tray.Dispose();
        _host.StopAsync().GetAwaiter().GetResult();
        base.ExitThreadCore();
    }
}
