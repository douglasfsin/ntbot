using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using NtBot.Connector.Windows.Configuration;

namespace NtBot.Connector.Windows.Updater;

public class AutoUpdateWorker : BackgroundService
{
    private readonly HttpClient _http;
    private readonly ConnectorOptions _options;
    private readonly ILogger<AutoUpdateWorker> _logger;

    public AutoUpdateWorker(HttpClient http, IOptions<ConnectorOptions> options, ILogger<AutoUpdateWorker> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
        _http.BaseAddress = new Uri(_options.ApiBaseUrl.TrimEnd('/') + "/");
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            _http.DefaultRequestHeaders.Add("X-Connector-Api-Key", _options.ApiKey);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndApplyUpdateAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Auto-update check falhou");
                }

                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Encerramento normal ao clicar em Sair — não propagar para o host.
        }
    }

    private async Task CheckAndApplyUpdateAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey)) return;

        var response = await _http.GetAsync(
            $"api/connector/version?current={Uri.EscapeDataString(_options.Version)}", ct);
        if (!response.IsSuccessStatusCode) return;

        var version = await response.Content.ReadFromJsonAsync<VersionResponse>(cancellationToken: ct);
        if (version is not { IsUpdateAvailable: true }) return;

        _logger.LogInformation("Nova versão disponível: {Version}", version.Version);

        var download = await _http.GetAsync($"api/connector/download/{version.Version}", ct);
        download.EnsureSuccessStatusCode();

        var tempZip = Path.Combine(Path.GetTempPath(), $"ntbot-connector-{version.Version}.zip");
        await using (var fs = File.Create(tempZip))
            await download.Content.CopyToAsync(fs, ct);

        if (!string.IsNullOrWhiteSpace(version.Sha256Hash)
            && version.Sha256Hash != "0000000000000000000000000000000000000000000000000000000000000000")
        {
            await using var stream = File.OpenRead(tempZip);
            var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            if (!hash.Equals(version.Sha256Hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("SHA256 inválido no pacote de update.");
        }

        _logger.LogInformation("Update baixado em {Path}. Reinício manual necessário para aplicar.", tempZip);
    }

    private sealed class VersionResponse
    {
        public string Version { get; set; } = string.Empty;
        public string? Sha256Hash { get; set; }
        public bool IsUpdateAvailable { get; set; }
    }
}
