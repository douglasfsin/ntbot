using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NtBot.Connector.Windows.Configuration;
using NtBot.Connector.Windows.Services;
using NtBot.Shared.Normalized;

namespace NtBot.Connector.Windows.SignalR;

public interface INtBotApiClient
{
    bool IsOnline { get; }
    Task EnsureSessionAsync(CancellationToken ct);
    Task SendIngestAsync(NormalizedIngestBatch batch, CancellationToken ct);
    Task SendHeartbeatAsync(CancellationToken ct);
}

public class NtBotApiClient : INtBotApiClient
{
    private readonly HttpClient _http;
    private readonly ConnectorOptions _options;
    private readonly ConnectorSessionState _session;
    private readonly OfflineQueue _queue;
    private readonly ILogger<NtBotApiClient> _logger;
    private int _retryAttempt;

    public NtBotApiClient(
        HttpClient http,
        IOptions<ConnectorOptions> options,
        ConnectorSessionState session,
        OfflineQueue queue,
        ILogger<NtBotApiClient> logger)
    {
        _http = http;
        _options = options.Value;
        _session = session;
        _queue = queue;
        _logger = logger;

        _http.BaseAddress = new Uri(_options.ApiBaseUrl.TrimEnd('/') + "/");
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            _http.DefaultRequestHeaders.Add("X-Connector-Api-Key", _options.ApiKey);
    }

    public bool IsOnline { get; private set; }

    public async Task EnsureSessionAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_session.SessionId)) return;

        var payload = new
        {
            version = _options.Version,
            machineName = Environment.MachineName,
            osVersion = Environment.OSVersion.VersionString
        };

        var response = await ExecuteWithRetryAsync(
            () => _http.PostAsJsonAsync("api/connector/session", payload, ct), ct);

        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>(cancellationToken: ct);
        if (session != null)
            _session.SessionId = session.SessionId.ToString();
    }

    public async Task SendIngestAsync(NormalizedIngestBatch batch, CancellationToken ct)
    {
        batch = batch with
        {
            SessionId = _session.SessionId,
            ConnectorVersion = _options.Version
        };

        try
        {
            var json = JsonSerializer.Serialize(batch);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await ExecuteWithRetryAsync(() => _http.PostAsync("api/connector/ingest", content, ct), ct);
            response.EnsureSuccessStatusCode();
            IsOnline = true;
            _retryAttempt = 0;
            await _queue.FlushAsync(SendIngestAsync, ct);
        }
        catch (Exception ex)
        {
            IsOnline = false;
            _logger.LogWarning(ex, "Ingest offline — enfileirando batch");
            _queue.Enqueue(batch);
        }
    }

    public Task SendHeartbeatAsync(CancellationToken ct) =>
        SendIngestAsync(new NormalizedIngestBatch
        {
            SessionId = _session.SessionId,
            ConnectorVersion = _options.Version,
            IsDelta = true
        }, ct);

    private async Task<HttpResponseMessage> ExecuteWithRetryAsync(
        Func<Task<HttpResponseMessage>> action, CancellationToken ct)
    {
        while (true)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _retryAttempt++;
                var delay = Math.Min(
                    _options.ReconnectBaseDelayMs * (int)Math.Pow(2, _retryAttempt - 1),
                    _options.MaxReconnectDelayMs);
                _logger.LogWarning(ex, "Retry HTTP em {Delay}ms (tentativa {Attempt})", delay, _retryAttempt);
                await Task.Delay(delay, ct);
            }
        }
    }

    private sealed class SessionResponse
    {
        public Guid SessionId { get; set; }
        public string SessionToken { get; set; } = string.Empty;
    }
}

public class OfflineQueue
{
    private readonly Queue<NormalizedIngestBatch> _queue = new();
    private readonly object _lock = new();

    public void Enqueue(NormalizedIngestBatch batch)
    {
        lock (_lock)
        {
            if (_queue.Count >= 500) _queue.Dequeue();
            _queue.Enqueue(batch);
        }
    }

    public async Task FlushAsync(Func<NormalizedIngestBatch, CancellationToken, Task> sender, CancellationToken ct)
    {
        while (true)
        {
            NormalizedIngestBatch? batch;
            lock (_lock)
            {
                if (_queue.Count == 0) return;
                batch = _queue.Dequeue();
            }

            await sender(batch, ct);
        }
    }
}
