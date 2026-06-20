using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace NtBot.Web.Services;

public class ConnectorApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthSession _session;
    private readonly ILogger<ConnectorApiClient> _logger;

    public ConnectorApiClient(
        IHttpClientFactory httpClientFactory,
        AuthSession session,
        ILogger<ConnectorApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _session = session;
        _logger = logger;
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient("NtBotApi");
        if (!string.IsNullOrEmpty(_session.Token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _session.Token);
        return client;
    }

    public async Task<ConnectorStatusModel?> GetStatusAsync()
    {
        var client = CreateClient();
        var response = await client.GetAsync("api/connector/status");
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Connector status falhou | Status={Status}", (int)response.StatusCode);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ConnectorStatusModel>();
    }

    public async Task<ConnectorKeyCreatedModel?> GenerateKeyAsync()
    {
        var client = CreateClient();
        var response = await client.PostAsync("api/connector/keys", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ConnectorKeyCreatedModel>();
    }

    public async Task<ConnectorKeyCreatedModel?> RotateKeyAsync(Guid keyId)
    {
        var client = CreateClient();
        var response = await client.PostAsync($"api/connector/keys/{keyId}/rotate", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ConnectorKeyCreatedModel>();
    }
}

public class ConnectorStatusModel
{
    public bool HasActiveKey { get; set; }
    public string? KeyPrefix { get; set; }
    public DateTime? KeyExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public string? LastUsedIp { get; set; }
    public bool IsConnected { get; set; }
    public DateTime? LastConnectionAt { get; set; }
    public string? ConnectedVersion { get; set; }
    public string? MachineName { get; set; }
    public string Plan { get; set; } = "FREE";
    public bool LicenseActive { get; set; }
    public string? LatestVersion { get; set; }
    public string? ReleaseNotes { get; set; }
    public string? DownloadPath { get; set; }
    public long? DownloadSizeBytes { get; set; }
}

public class ConnectorKeyCreatedModel
{
    public Guid KeyId { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
