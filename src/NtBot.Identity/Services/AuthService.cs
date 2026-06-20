using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NtBot.Domain.Entities;
using NtBot.Identity.Dtos;
using NtBot.Infrastructure.Persistence;

namespace NtBot.Identity.Services;

public interface IAuthService
{
    Task<AuthResponse> InitiateRegistrationAsync(RegisterRequest request);
    Task<AuthResponse> CompleteRegistrationAsync(RegisterCompleteRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request);
}

public class AuthService : IAuthService
{
    private readonly NtBotDbContext _db;
    private readonly IOtpVerificationService _otp;
    private readonly IEmailService _email;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        NtBotDbContext db,
        IOtpVerificationService otp,
        IEmailService email,
        IPasswordHasher hasher,
        IJwtTokenService jwt,
        ILogger<AuthService> logger)
    {
        _db = db;
        _otp = otp;
        _email = email;
        _hasher = hasher;
        _jwt = jwt;
        _logger = logger;
    }

    public async Task<AuthResponse> InitiateRegistrationAsync(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email) ||
            await _db.Tenants.AnyAsync(t => t.Email == email))
        {
            return Fail("Email já cadastrado", "Email", "Este email já está em uso");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return Fail("Senha deve ter no mínimo 8 caracteres", "Password", "Mínimo 8 caracteres");
        }

        var otp = await _otp.CreateOtpAsync(null, null, OtpVerificationTypes.Registration);
        await _email.SendRegistrationOtpAsync(email, otp.OtpCode, request.TenantName);

        return new AuthResponse
        {
            Success = true,
            Message = $"Código enviado para {email}. Verifique seu email (ou logs da API se SMTP não configurado)."
        };
    }

    public async Task<AuthResponse> CompleteRegistrationAsync(RegisterCompleteRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var otp = await _otp.GetValidOtpAsync(null, null, request.OtpCode, OtpVerificationTypes.Registration);
        if (otp == null)
            return Fail("Código inválido ou expirado");

        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Fail("Email já cadastrado");

        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var trialDays = 7;

        var tenant = new Tenant
        {
            Id = tenantId,
            Name = request.TenantName.Trim(),
            Email = email,
            Plan = SubscriptionPlan.FREE,
            IsActive = true,
            IsTrial = true,
            SubscriptionStart = DateTime.UtcNow,
            SubscriptionEnd = DateTime.UtcNow.AddDays(trialDays),
            CreatedAt = DateTime.UtcNow
        };

        var user = new User
        {
            Id = userId,
            TenantId = tenantId,
            Email = email,
            FullName = request.FullName.Trim(),
            PasswordHash = _hasher.Hash(request.Password),
            Role = UserRole.ADMIN,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Tenants.Add(tenant);
        _db.Users.Add(user);
        await _otp.MarkAsUsedAsync(otp.Id);
        await _db.SaveChangesAsync();

        await _email.SendWelcomeEmailAsync(email, user.FullName ?? email, tenant.Name);

        var (token, expires) = _jwt.CreateToken(user);
        _logger.LogInformation("Registration complete tenant={TenantId} user={UserId}", tenantId, userId);

        return Success("Cadastro realizado com sucesso", token, expires, user, tenant);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null || !_hasher.Verify(request.Password, user.PasswordHash))
            return Fail("Email ou senha inválidos");

        if (!user.IsActive)
            return Fail("Usuário desativado");

        if (user.Tenant != null && !user.Tenant.IsActive)
            return Fail("Conta desativada. Entre em contato com o suporte.");

        user.LastLogin = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var (token, expires) = _jwt.CreateToken(user);
        return Success("Login realizado com sucesso", token, expires, user, user.Tenant);
    }

    public async Task<AuthResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user != null)
        {
            var otp = await _otp.CreateOtpAsync(user.Id, user.TenantId, OtpVerificationTypes.ForgotPassword);
            await _email.SendPasswordResetOtpAsync(email, otp.OtpCode, user.FullName ?? email);
        }

        return new AuthResponse
        {
            Success = true,
            Message = "Se o email existir, um código de recuperação foi enviado"
        };
    }

    public async Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
            return Fail("Usuário não encontrado");

        var otp = await _otp.GetValidOtpAsync(user.Id, user.TenantId, request.OtpCode, OtpVerificationTypes.ForgotPassword);
        if (otp == null)
            return Fail("Código inválido ou expirado");

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return Fail("Senha deve ter no mínimo 8 caracteres");

        user.PasswordHash = _hasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _otp.MarkAsUsedAsync(otp.Id);
        await _db.SaveChangesAsync();

        await _email.SendPasswordChangedAsync(email, user.FullName ?? email);

        return new AuthResponse { Success = true, Message = "Senha alterada com sucesso" };
    }

    private static AuthResponse Fail(string message, string? field = null, string? fieldMessage = null) =>
        new()
        {
            Success = false,
            Message = message,
            Errors = field != null
                ? new Dictionary<string, string[]> { [field] = [fieldMessage ?? message] }
                : null
        };

    private static AuthResponse Success(string message, string token, DateTime expires, User user, Tenant? tenant) =>
        new()
        {
            Success = true,
            Message = message,
            Token = token,
            ExpiresAt = expires,
            User = MapUser(user),
            Tenant = tenant != null ? MapTenant(tenant) : null
        };

    private static UserDto MapUser(User user) => new()
    {
        Id = user.Id,
        TenantId = user.TenantId,
        Email = user.Email,
        FullName = user.FullName,
        Role = user.Role.ToString(),
        EmailConfirmed = user.EmailConfirmed
    };

    private static TenantDto MapTenant(Tenant tenant) => new()
    {
        Id = tenant.Id,
        Name = tenant.Name,
        Email = tenant.Email,
        Plan = tenant.Plan.ToString(),
        IsTrial = tenant.IsTrial
    };
}
