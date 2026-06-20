using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NtBot.Domain.Entities;
using NtBot.Infrastructure.Persistence;

namespace NtBot.Identity.Services;

public interface IOtpVerificationService
{
    Task<OtpVerification> CreateOtpAsync(Guid? userId, Guid? tenantId, string verificationType, int expirationMinutes = 10);
    Task<OtpVerification?> GetValidOtpAsync(Guid? userId, Guid? tenantId, string otpCode, string verificationType);
    Task MarkAsUsedAsync(Guid otpId);
    Task InvalidateOtpsAsync(Guid? userId, Guid? tenantId, string verificationType);
}

public class OtpVerificationService : IOtpVerificationService
{
    private readonly NtBotDbContext _db;
    private readonly ILogger<OtpVerificationService> _logger;

    public OtpVerificationService(NtBotDbContext db, ILogger<OtpVerificationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<OtpVerification> CreateOtpAsync(Guid? userId, Guid? tenantId, string verificationType, int expirationMinutes = 10)
    {
        if (!userId.HasValue && !tenantId.HasValue && verificationType != OtpVerificationTypes.Registration)
        {
            throw new ArgumentException("UserId or TenantId required");
        }

        await InvalidateOtpsAsync(userId, tenantId, verificationType);

        var otp = new OtpVerification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            OtpCode = OtpCodeGenerator.Generate(),
            VerificationType = verificationType,
            ExpiresAt = OtpCodeGenerator.GetExpiration(expirationMinutes),
            CreatedAt = DateTime.UtcNow
        };

        _db.OtpVerifications.Add(otp);
        await _db.SaveChangesAsync();

        _logger.LogInformation("OTP created type={Type} user={UserId} tenant={TenantId}", verificationType, userId, tenantId);
        return otp;
    }

    public async Task<OtpVerification?> GetValidOtpAsync(Guid? userId, Guid? tenantId, string otpCode, string verificationType)
    {
        var query = _db.OtpVerifications
            .Where(o => o.OtpCode == otpCode
                        && o.VerificationType == verificationType
                        && !o.IsUsed
                        && o.ExpiresAt > DateTime.UtcNow);

        if (userId.HasValue)
            query = query.Where(o => o.UserId == userId);
        if (tenantId.HasValue)
            query = query.Where(o => o.TenantId == tenantId);

        return await query.OrderByDescending(o => o.CreatedAt).FirstOrDefaultAsync();
    }

    public async Task MarkAsUsedAsync(Guid otpId)
    {
        var otp = await _db.OtpVerifications.FindAsync(otpId);
        if (otp != null)
        {
            otp.MarkAsUsed();
        }
    }

    public async Task InvalidateOtpsAsync(Guid? userId, Guid? tenantId, string verificationType)
    {
        var query = _db.OtpVerifications
            .Where(o => o.VerificationType == verificationType && !o.IsUsed);

        if (userId.HasValue)
            query = query.Where(o => o.UserId == userId);
        if (tenantId.HasValue)
            query = query.Where(o => o.TenantId == tenantId);

        var otps = await query.ToListAsync();
        foreach (var otp in otps)
        {
            otp.MarkAsUsed();
        }

        if (otps.Count > 0)
            await _db.SaveChangesAsync();
    }
}
