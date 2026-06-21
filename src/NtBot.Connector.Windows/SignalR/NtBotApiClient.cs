using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NtBot.Connector.Windows.Configuration;
using NtBot.Connector.Windows.Dtos;
using NtBot.Connector.Windows.Services;
using NtBot.Shared.Normalized;

namespace NtBot.Connector.Windows.SignalR;

public interface INtBotApiClient
{
    bool IsOnline { get; }
    bool IsConfigured { get; }
    Task EnsureSessionAsync(CancellationToken ct);
    Task SendIngestAsync(NormalizedIngestBatch batch, CancellationToken ct);
    Task SendCandlesAsync(CandleIngestBatch batch, CancellationToken ct);
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
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task EnsureSessionAsync(CancellationToken ct)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("ApiKey não configurada — defina Connector:ApiKey em appsettings.json");
            return;
        }

        if (!string.IsNullOrWhiteSpace(_session.SessionId)) return;

        var payload = new
        {
            version = _options.Version,
            machineName = Environment.MachineName,
            osVersion = Environment.OSVersion.VersionString
        };

        var response = await ExecuteWithRetryAsync(
            () => _http.PostAsJsonAsync("api/connector/session", payload, ct),
            ct,
            treatUnauthorizedAsFatal: true);

        if (!response.IsSuccessStatusCode)
        {
            IsOnline = false;
            _logger.LogWarning("Falha ao iniciar sessão: {Status}", (int)response.StatusCode);
            return;
        }

        var session = await response.Content.ReadFromJsonAsync<SessionResponse>(cancellationToken: ct);
        if (session != null)
            _session.SessionId = session.SessionId.ToString();

        IsOnline = true;
    }

    public async Task SendIngestAsync(NormalizedIngestBatch batch, CancellationToken ct)
    {
        if (!IsConfigured) return;

        batch = batch with
        {
            SessionId = _session.SessionId,
            ConnectorVersion = _options.Version
        };

        try
        {
            var json = JsonSerializer.Serialize(batch);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await ExecuteWithRetryAsync(
                () => _http.PostAsync("api/connector/ingest", content, ct),
                ct);

            if (!response.IsSuccessStatusCode)
            {
                IsOnline = false;
                _queue.Enqueue(batch);
                return;
            }

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

    public async Task SendCandlesAsync(CandleIngestBatch batch, CancellationToken ct)
    {
        if (!IsConfigured) return;

        try
        {
            var response = await ExecuteWithRetryAsync(
                () => _http.PostAsJsonAsync("api/connector/candles", batch, ct),
                ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Falha ao enviar candles: HTTP {Status} — {Body}", (int)response.StatusCode, body);
                return;
            }

            IsOnline = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Envio de candles falhou");
        }
    }

    private async Task<HttpResponseMessage> ExecuteWithRetryAsync(
        Func<Task<HttpResponseMessage>> action,
        CancellationToken ct,
        bool treatUnauthorizedAsFatal = false)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var response = await action();
                if (treatUnauthorizedAsFatal && response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    return response;

                if (response.IsSuccessStatusCode)
                {
                    _retryAttempt = 0;
                    return response;
                }

                _retryAttempt++;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _retryAttempt++;
                _logger.LogWarning(ex, "Retry HTTP (tentativa {Attempt})", _retryAttempt);
            }

            var delay = Math.Min(
                _options.ReconnectBaseDelayMs * (int)Math.Pow(2, Math.Min(_retryAttempt - 1, 6)),
                _options.MaxReconnectDelayMs);
            await Task.Delay(delay, ct);
        }

        throw new OperationCanceledException(ct);
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
