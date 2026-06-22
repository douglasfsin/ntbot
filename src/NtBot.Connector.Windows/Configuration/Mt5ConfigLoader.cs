using System.Text.Json;
using System.Text.Json.Serialization;

namespace NtBot.Connector.Windows.Configuration;

public static class Mt5ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static Mt5Config Load(string baseDirectory, string relativePath, ConnectorOptions connectorOptions)
    {
        var path = Path.Combine(baseDirectory, relativePath);
        if (!File.Exists(path))
        {
            return new Mt5Config
            {
                ApiPort = connectorOptions.Mt5Port,
                Symbols = ["XAUUSD", "EURUSD"]
            };
        }

        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<Mt5Config>(json, JsonOptions) ?? new Mt5Config();
        if (config.ApiPort <= 0)
            config.ApiPort = connectorOptions.Mt5Port;

        config.Symbols = config.Symbols
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        config.SymbolAliases = config.SymbolAliases
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
            .ToDictionary(
                kv => kv.Key.Trim().ToUpperInvariant(),
                kv => kv.Value.Trim(),
                StringComparer.OrdinalIgnoreCase);

        return config;
    }
}
