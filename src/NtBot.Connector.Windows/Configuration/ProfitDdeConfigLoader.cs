using System.Text.Json;

namespace NtBot.Connector.Windows.Configuration;

public static class ProfitDdeConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    public static ProfitDdeConfig Load(string path)
    {
        var fullPath = ResolvePath(path);
        if (!File.Exists(fullPath))
            return new ProfitDdeConfig();

        var json = File.ReadAllText(fullPath);
        return JsonSerializer.Deserialize<ProfitDdeConfig>(json, JsonOptions) ?? new ProfitDdeConfig();
    }

    public static void Save(string path, ProfitDdeConfig config)
    {
        var fullPath = ResolvePath(path);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(fullPath, json);
    }

    private static string ResolvePath(string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
}
