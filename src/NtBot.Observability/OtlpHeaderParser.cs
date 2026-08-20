namespace NtBot.Observability;

public static class OtlpHeaderParser
{
    public static string? Merge(string? headers, string? ingestionKey)
    {
        var map = ToDictionary(headers);
        if (!string.IsNullOrWhiteSpace(ingestionKey)
            && !map.ContainsKey("signoz-ingestion-key"))
        {
            map["signoz-ingestion-key"] = ingestionKey.Trim();
        }

        if (map.Count == 0)
            return null;

        return string.Join(",", map.Select(kv => $"{kv.Key}={kv.Value}"));
    }

    public static Dictionary<string, string> ToDictionary(string? headers)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(headers))
            return map;

        foreach (var part in headers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
                continue;

            var key = part[..eq].Trim();
            var value = part[(eq + 1)..].Trim();
            if (key.Length == 0 || value.Length == 0)
                continue;

            map[key] = value;
        }

        return map;
    }
}
