using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NtBot.Connector.Windows.Configuration;
using NtBot.Connector.Windows.Core;
using NtBot.Connector.Windows.Providers.MT5;
using NtBot.Connector.Windows.Providers.NinjaTrader;
using NtBot.Connector.Windows.Providers.Profit;
using NtBot.Connector.Windows.Providers.TradingView;
using NtBot.Connector.Windows.Services;
using NtBot.Connector.Windows.SignalR;
using NtBot.Connector.Windows.Logging;
using NtBot.Connector.Windows.UI;
using NtBot.Connector.Windows.Updater;
using NtBot.Connector.Windows.Workers;
using Serilog;

namespace NtBot.Connector.Windows;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        var logsDir = ConnectorLogging.EnsureLogsDirectory();

        Application.ThreadException += (_, e) =>
            Log.Error(e.Exception, "Exceção não tratada na UI");

        IHost? host = null;

        try
        {
            host = Host.CreateDefaultBuilder(args)
                .UseSerilog((context, _, configuration) =>
                {
                    ConnectorLogging.Configure(configuration, logsDir);
                    configuration.ReadFrom.Configuration(context.Configuration);
                })
                .ConfigureServices(ConfigureServices)
                .Build();

            Log.Information(
                "NTBot Connector iniciando em {BaseDir} (logs a cada {Minutes} min, shared)",
                AppContext.BaseDirectory,
                ConnectorLogging.RollIntervalMinutes);

            var trayContext = new TrayApplicationContext(host);
            Application.Run(trayContext);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Connector Windows encerrado inesperadamente");
            MessageBox.Show(
                $"Falha ao iniciar o NTBot Connector:\n\n{ex.Message}",
                "NTBot Connector",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            if (host != null)
            {
                try
                {
                    host.StopAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
                    host.Dispose();
                }
                catch
                {
                    // shutdown
                }
            }

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
        services.AddSingleton<IPlatformStatusRegistry, PlatformStatusRegistry>();
        services.AddSingleton<ProviderOrchestrator>();

        services.AddHttpClient<INtBotApiClient, NtBotApiClient>(client =>
            client.Timeout = TimeSpan.FromSeconds(30));
        services.AddHttpClient<NinjaTraderProvider>(client =>
            client.Timeout = TimeSpan.FromSeconds(30));
        services.AddHttpClient<AutoUpdateWorker>(client =>
            client.Timeout = TimeSpan.FromSeconds(30));

        services.AddSingleton<ProfitRtdWorker>();
        services.AddSingleton<IBrokerPlugin>(sp => sp.GetRequiredService<ProfitRtdWorker>());
        services.AddHostedService(sp => sp.GetRequiredService<ProfitRtdWorker>());

        services.AddSingleton<Mt5Provider>();
        services.AddSingleton<IBrokerPlugin>(sp => sp.GetRequiredService<Mt5Provider>());

        services.AddSingleton<NinjaTraderProvider>();
        services.AddSingleton<IBrokerPlugin>(sp => sp.GetRequiredService<NinjaTraderProvider>());

        services.AddSingleton<TradingViewProvider>();
        services.AddSingleton<IBrokerPlugin>(sp => sp.GetRequiredService<TradingViewProvider>());

        services.AddHostedService<BrokerSupervisorWorker>();
        services.AddHostedService<ConnectorIngestWorker>();
        services.AddHostedService<NtBotHubClient>();
        services.AddHostedService<AutoUpdateWorker>();
    }
}
