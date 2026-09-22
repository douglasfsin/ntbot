namespace NtBot.Api.Configuration;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string Configuration { get; set; } = string.Empty;
    public string InstanceName { get; set; } = "NTBot:";
}
