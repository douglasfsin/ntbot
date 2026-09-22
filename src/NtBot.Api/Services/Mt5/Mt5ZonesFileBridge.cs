using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using NtBot.Api.Configuration;
using NtBot.TradingIntelligence.Engine;

namespace NtBot.Api.Services.Mt5;

public sealed class Mt5ZonesFileBridge : IMt5ZonesFileBridge
{
    private readonly Mt5ZonesFileBridgeOptions _options;
    private readonly ILogger<Mt5ZonesFileBridge> _logger;
    private readonly string _outputDir;

    public Mt5ZonesFileBridge(
        IOptions<Mt5ZonesFileBridgeOptions> options,
        ILogger<Mt5ZonesFileBridge> logger)
    {
        _options = options.Value;
        _logger = logger;
        _outputDir = ResolveOutputDirectory(_options.OutputDirectory);
    }

    public string ResolvedOutputDirectory => _outputDir;

    public string? WriteZonesFile(
        string symbol,
        string timeframe,
        IReadOnlyList<Mt5ChartZoneMark> zones,
        DateTimeOffset updatedAt)
    {
        if (!_options.Enabled)
            return null;

        try
        {
            Directory.CreateDirectory(_outputDir);

            var safeSymbol = SanitizeFileToken(symbol);
            var fileName = _options.FileNamePattern
                .Replace("{symbol}", safeSymbol, StringComparison.OrdinalIgnoreCase)
                .Replace("{SYMBOL}", safeSymbol, StringComparison.Ordinal);
            var path = Path.Combine(_outputDir, fileName);

            var lines = zones.Select(FormatDelimLine);
            var sb = new StringBuilder();
            sb.Append("#NTBot|symbol=").Append(safeSymbol)
                .Append("|timeframe=").Append(timeframe)
                .Append("|count=").Append(zones.Count)
                .Append("|updatedAt=").Append(updatedAt.UtcDateTime.ToString("o", CultureInfo.InvariantCulture))
                .AppendLine();
            foreach (var line in lines)
                sb.AppendLine(line);

            // Atomic-ish write: temp then replace.
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Copy(tmp, path, overwrite: true);
            File.Delete(tmp);

            _logger.LogDebug(
                "MT5 zones file bridge wrote {Count} zones → {Path}",
                zones.Count,
                path);
            return path;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write MT5 zones file for {Symbol}", symbol);
            return null;
        }
    }

    internal static string FormatDelimLine(Mt5ChartZoneMark z) =>
        string.Join('|',
            z.Id,
            z.Kind,
            z.Side,
            z.PriceLow.ToString(CultureInfo.InvariantCulture),
            z.PriceHigh.ToString(CultureInfo.InvariantCulture),
            z.FillColor,
            z.FillAlpha.ToString(CultureInfo.InvariantCulture),
            z.LineStyle,
            AsciiSafeLabel(z.Label.Replace('|', '/')),
            z.Score.ToString(CultureInfo.InvariantCulture),
            z.Source);

    /// <summary>Strip non-ASCII so MT5 FILE_ANSI reads labels without glyph corruption.</summary>
    internal static string AsciiSafeLabel(string label)
    {
        if (string.IsNullOrEmpty(label)) return label;
        var sb = new StringBuilder(label.Length);
        foreach (var ch in label)
        {
            if (ch is >= (char)32 and < (char)127)
                sb.Append(ch);
            else if (ch is '↑' or '▲')
                sb.Append('+');
            else if (ch is '↓' or '▼')
                sb.Append('-');
            else if (ch == '★')
                sb.Append('*');
            else if (ch is '—' or '–')
                sb.Append('-');
            // drop other unicode (accents in Premio already ASCII in ShortLabel)
        }
        return sb.ToString();
    }

    private static string ResolveOutputDirectory(string configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
            return Environment.ExpandEnvironmentVariables(configured.Trim());

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "MetaQuotes", "Terminal", "Common", "Files");
    }

    private static string SanitizeFileToken(string symbol)
    {
        var s = symbol.Trim().ToUpperInvariant();
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (char.IsLetterOrDigit(ch) || ch is '_' or '-')
                sb.Append(ch);
        }
        return sb.Length > 0 ? sb.ToString() : "UNKNOWN";
    }
}
