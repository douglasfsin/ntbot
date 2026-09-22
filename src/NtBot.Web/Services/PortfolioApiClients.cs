using System.Net.Http.Json;
using NtBot.Web.Models;

namespace NtBot.Web.Services;

public class BrandingApiClient : AuthenticatedApiClient
{
    public BrandingApiClient(IHttpClientFactory httpClientFactory, AuthSession session)
        : base(httpClientFactory, session) { }

    public Task<TenantBrandingModel?> GetBySlugAsync(string slug) =>
        GetAsync<TenantBrandingModel>(
            $"api/tenants/branding/by-slug/{Uri.EscapeDataString(slug)}",
            authenticated: false);

    public Task<BrandingMeResponse?> GetMineAsync() =>
        GetAsync<BrandingMeResponse>("api/tenants/branding/me", authenticated: true);

    public async Task<(TenantBrandingModel? Ok, string? Error)> UpsertAsync(TenantBrandingModel model)
    {
        var client = CreateClient(authenticated: true);
        var response = await client.PutAsJsonAsync("api/tenants/branding/me", model);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            return (null, ExtractMessage(body) ?? "Falha ao salvar branding");
        }
        return (await response.Content.ReadFromJsonAsync<TenantBrandingModel>(), null);
    }

    private static string? ExtractMessage(string body)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var msg))
                return msg.GetString();
        }
        catch { /* ignore */ }
        return null;
    }
}

public class PortfolioApiClient : AuthenticatedApiClient
{
    public PortfolioApiClient(IHttpClientFactory httpClientFactory, AuthSession session)
        : base(httpClientFactory, session) { }

    public Task<TenantFeaturesModel?> GetFeaturesAsync() =>
        GetAsync<TenantFeaturesModel>("api/portfolio/features", authenticated: true);

    public Task<List<ClientSummaryModel>?> ListClientsAsync() =>
        GetAsync<List<ClientSummaryModel>>("api/portfolio/clients", authenticated: true);

    public Task<List<GoalPortfolioModel>?> ListPortfoliosAsync(Guid? clientUserId = null)
    {
        var url = "api/portfolio/portfolios";
        if (clientUserId.HasValue)
            url += $"?clientUserId={clientUserId}";
        return GetAsync<List<GoalPortfolioModel>>(url, authenticated: true);
    }

    public Task<GoalPortfolioModel?> GetPortfolioAsync(Guid id) =>
        GetAsync<GoalPortfolioModel>($"api/portfolio/portfolios/{id}", authenticated: true);

    public async Task<(GoalPortfolioModel? Ok, string? Error)> CreatePortfolioAsync(object request)
    {
        var client = CreateClient(authenticated: true);
        var response = await client.PostAsJsonAsync("api/portfolio/portfolios", request);
        if (!response.IsSuccessStatusCode)
            return (null, await ReadError(response));
        return (await response.Content.ReadFromJsonAsync<GoalPortfolioModel>(), null);
    }

    public async Task<(PortfolioPositionModel? Ok, string? Error)> AddPositionAsync(Guid portfolioId, object request)
    {
        var client = CreateClient(authenticated: true);
        var response = await client.PostAsJsonAsync($"api/portfolio/portfolios/{portfolioId}/positions", request);
        if (!response.IsSuccessStatusCode)
            return (null, await ReadError(response));
        return (await response.Content.ReadFromJsonAsync<PortfolioPositionModel>(), null);
    }

    public async Task<(int Imported, string? Error)> ImportCsvAsync(Guid portfolioId, string csv)
    {
        var client = CreateClient(authenticated: true);
        var response = await client.PostAsJsonAsync($"api/portfolio/portfolios/{portfolioId}/import-csv", new { csvContent = csv });
        if (!response.IsSuccessStatusCode)
            return (0, await ReadError(response));
        using var doc = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var imported = doc.RootElement.TryGetProperty("imported", out var i) ? i.GetInt32() : 0;
        return (imported, null);
    }

