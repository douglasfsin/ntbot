using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace NtBot.Web.Services;

public class BillingApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthSession _session;
    private readonly ILogger<BillingApiClient> _logger;

    public BillingApiClient(
        IHttpClientFactory httpClientFactory,
        AuthSession session,
        ILogger<BillingApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _session = session;
        _logger = logger;
    }

    private HttpClient CreateClient(bool authenticated = false)
    {
        var client = _httpClientFactory.CreateClient("NtBotApi");
        if (authenticated && !string.IsNullOrEmpty(_session.Token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _session.Token);
        return client;
    }

    public async Task<BillingConfigModel?> GetConfigAsync()
    {
        var client = CreateClient();
        return await client.GetFromJsonAsync<BillingConfigModel>("api/billing/config");
    }

    public async Task<List<PlanModel>> GetPlansAsync()
    {
        var client = CreateClient();
        var plans = await client.GetFromJsonAsync<List<PlanModel>>("api/billing/plans");
        return plans ?? [];
    }

    public async Task<SubscriptionModel?> GetSubscriptionAsync()
    {
        var client = CreateClient(authenticated: true);
        var response = await client.GetAsync("api/billing/subscription");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SubscriptionModel>();
    }

    public async Task<CheckoutResult?> CreateCheckoutAsync(string planSlug)
    {
        var client = CreateClient(authenticated: true);
        var response = await client.PostAsJsonAsync("api/billing/checkout", new { planSlug });

        var result = await response.Content.ReadFromJsonAsync<CheckoutResult>();
        if (result == null && !response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Checkout falhou | Status={Status}", (int)response.StatusCode);
            return new CheckoutResult { Success = false, Message = "Erro ao iniciar checkout." };
        }

        return result;
    }

    public async Task<CheckoutResult?> ConfirmCheckoutAsync(string sessionId)
    {
        var client = CreateClient(authenticated: true);
        var response = await client.PostAsJsonAsync("api/billing/confirm", new { sessionId });
        return await response.Content.ReadFromJsonAsync<CheckoutResult>();
    }
}

public class BillingConfigModel
{
    public bool StripeConfigured { get; set; }
    public string? PublishableKey { get; set; }
    public bool TestMode { get; set; }
}

public class PlanModel
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MonthlyPrice { get; set; }
    public decimal? YearlyPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public int MaxStrategies { get; set; }
    public int MaxBrokers { get; set; }
    public int MaxActivePositions { get; set; }
    public bool IsFree { get; set; }
}

public class SubscriptionModel
{
    public Guid Id { get; set; }
    public string PlanSlug { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public decimal MonthlyPrice { get; set; }
    public bool IsActive { get; set; }
}

public class CheckoutResult
{
    public bool Success { get; set; }
    public string? CheckoutUrl { get; set; }
    public Guid? SubscriptionId { get; set; }
    public string? Message { get; set; }
}
