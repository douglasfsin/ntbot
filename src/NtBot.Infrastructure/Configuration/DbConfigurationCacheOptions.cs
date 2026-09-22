namespace NtBot.Infrastructure.Configuration;

public sealed class DbConfigurationCacheOptions
{
    public const string SectionName = "DbConfigurationCache";

    /// <summary>Tempo padrão de retenção em memória (horas).</summary>
    public int TtlHours { get; set; } = 8;

    public TimeSpan Ttl => TimeSpan.FromHours(TtlHours > 0 ? TtlHours : 8);
}
