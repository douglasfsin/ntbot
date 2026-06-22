using System.Text;
using System.Text.Json;

namespace NtBot.Connector.Windows.Providers.MT5;

internal static class Mt5SseReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task ReadStreamAsync(
        HttpClient client,
        string streamUrl,
        Func<string, string, CancellationToken, Task> onEvent,
        CancellationToken ct)
    {
        using var response = await client.GetAsync(streamUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase}). {body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        string? eventType = null;
        var dataBuffer = new StringBuilder();

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null)
                break;

            if (line.StartsWith("event:", StringComparison.Ordinal))
                eventType = line["event:".Length..].Trim();
            else if (line.StartsWith("data:", StringComparison.Ordinal))
                dataBuffer.Append(line["data:".Length..].Trim());
            else if (line.Length == 0)
            {
                if (eventType != null && dataBuffer.Length > 0)
                    await onEvent(eventType, dataBuffer.ToString(), ct);

                eventType = null;
                dataBuffer.Clear();
            }
        }
    }

    public static Mt5PyTick? ParseTick(string json) =>
        JsonSerializer.Deserialize<Mt5PyTick>(json, JsonOptions);
}

internal sealed class Mt5PyTick
{
    public string Symbol { get; set; } = string.Empty;
    public double Bid { get; set; }
    public double Ask { get; set; }
    public double Last { get; set; }
    public double Spread { get; set; }
    public long Volume { get; set; }
    public long TimeMsc { get; set; }
    public string? Time { get; set; }
}

internal sealed class Mt5PyStatus
{
    public bool Connected { get; set; }
    public Mt5PyAccount? Account { get; set; }
}

internal sealed class Mt5PyAccount
{
    public long? Login { get; set; }
    public string? Server { get; set; }
    public string? Currency { get; set; }
    public double? Balance { get; set; }
}
