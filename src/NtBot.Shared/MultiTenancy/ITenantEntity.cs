namespace NtBot.Shared.MultiTenancy;

public interface ITenantEntity
{
    Guid TenantId { get; set; }
}
