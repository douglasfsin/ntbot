using NtBot.Observability;
using NtBot.Worker;
using Serilog;

Log.Logger = ObservabilityHosting.CreateBootstrapLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);
    builder.UseNtBotSerilog("ntbot-worker");
    builder.Services.AddNtBotOpenTelemetry(
        builder.Configuration,
        builder.Environment,
        "ntbot-worker",
        ObservabilityHostKind.Worker);
    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "NtBot.Worker terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
