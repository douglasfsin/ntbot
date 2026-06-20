using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using NtBot.Identity.Dtos;

namespace NtBot.Web.Services;

public class AuthSignInService
{
    public const string AccessTokenClaim = "ntbot_access_token";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthSignInService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task SignInAsync(AuthResponse response)
    {
        var context = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HttpContext indisponível para autenticação.");

        var principal = BuildPrincipal(response);
        var props = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = response.ExpiresAt?.ToUniversalTime() ?? DateTimeOffset.UtcNow.AddDays(1)
        };

        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, props);
    }

    public async Task SignOutAsync()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context != null)
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    public void HydrateSession(AuthSession session)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return;

        var token = user.FindFirst(AccessTokenClaim)?.Value;
        if (string.IsNullOrEmpty(token))
            return;

        session.Token = token;
        session.User = new UserDto
        {
            Id = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!),
            TenantId = Guid.Parse(user.FindFirst("tenant_id")!.Value),
            Email = user.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            FullName = user.FindFirstValue(ClaimTypes.Name),
            Role = user.FindFirstValue(ClaimTypes.Role) ?? "USER",
            EmailConfirmed = true
        };

        var tenantName = user.FindFirst("tenant_name")?.Value;
        if (tenantName != null)
        {
            session.Tenant = new TenantDto
            {
                Id = session.User.TenantId,
                Name = tenantName,
                Email = session.User.Email,
                Plan = user.FindFirst("tenant_plan")?.Value ?? "FREE",
                IsTrial = user.FindFirst("tenant_trial")?.Value == "true"
            };
        }
    }

    public static ClaimsPrincipal BuildPrincipal(AuthResponse response)
    {
        if (response.User == null || string.IsNullOrEmpty(response.Token))
            throw new ArgumentException("Resposta de auth inválida.");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, response.User.Id.ToString()),
            new(ClaimTypes.Email, response.User.Email),
            new("tenant_id", response.User.TenantId.ToString()),
            new(ClaimTypes.Role, response.User.Role),
            new(AccessTokenClaim, response.Token)
        };

        if (!string.IsNullOrEmpty(response.User.FullName))
            claims.Add(new Claim(ClaimTypes.Name, response.User.FullName));

        if (response.Tenant != null)
        {
            claims.Add(new Claim("tenant_name", response.Tenant.Name));
            claims.Add(new Claim("tenant_plan", response.Tenant.Plan));
            claims.Add(new Claim("tenant_trial", response.Tenant.IsTrial ? "true" : "false"));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }
}
