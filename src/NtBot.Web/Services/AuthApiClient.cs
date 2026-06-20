using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NtBot.Identity.Dtos;

namespace NtBot.Web.Services;

public class AuthApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthSession _session;
    private readonly ILogger<AuthApiClient> _logger;

    public AuthApiClient(
        IHttpClientFactory httpClientFactory,
        AuthSession session,
        ILogger<AuthApiClient> logger)
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

    public Task<AuthResponse?> LoginAsync(LoginRequest request) =>
        PostAsync<LoginRequest, AuthResponse>("api/auth/login", request);

    public Task<AuthResponse?> RegisterInitAsync(RegisterRequest request) =>
        PostAsync<RegisterRequest, AuthResponse>("api/auth/register/init", request);

    public Task<AuthResponse?> RegisterVerifyAsync(RegisterCompleteRequest request) =>
        PostAsync<RegisterCompleteRequest, AuthResponse>("api/auth/register/verify", request);

    public Task<AuthResponse?> ForgotPasswordAsync(ForgotPasswordRequest request) =>
        PostAsync<ForgotPasswordRequest, AuthResponse>("api/auth/forgot-password", request);

    public Task<AuthResponse?> ResetPasswordAsync(ResetPasswordRequest request) =>
        PostAsync<ResetPasswordRequest, AuthResponse>("api/auth/reset-password", request);

    private async Task<TResponse?> PostAsync<TRequest, TResponse>(string path, TRequest body)
    {
        var client = CreateClient();
        var baseAddress = client.BaseAddress?.ToString().TrimEnd('/') ?? "(sem BaseAddress)";
        var fullUrl = $"{baseAddress}/{path}";

        _logger.LogInformation("API POST {Url}", fullUrl);

        try
        {
            var response = await client.PostAsJsonAsync(path, body);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning(
                    "API POST {Url} respondeu {StatusCode}: {Body}",
                    fullUrl,
                    (int)response.StatusCode,
                    errorBody);
            }

            return await response.Content.ReadFromJsonAsync<TResponse>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "Falha de conexão com a API POST {Url} (BaseAddress={BaseAddress})",
                fullUrl,
                baseAddress);

            throw new HttpRequestException(
                $"Não foi possível contactar a API em {baseAddress}. " +
                $"Verifique se NtBot.Api está no ar e se API_BASE_URL está correto no Coolify.",
                ex);
        }
    }
}

public class AuthSession
{
    public string? Token { get; set; }
    public UserDto? User { get; set; }
    public TenantDto? Tenant { get; set; }

    public void SetFromAuthResponse(AuthResponse response)
    {
        Token = response.Token;
        User = response.User;
        Tenant = response.Tenant;
    }

    public void Clear()
    {
        Token = null;
        User = null;
        Tenant = null;
    }
}