    public Task<List<ProductCatalogModel>?> ListProductsAsync(string? family = null, bool? retailOnly = null, string? ratingMin = null)
    {
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(family)) qs.Add($"family={Uri.EscapeDataString(family)}");
        if (retailOnly.HasValue) qs.Add($"retailOnly={retailOnly.Value.ToString().ToLowerInvariant()}");
        if (!string.IsNullOrWhiteSpace(ratingMin)) qs.Add($"ratingMin={Uri.EscapeDataString(ratingMin)}");
        var url = "api/portfolio/products" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
        return GetAsync<List<ProductCatalogModel>>(url, authenticated: true);
    }

    public async Task<(PortfolioOfferModel? Ok, string? Error)> SendOfferAsync(object request)
    {
        var client = CreateClient(authenticated: true);
        var response = await client.PostAsJsonAsync("api/portfolio/offers", request);
        if (!response.IsSuccessStatusCode)
            return (null, await ReadError(response));
        return (await response.Content.ReadFromJsonAsync<PortfolioOfferModel>(), null);
    }

    public Task<List<PortfolioOfferModel>?> ListOffersAsync(bool pendingOnly = false) =>
        GetAsync<List<PortfolioOfferModel>>($"api/portfolio/offers?pendingOnly={pendingOnly}", authenticated: true);

    public async Task<(PortfolioOfferModel? Ok, string? Error)> RespondOfferAsync(Guid offerId, bool accept)
    {
        var client = CreateClient(authenticated: true);
        var response = await client.PostAsJsonAsync($"api/portfolio/offers/{offerId}/respond", new { accept });
        if (!response.IsSuccessStatusCode)
            return (null, await ReadError(response));
        return (await response.Content.ReadFromJsonAsync<PortfolioOfferModel>(), null);
    }

    public async Task UpsertProfileAsync(Guid clientUserId, string risk, string segment)
    {
        var client = CreateClient(authenticated: true);
        await client.PutAsJsonAsync("api/portfolio/clients/profile", new
        {
            clientUserId,
            riskProfile = risk,
            segment
        });
    }

    public async Task<(GoalPortfolioModel? Ok, string? Error)> UpdatePortfolioAsync(Guid id, object request)
    {
        var client = CreateClient(authenticated: true);
        var response = await client.PutAsJsonAsync($"api/portfolio/portfolios/{id}", request);
        if (!response.IsSuccessStatusCode)
            return (null, await ReadError(response));
        return (await response.Content.ReadFromJsonAsync<GoalPortfolioModel>(), null);
    }

    public Task<PortfolioPerformanceModel?> GetPerformanceAsync(Guid id) =>
        GetAsync<PortfolioPerformanceModel>($"api/portfolio/portfolios/{id}/performance", authenticated: true);

    public async Task<(PortfolioValuationSnapshotModel? Ok, string? Error)> CaptureValuationAsync(Guid id, string? notes = null)
    {
        var client = CreateClient(authenticated: true);
        var response = await client.PostAsJsonAsync($"api/portfolio/portfolios/{id}/valuations/capture", new { notes });
        if (!response.IsSuccessStatusCode)
            return (null, await ReadError(response));
        return (await response.Content.ReadFromJsonAsync<PortfolioValuationSnapshotModel>(), null);
    }

    public Task<List<PortfolioValuationSnapshotModel>?> ListValuationsAsync(Guid id) =>
        GetAsync<List<PortfolioValuationSnapshotModel>>($"api/portfolio/portfolios/{id}/valuations", authenticated: true);

    public async Task<(PortfolioCashflowModel? Ok, string? Error)> AddCashflowAsync(Guid id, decimal amount, string type)
    {
        var client = CreateClient(authenticated: true);
        var response = await client.PostAsJsonAsync($"api/portfolio/portfolios/{id}/cashflows", new { amount, type });
        if (!response.IsSuccessStatusCode)
            return (null, await ReadError(response));
        return (await response.Content.ReadFromJsonAsync<PortfolioCashflowModel>(), null);
    }

    public Task<List<PortfolioCashflowModel>?> ListCashflowsAsync(Guid id) =>
        GetAsync<List<PortfolioCashflowModel>>($"api/portfolio/portfolios/{id}/cashflows", authenticated: true);

    public async Task<PortfolioAnalysisModel?> AnalyzeAsync(Guid id, string? customPrompt = null)
    {
        var client = CreateClient(authenticated: true);
        var response = await client.PostAsJsonAsync($"api/portfolio/portfolios/{id}/analyze", new { customPrompt });
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<PortfolioAnalysisModel>();
    }

    public async Task<(PortfolioReportJobModel? Ok, string? Error)> GenerateReportAsync(Guid id, bool sendEmail, string? email)
    {
        var client = CreateClient(authenticated: true);
        var response = await client.PostAsJsonAsync($"api/portfolio/portfolios/{id}/report", new { email, sendEmail });
        if (!response.IsSuccessStatusCode)
            return (null, await ReadError(response));
        return (await response.Content.ReadFromJsonAsync<PortfolioReportJobModel>(), null);
    }

    public Task<PortfolioReportJobModel?> GetReportAsync(Guid jobId) =>
        GetAsync<PortfolioReportJobModel>($"api/portfolio/reports/{jobId}", authenticated: true);

    public string GetReportPdfUrl(Guid jobId) => $"api/portfolio/reports/{jobId}/pdf";

    public Task<OpenFinanceStatusModel?> OpenFinanceStatusAsync() =>
        GetAsync<OpenFinanceStatusModel>("api/portfolio/open-finance/status", authenticated: true);

    public Task<List<OpenFinanceConsentModel>?> ListConsentsAsync(Guid? clientUserId = null)
    {
        var url = "api/portfolio/open-finance/consents";
        if (clientUserId.HasValue) url += $"?clientUserId={clientUserId}";
        return GetAsync<List<OpenFinanceConsentModel>>(url, authenticated: true);
    }

    public async Task<(OpenFinanceConsentModel? Ok, string? Error)> RequestConsentAsync(string provider, Guid? clientUserId = null)
    {
        var client = CreateClient(authenticated: true);
        var response = await client.PostAsJsonAsync("api/portfolio/open-finance/consents", new
        {
            clientUserId = clientUserId ?? Guid.Empty,
            provider
        });
        if (!response.IsSuccessStatusCode)
            return (null, await ReadError(response));
        return (await response.Content.ReadFromJsonAsync<OpenFinanceConsentModel>(), null);
    }

    public async Task<(OpenFinanceConsentModel? Ok, string? Error)> GrantConsentAsync(Guid consentId)
    {
        var client = CreateClient(authenticated: true);
        var response = await client.PostAsync($"api/portfolio/open-finance/consents/{consentId}/grant", null);
        if (!response.IsSuccessStatusCode)
            return (null, await ReadError(response));
        return (await response.Content.ReadFromJsonAsync<OpenFinanceConsentModel>(), null);
    }

    public async Task<(OpenFinanceConsentModel? Ok, string? Error)> RevokeConsentAsync(Guid consentId)
    {
        var client = CreateClient(authenticated: true);
        var response = await client.PostAsync($"api/portfolio/open-finance/consents/{consentId}/revoke", null);
        if (!response.IsSuccessStatusCode)
            return (null, await ReadError(response));
        return (await response.Content.ReadFromJsonAsync<OpenFinanceConsentModel>(), null);
    }

    public async Task<(OpenFinanceImportResultModel? Ok, string? Error)> ImportOpenFinanceAsync(Guid portfolioId, Guid? consentId = null)
    {
        var client = CreateClient(authenticated: true);
        var response = await client.PostAsJsonAsync("api/portfolio/open-finance/import", new { portfolioId, consentId });
        if (!response.IsSuccessStatusCode)
            return (null, await ReadError(response));
        return (await response.Content.ReadFromJsonAsync<OpenFinanceImportResultModel>(), null);
    }

    private static async Task<string> ReadError(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var msg))
                return msg.GetString() ?? $"Erro {(int)response.StatusCode}";
        }
        catch { /* ignore */ }
        return $"Erro {(int)response.StatusCode}";
    }
}
