using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using NtBot.Identity.Dtos;
using NtBot.Web.Components;
using NtBot.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

var apiBaseUrl = ApiUrlResolver.Resolve(builder.Configuration, builder.Environment);

builder.Services.AddHttpContextAccessor();

var dataProtectionPath = Path.Combine(
    Environment.GetEnvironmentVariable("HOME") ?? "/app",
    ".aspnet",
    "DataProtection-Keys");
Directory.CreateDirectory(dataProtectionPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("NtBot.Web");

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(1);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthSession>();
builder.Services.AddScoped<RegisterDraft>();
builder.Services.AddScoped<AuthSignInService>();
builder.Services.AddScoped<JwtAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthStateProvider>());
builder.Services.AddScoped<AuthApiClient>();
builder.Services.AddScoped<BillingApiClient>();
builder.Services.AddScoped<ConnectorApiClient>();
builder.Services.AddScoped<QuantStrategyApiClient>();
builder.Services.AddScoped<ProfitChartApiClient>();
builder.Services.AddScoped<AnalysisApiClient>();
builder.Services.AddScoped<MacroApiClient>();
builder.Services.AddScoped<HealthApiClient>();
builder.Services.AddScoped<ProfitChartHubService>();
builder.Services.AddScoped<ConnectorWebHubService>();
builder.Services.AddScoped<MacroHubService>();
builder.Services.AddScoped<MarketApiClient>();
builder.Services.AddScoped<MarketHubService>();

builder.Services.AddHttpClient("NtBotApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
else
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/app", StringComparison.OrdinalIgnoreCase))
    {
        var auth = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!auth.Succeeded)
        {
            context.Response.Redirect("/login");
            return;
        }
    }

    await next();
});

app.UseAntiforgery();

app.MapPost("/auth/signin-cookie", async (HttpContext context, AuthSignInService signIn, IFormCollection form) =>
{
    var token = form["accessToken"].ToString();
    var userId = form["userId"].ToString();
    var tenantId = form["tenantId"].ToString();
    var email = form["email"].ToString();
    var role = form["role"].ToString();
    var fullName = form["fullName"].ToString();
    var tenantName = form["tenantName"].ToString();
    var tenantPlan = form["tenantPlan"].ToString();
    var tenantTrial = form["tenantTrial"].ToString() == "true";

    if (string.IsNullOrWhiteSpace(token) || !Guid.TryParse(userId, out var uid) || !Guid.TryParse(tenantId, out var tid))
        return Results.Redirect("/login");

    var response = new AuthResponse
    {
        Success = true,
        Token = token,
        User = new UserDto
        {
            Id = uid,
            TenantId = tid,
            Email = email,
            FullName = string.IsNullOrWhiteSpace(fullName) ? null : fullName,
            Role = role,
            EmailConfirmed = true
        },
        Tenant = string.IsNullOrWhiteSpace(tenantName)
            ? null
            : new TenantDto
            {
                Id = tid,
                Name = tenantName,
                Email = email,
                Plan = string.IsNullOrWhiteSpace(tenantPlan) ? "FREE" : tenantPlan,
                IsTrial = tenantTrial
            }
    };

    await signIn.SignInAsync(response);
    return Results.Redirect("/app");
}).DisableAntiforgery();

app.MapGet("/auth/signout-cookie", async (AuthSignInService signIn) =>
{
    await signIn.SignOutAsync();
    return Results.Redirect("/login");
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

if (app.Environment.IsProduction() &&
    apiBaseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase))
{
    app.Logger.LogCritical(
        "API_BASE_URL inválida em Production: {ApiBaseUrl}. Configure a URL pública da NtBot.Api no Coolify.",
        apiBaseUrl);
}
else
{
    app.Logger.LogInformation(
        "NtBot.Web pronto. Environment={Environment} ApiBaseUrl={ApiBaseUrl}",
        app.Environment.EnvironmentName,
        apiBaseUrl);
}

app.Run();
