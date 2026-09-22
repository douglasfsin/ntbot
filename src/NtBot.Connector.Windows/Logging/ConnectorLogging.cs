using Serilog;

namespace NtBot.Connector.Windows.Logging;

public static class ConnectorLogging
{
    public const int RollIntervalMinutes = 30;

    public static string LogsDirectory =>
        Path.Combine(AppContext.BaseDirectory, "Logs");

    public static string EnsureLogsDirectory()
    {
        Directory.CreateDirectory(LogsDirectory);
        return LogsDirectory;
    }

    /// <summary>
    /// Bucket de rotação a cada 30 min — ex.: connector-20260620-1830.log
    /// </summary>
    public static string GetRollBucket(DateTimeOffset timestamp)
    {
        var local = timestamp.ToLocalTime();
        var minuteSlot = local.Minute < RollIntervalMinutes ? 0 : RollIntervalMinutes;
        return $"{local:yyyyMMdd}-{local:HH}{minuteSlot:D2}";
    }

    public static LoggerConfiguration Configure(LoggerConfiguration configuration, string logsDir) =>
        configuration
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Async(sinks => sinks.Map(
                e => GetRollBucket(e.Timestamp),
                (bucket, mapSink) => mapSink.File(
                    Path.Combine(logsDir, $"connector-{bucket}.log"),
                    shared: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(2),
                    rollOnFileSizeLimit: false,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")))
            .WriteTo.Logger(lc => lc
                .Filter.ByIncludingOnly(e =>
                {
                    var msg = e.RenderMessage();
                    return msg.Contains("[ReplayMonitor]", StringComparison.OrdinalIgnoreCase)
                        || msg.Contains("ReplayMode", StringComparison.OrdinalIgnoreCase)
                        || msg.Contains("DDE-REPLAY", StringComparison.OrdinalIgnoreCase)
                        || msg.Contains("Replay DDE", StringComparison.OrdinalIgnoreCase)
                        || msg.Contains("RTD fallback bloqueado", StringComparison.OrdinalIgnoreCase);
                })
                .WriteTo.Async(sinks => sinks.Map(
                    e => GetRollBucket(e.Timestamp),
                    (bucket, mapSink) => mapSink.File(
                        Path.Combine(logsDir, $"dde-replay-{bucket}.log"),
                        shared: true,
                        flushToDiskInterval: TimeSpan.FromSeconds(1),
                        rollOnFileSizeLimit: false,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))));
}
