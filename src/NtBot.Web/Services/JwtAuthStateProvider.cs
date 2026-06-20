using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace NtBot.Web.Services;

public class JwtAuthStateProvider : AuthenticationStateProvider
{
    private readonly AuthSession _session;

    public JwtAuthStateProvider(AuthSession session)
    {
        _session = session;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (string.IsNullOrEmpty(_session.Token) || _session.User == null)
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

        var identity = BuildIdentity(_session);
        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }

    public void NotifyAuthChanged()
    {
        if (string.IsNullOrEmpty(_session.Token) || _session.User == null)
        {
            NotifyAuthenticationStateChanged(Task.FromResult(
                new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))));
            return;
        }

        var identity = BuildIdentity(_session);
        NotifyAuthenticationStateChanged(Task.FromResult(
            new AuthenticationState(new ClaimsPrincipal(identity))));
    }

    private ClaimsIdentity BuildIdentity(AuthSession session)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.User!.Id.ToString()),
            new(ClaimTypes.Email, session.User.Email),
            new("tenant_id", session.User.TenantId.ToString()),
            new(ClaimTypes.Role, session.User.Role)
        };

        if (!string.IsNullOrEmpty(session.User.FullName))
            claims.Add(new Claim(ClaimTypes.Name, session.User.FullName));

        return new ClaimsIdentity(claims, authenticationType: "jwt");
    }

    public static bool IsTokenExpired(string? token)
    {
        if (string.IsNullOrEmpty(token)) return true;
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            return jwt.ValidTo < DateTime.UtcNow;
        }
        catch
        {
            return true;
        }
    }
}
